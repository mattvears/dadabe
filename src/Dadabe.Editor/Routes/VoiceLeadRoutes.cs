using System.Collections.Immutable;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            [FromServices] SongService songs,
            [FromServices] ChordParser parser,
            [FromServices] ChordExpander expander) =>
        {
            var form = await req.ReadFormAsync();
            var chordsRaw = form["chords"].ToString().Trim();
            var solutions = int.TryParse(form["solutions"], out var s) ? Math.Clamp(s, 1, 10) : 1;
            var minComfortPct = int.TryParse(form["minComfort"], out var mc) ? Math.Clamp(mc, 0, 100) : 70;
            var minComfort = minComfortPct / 100.0;

            // Sourcing from a song section (D57 bug #6) supplies its own chord
            // list, bars, tuning, and hand constraints — the manual tuning
            // field and typed chords are ignored in that mode.
            var useActiveSection = form["useActiveSection"].Contains("true");
            var songSlug = form["songSlug"].ToString();
            var sectionId = form["sectionId"].ToString();

            string[] chordSymbols;
            int[] bars;
            string tuning, tuningLabel;
            HandModel hand = HandModel.Default;
            SearchParams searchParams = SearchParams.Default;
            SongSection? sourceSection = null;

            if (useActiveSection)
            {
                if (string.IsNullOrWhiteSpace(songSlug) || string.IsNullOrWhiteSpace(sectionId))
                    return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                        VoiceLeadResultModel.FromError(chordsRaw, "", "Select an active song and section (top right) first."));

                var (song, section) = songs.FindSection(songSlug, sectionId);
                if (song is null || section is null)
                    return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                        VoiceLeadResultModel.FromError(chordsRaw, "", "Song or section not found."));
                if (section.Slots.Count == 0)
                    return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                        VoiceLeadResultModel.FromError(chordsRaw, "", "Section has no chords to voice-lead."));

                sourceSection = section;
                chordSymbols = section.Slots.Select(sl => sl.Symbol).ToArray();
                bars = section.Slots.Select(sl => sl.Bars).ToArray();
                tuning = song.Tuning ?? "DADABE";
                tuningLabel = tuning;
                hand = SongHandResolver.ResolveHandModel(song.Hand);
                searchParams = SongHandResolver.ResolveSearchParams(song.Hand);
            }
            else
            {
                tuning = form["tuning"].ToString().Trim();
                tuningLabel = VoicingRoutes.ResolveLabel(form["tuningName"].ToString(), tuning, catalogs);

                if (string.IsNullOrWhiteSpace(chordsRaw))
                    return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                        VoiceLeadResultModel.FromError("?", tuningLabel, "Chords are required."));

                chordSymbols = chordsRaw
                    .Split(ChordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (chordSymbols.Length == 0)
                    return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                        VoiceLeadResultModel.FromError(chordsRaw, tuningLabel, "Enter at least one chord."));
                bars = chordSymbols.Select(_ => 1).ToArray();
            }

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
                var set = VoicingSearch.Search(spec, resolvedTuning, hand,
                    searchParams, catalogs.VoicingCategories);

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
                        Moves: moves,
                        Diagram: ChordDiagramBuilder.FromVoicing(step.Voicing),
                        Bars: bars[i],
                        Positions: positions.Select(p => new VoiceLeadPositionRow(p.String, p.Fret, p.Muted, p.Open)).ToList()));
                }
                return new VoiceLeadSolutionRow(idx + 1, sol.TotalDistance, steps);
            }).ToList();

            return Results.RazorSlice<VoiceLeadingResult, VoiceLeadResultModel>(
                new VoiceLeadResultModel(chordsRaw, tuningLabel, solutionRows, null, resolvedTuning.Strings.Length,
                    sourceSection is not null ? songSlug : null, sourceSection is not null ? sectionId : null));
        });

        // Replaces a song section's slots wholesale with one voice-lead
        // solution (D57 bug #6) — the counterpart to "Fill unpinned" (§9),
        // but for a result the user picked explicitly rather than solved
        // automatically.
        app.MapPost("/api/voiceleading/apply", async (
            HttpRequest req,
            [FromServices] Catalogs catalogs,
            [FromServices] SongService songs) =>
        {
            var form = await req.ReadFormAsync();
            var slug = form["slug"].ToString();
            var sectionId = form["sectionId"].ToString();
            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(sectionId))
                return Results.Content(
                    """<p style="color:var(--wa-color-danger-600)">Select an active song and section (top right) first.</p>""",
                    "text/html");

            List<VoiceLeadApplyStepDto>? steps;
            try { steps = JsonSerializer.Deserialize<List<VoiceLeadApplyStepDto>>(form["steps"].ToString()); }
            catch (JsonException) { steps = null; }
            if (steps is null || steps.Count == 0)
                return Results.Content(
                    """<p style="color:var(--wa-color-danger-600)">Malformed voice-lead result.</p>""",
                    "text/html");

            var song = songs.Get(slug);
            if (song is null)
                return Results.Content(
                    """<p style="color:var(--wa-color-danger-600)">Song not found.</p>""",
                    "text/html");

            var tuningName = song.Tuning ?? "DADABE";
            Dadabe.Core.Tuning tuning;
            try { tuning = VoicingRoutes.ResolveTuningPublic(catalogs, tuningName, tuningName); }
            catch (FormatException ex)
            {
                return Results.Content(
                    $"""<p style="color:var(--wa-color-danger-600)">{WebUtility.HtmlEncode(ex.Message)}</p>""", "text/html");
            }

            var hand = SongHandResolver.ResolveHandModel(song.Hand);
            var provenance = new PinProvenance((int)ModelVersion.V1, tuning.Name, hand.ContentHash.ToString());
            var newSlots = steps.Select(st => new SongSlot(
                st.Symbol,
                st.Bars,
                new PinnedVoicing(
                    st.Positions.Select(p => new PinnedPosition(p.String, p.Fret, p.Muted, p.Open)).ToList(),
                    st.ComfortPct,
                    st.Structure),
                provenance)).ToList();

            var (ok, error) = songs.ReplaceSlots(slug, sectionId, newSlots);
            return Results.Content(
                ok
                    ? """<p style="color:var(--wa-color-success-600)">Section updated from the voice-lead result.</p>"""
                    : $"""<p style="color:var(--wa-color-danger-600)">{WebUtility.HtmlEncode(error)}</p>""",
                "text/html");
        });
    }
}

file sealed record VoiceLeadApplyPositionDto(
    [property: JsonPropertyName("string")] int String,
    [property: JsonPropertyName("fret")] int? Fret,
    [property: JsonPropertyName("muted")] bool Muted,
    [property: JsonPropertyName("open")] bool Open);

file sealed record VoiceLeadApplyStepDto(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("bars")] int Bars,
    [property: JsonPropertyName("comfortPct")] int ComfortPct,
    [property: JsonPropertyName("structure")] string Structure,
    [property: JsonPropertyName("positions")] List<VoiceLeadApplyPositionDto> Positions);

file static class StringExtensionsVL
{
    public static string? NullIfEmptyVL(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
