using System.Collections.Immutable;
using Dadabe.Core.Chord;
using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;
using Dadabe.Fretboard;
using Microsoft.AspNetCore.Mvc;

namespace Dadabe.Editor.Routes;

public static class VoiceLeadRoutes
{
    private static readonly char[] ChordSeparators = [' ', '\n', '\r'];
    public static void MapVoiceLeadRoutes(this WebApplication app)
    {
        app.MapGet("/voice-lead", ([FromServices] Catalogs catalogs, [FromServices] TuningService tunings) =>
        {
            var embeddedNames = catalogs.Tunings.All
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .Select(t => t.Name)
                .ToList();

            return Results.RazorSlice<VoiceLeadingIndex, VoiceLeadIndexModel>(
                new VoiceLeadIndexModel(embeddedNames, tunings.ListAll()));
        });

        app.MapPost("/api/voiceleading/run", async (
            HttpRequest req,
            [FromServices] Catalogs catalogs,
            [FromServices] TuningService tuningService,
            [FromServices] ChordParser parser,
            [FromServices] ChordExpander expander) =>
        {
            var form        = await req.ReadFormAsync();
            var chordsRaw   = form["chords"].ToString().Trim();
            var tuning      = form["tuning"].ToString().Trim();
            var tuningLabel = VoicingRoutes.ResolveLabel(form["tuningName"].ToString(), tuning, catalogs);
            var solutions   = int.TryParse(form["solutions"],  out var s) ? Math.Clamp(s, 1, 10) : 1;
            var minComfortPct = int.TryParse(form["minComfort"], out var mc) ? Math.Clamp(mc, 0, 100) : 70;
            var minComfort  = minComfortPct / 100.0;

            if (string.IsNullOrWhiteSpace(chordsRaw))
                return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                    VoiceLeadResultModel.FromError("?", tuningLabel, "Chords are required."));

            // Parse all chord symbols up-front.
            var chordSymbols = chordsRaw
                .Split(ChordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (chordSymbols.Length == 0)
                return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                    VoiceLeadResultModel.FromError(chordsRaw, tuningLabel, "Enter at least one chord."));

            Dadabe.Core.Tuning resolvedTuning;
            try { resolvedTuning = VoicingRoutes.ResolveTuningPublic(catalogs, tuning, tuningLabel); }
            catch (FormatException ex)
            {
                return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                    VoiceLeadResultModel.FromError(chordsRaw, tuningLabel, ex.Message));
            }

            var voicingsPerChord = new ImmutableArray<Voicing>[chordSymbols.Length];
            for (int i = 0; i < chordSymbols.Length; i++)
            {
                if (!parser.TryParse(chordSymbols[i], out var sym, out var parseErr))
                    return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                        VoiceLeadResultModel.FromError(chordsRaw, tuningLabel,
                            $"Invalid chord '{chordSymbols[i]}': {parseErr}"));

                var spec = expander.Expand(sym);
                var set  = VoicingSearch.Search(spec, resolvedTuning, HandModel.Default,
                    SearchParams.Default, catalogs.VoicingCategories);

                var voicings = set.Voicings;
                if (minComfort > 0.0)
                    voicings = voicings.Where(v => v.Comfort >= minComfort).ToImmutableArray();

                if (voicings.Length == 0)
                    return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                        VoiceLeadResultModel.FromError(chordsRaw, tuningLabel,
                            $"Chord '{chordSymbols[i]}' has no playable voicings above min-comfort {minComfortPct}%."));

                voicingsPerChord[i] = voicings;
            }

            var solved = VoiceLeadSolver.Solve(voicingsPerChord, solutions);

            var solutionRows = solved.Select((sol, idx) =>
            {
                var steps = new List<VoiceLeadStepRow>(sol.Steps.Count);
                for (var i = 0; i < sol.Steps.Count; i++)
                {
                    var step = sol.Steps[i];
                    var positions = step.Voicing.Positions.OrderBy(p => p.String).ToList();
                    var ascii = "[" + string.Join(" ", positions.Select(p =>
                        p.Muted ? "x" : p.Open ? "0" : p.Fret!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "]";

                    List<VoiceMoveRow>? moves = null;
                    if (i > 0)
                    {
                        var transition = VoiceLeadSolver.BuildTransition(sol.Steps[i - 1].Voicing, step.Voicing);
                        moves = transition.Moves
                            .Select(m => new VoiceMoveRow(m.StringIndex, m.FromFret, m.ToFret, m.Distance))
                            .ToList();
                    }

                    steps.Add(new VoiceLeadStepRow(
                        Chord: step.Voicing.ChordSpec.Root + step.Voicing.ChordSpec.Quality,
                        AsciiNotation: ascii,
                        Structure: step.Voicing.Structure,
                        ComfortPct: (int)Math.Round(step.Voicing.Comfort * 100),
                        TransitionDistance: step.TransitionDistance,
                        Moves: moves));
                }
                return new VoiceLeadSolutionRow(idx + 1, sol.TotalDistance, steps);
            }).ToList();

            return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                new VoiceLeadResultModel(chordsRaw, tuningLabel, solutionRows, null, resolvedTuning.Strings.Length));
        });
    }
}

file static class StringExtensionsVL
{
    public static string? NullIfEmptyVL(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
