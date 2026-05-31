using Dadabe.Core.Chord;
using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;
using Dadabe.Fretboard;
using Microsoft.AspNetCore.Mvc;

namespace Dadabe.Editor.Routes;

public static class VoicingRoutes
{
    public static void MapVoicingRoutes(this WebApplication app)
    {
        app.MapGet("/voicings", ([FromServices] Catalogs catalogs, [FromServices] TuningService tunings) =>
        {
            var embeddedNames = catalogs.Tunings.All
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .Select(t => t.Name)
                .ToList();

            return Results.RazorSlice<VoicingsIndex, VoicingsIndexModel>(
                new VoicingsIndexModel(embeddedNames, tunings.ListAll()));
        });

        app.MapPost("/api/voicings/run", async (
            HttpRequest req,
            [FromServices] Catalogs catalogs,
            [FromServices] TuningService tuningService,
            [FromServices] ChordParser parser,
            [FromServices] ChordExpander expander) =>
        {
            var form        = await req.ReadFormAsync();
            var chord       = form["chord"].ToString().Trim();
            var tuning      = form["tuning"].ToString().Trim();
            var tuningLabel = form["tuningName"].ToString().Trim().NullIfEmpty() ?? tuning;
            var limit       = int.TryParse(form["limit"],   out var l) ? l : 10;
            var topN        = int.TryParse(form["topN"],    out var n) ? n : 0;
            var entropy     = double.TryParse(form["entropy"],
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var e) ? e : 0.5;

            if (string.IsNullOrWhiteSpace(chord))
                return Results.RazorSlice<VoicingsResult, VoicingResultModel>(
                    VoicingResultModel.FromError("?", tuningLabel, "Chord is required."));

            if (!parser.TryParse(chord, out var symbol, out var parseError))
                return Results.RazorSlice<VoicingsResult, VoicingResultModel>(
                    VoicingResultModel.FromError(chord, tuningLabel, $"Invalid chord symbol: {parseError}"));

            Dadabe.Core.Tuning resolvedTuning;
            try
            {
                resolvedTuning = ResolveTuning(catalogs, tuning, tuningLabel);
            }
            catch (FormatException ex)
            {
                return Results.RazorSlice<VoicingsResult, VoicingResultModel>(
                    VoicingResultModel.FromError(chord, tuningLabel, ex.Message));
            }

            var spec = expander.Expand(symbol);
            var set  = VoicingSearch.Search(spec, resolvedTuning, HandModel.Default, SearchParams.Default, catalogs.VoicingCategories);

            var voicings = set.Voicings;
            var total    = voicings.Length;
            if (limit > 0 && voicings.Length > limit)
                voicings = voicings[..limit];

            var rows = voicings
                .Select((v, i) => new VoicingRow(
                    Index: i + 1,
                    Structure: v.Structure,
                    ComfortPct: (int)Math.Round(v.Comfort * 100),
                    Positions: FormatPositions(v)))
                .ToList();

            var nextChords = NextChordPredictor.Predict(spec, topN, entropy)
                .Select(c => new NextChordEntry(c.Symbol, c.Probability))
                .ToList();

            return Results.RazorSlice<VoicingsResult, VoicingResultModel>(
                new VoicingResultModel(chord, tuningLabel, total, rows, nextChords, null));
        });
    }

    private static Dadabe.Core.Tuning ResolveTuning(Catalogs catalogs, string nameOrSpec, string displayName)
    {
        if (string.IsNullOrWhiteSpace(nameOrSpec))
            throw new FormatException("Tuning is required.");

        if (catalogs.Tunings.TryGet(nameOrSpec, out var named))
            return named!;

        if (nameOrSpec.Contains(',', StringComparison.Ordinal))
            return Dadabe.Core.Tuning.ParseSpec(nameOrSpec, displayName);

        throw new FormatException($"Unknown tuning '{nameOrSpec}'.");
    }

    private static string FormatPositions(Voicing voicing)
    {
        return string.Join("  ", voicing.Positions
            .OrderBy(p => p.String)
            .Select(p =>
            {
                var stringName = voicing.Tuning.Strings[p.String].ToString();
                if (p.Muted) return $"{stringName}:×";
                var fret = p.Fret!.Value == 0 ? "0" : p.Fret!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var fn   = p.Function is not null ? $"/{p.Function}" : "";
                return $"{stringName}:{fret}{fn}";
            }));
    }
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
