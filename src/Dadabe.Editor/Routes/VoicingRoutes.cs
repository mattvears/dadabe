using System.Collections.Immutable;
using Microsoft.Extensions.Primitives;
using Dadabe.Core.Chord;
using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;
using Dadabe.Fretboard;
using Microsoft.AspNetCore.Mvc;

namespace Dadabe.Editor.Routes;

public static class VoicingRoutes
{
    private static readonly string[] AllCategories = ["power", "triad", "shell", "drop-2", "drop-3", "spread"];
    public static void MapVoicingRoutes(this WebApplication app)
    {
        // Inline voicing fragment — used by the predictions result card.
        app.MapGet("/api/voicings/fragment", (
            HttpRequest req,
            [FromQuery] string chord,
            [FromQuery] string tuning,
            [FromServices] Catalogs catalogs,
            [FromServices] ChordParser parser,
            [FromServices] ChordExpander expander) =>
        {
            if (string.IsNullOrWhiteSpace(chord))
                return Results.RazorSlice<VoicingFragment, VoicingFragmentModel>(
                    new VoicingFragmentModel("?", tuning, [], "Chord is required."));

            if (!parser.TryParse(chord, out var symbol, out var parseError))
                return Results.RazorSlice<VoicingFragment, VoicingFragmentModel>(
                    new VoicingFragmentModel(chord, tuning, [], $"Invalid chord: {parseError}"));

            Dadabe.Core.Tuning resolvedTuning;
            try { resolvedTuning = ResolveTuning(catalogs, tuning, tuning); }
            catch (FormatException ex)
            {
                return Results.RazorSlice<VoicingFragment, VoicingFragmentModel>(
                    new VoicingFragmentModel(chord, tuning, [], ex.Message));
            }

            var searchParams = BuildSearchParams(key => req.Query[key]);
            var minComfortPct = int.TryParse(req.Query["minComfort"], out var mc) ? Math.Clamp(mc, 0, 100) : 0;
            var minComfort = minComfortPct / 100.0;

            var spec = expander.Expand(symbol);
            var set  = VoicingSearch.Search(spec, resolvedTuning, HandModel.Default,
                searchParams, catalogs.VoicingCategories);

            var voicings = set.Voicings;
            if (minComfort > 0.0)
                voicings = voicings.Where(v => v.Comfort >= minComfort).ToImmutableArray();

            var rows = voicings
                .Take(12)
                .Select(v =>
                {
                    var ascii = "[" + string.Join(" ", v.Positions.OrderBy(p => p.String).Select(p =>
                        p.Muted ? "x" : p.Open ? "0" : p.Fret!.Value.ToString(
                            System.Globalization.CultureInfo.InvariantCulture))) + "]";
                    return new VoicingFragmentRow(ascii, v.Structure, (int)Math.Round(v.Comfort * 100));
                })
                .ToList();

            return Results.RazorSlice<VoicingFragment, VoicingFragmentModel>(
                new VoicingFragmentModel(chord, resolvedTuning.Name, rows, null));
        });

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
            var tuningLabel = ResolveLabel(form["tuningName"].ToString(), tuning, catalogs);
            var limit       = int.TryParse(form["limit"],      out var l)  ? l  : 200;
            var topN        = int.TryParse(form["topN"],       out var n)  ? n  : 0;
            var entropy     = double.TryParse(form["entropy"],
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var e) ? e : 0.5;
            var minComfortPct = int.TryParse(form["minComfort"], out var mc) ? Math.Clamp(mc, 0, 100) : 0;
            var minComfort  = minComfortPct / 100.0;

            var searchParams = BuildSearchParams(key => form[key]);

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
            var set  = VoicingSearch.Search(spec, resolvedTuning, HandModel.Default, searchParams, catalogs.VoicingCategories);

            var voicings = set.Voicings;
            if (minComfort > 0.0)
                voicings = voicings.Where(v => v.Comfort >= minComfort).ToImmutableArray();
            var total    = voicings.Length;
            if (limit > 0 && voicings.Length > limit)
                voicings = voicings[..limit];

            var rows = voicings
                .Select((v, i) =>
                {
                    var ordered = v.Positions.OrderBy(p => p.String).ToList();
                    var notes = string.Join(" ", ordered.Select(p =>
                        p.Muted ? "x" : p.DisplayNote!.Value.ToString()));
                    var intervals = string.Join(" ", ordered.Select(p =>
                        p.Muted ? "x" : FunctionToRoman(p.Function!)));
                    var diagram = BuildDiagram(v);
                    return new VoicingRow(
                        Index: i + 1,
                        Structure: v.Structure,
                        ComfortPct: (int)Math.Round(v.Comfort * 100),
                        AsciiNotation: BuildAsciiNotation(diagram.Strings),
                        Notes: notes,
                        Intervals: intervals,
                        Diagram: diagram);
                })
                .ToList();

            var nextChords = NextChordPredictor.Predict(spec, topN, entropy)
                .Select(c => new NextChordEntry(c.Symbol, c.Probability))
                .ToList();

            var notice = spec.Bass is { } bass && set.Voicings.IsEmpty
                ? $"No playable voicing on {resolvedTuning.Name} places {bass} in the bass. "
                  + "Try widening the search, or drop the slash bass."
                : null;

            return Results.RazorSlice<VoicingsResult, VoicingResultModel>(
                new VoicingResultModel(chord, tuningLabel, total, rows, nextChords, null, notice, tuning));
        });
    }

    /// <summary>
    /// Returns a human-readable label for a tuning value.
    /// Falls back to the catalog name (or the raw value) when the JS-supplied label
    /// is blank or the literal string "undefined" (wa-select not yet upgraded on submit).
    /// </summary>
    public static string ResolveLabel(string? formLabel, string tuningValue, Catalogs catalogs)
    {
        var label = formLabel?.Trim();
        if (!string.IsNullOrEmpty(label) && label != "undefined")
            return label;
        return catalogs.Tunings.TryGet(tuningValue, out var named) ? named!.Name : tuningValue;
    }

    public static Dadabe.Core.Tuning ResolveTuningPublic(Catalogs catalogs, string nameOrSpec, string displayName)
        => ResolveTuning(catalogs, nameOrSpec, displayName);

    /// <summary>
    /// Parses the shared "Voicing options" panel fields (see
    /// <c>VoicingOptionsFields.cshtml</c>) into a <see cref="SearchParams"/>,
    /// falling back to <see cref="SearchParams.Default"/> for any field the
    /// caller's request doesn't carry. <paramref name="get"/> abstracts over
    /// <c>IFormCollection</c> and <c>IQueryCollection</c>, which both expose
    /// the same string-keyed indexer.
    /// </summary>
    public static SearchParams BuildSearchParams(Func<string, StringValues> get)
    {
        var d = SearchParams.Default;
        var frets      = int.TryParse(get("frets"),      out var fr) ? fr : d.MaxFret;
        var span       = int.TryParse(get("span"),       out var sp) ? sp : d.MaxSpan;
        var minStr     = int.TryParse(get("minStrings"), out var mn) ? mn : d.MinStrings;
        var maxStr     = int.TryParse(get("maxStrings"), out var mx) ? mx : d.MaxStrings;
        var allowOpen  = get("allowOpen").Contains("true");
        var allowBarre = get("allowBarre").Contains("true");
        var allowThumb = get("allowThumb").Contains("true");
        var requireRoot = get("requireRoot").Contains("true");

        var checkedCats = AllCategories
            .Where(cat => get("cat_" + cat.Replace("-", "")).Contains(cat))
            .ToImmutableArray();
        // If all are checked (or none explicitly unchecked), pass empty = no filter.
        var categories = checkedCats.Length == AllCategories.Length
            ? ImmutableArray<string>.Empty
            : checkedCats;

        return new SearchParams(frets, span, minStr, maxStr, allowOpen, allowBarre, allowThumb, categories, requireRoot);
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

    private static string FunctionToRoman(string function)
    {
        // A foreign slash bass has no scale-degree reading. Handled up front:
        // the accidental-stripping loop below would otherwise treat the leading
        // 'b' as a flat and emit "b" + "ass".
        if (function == Dadabe.Core.Chord.ChordSpec.BassFunction) { return "bass"; }

        var i = 0;
        while (i < function.Length && (function[i] == 'b' || function[i] == '#')) i++;
        var prefix = function[..i];
        var roman = function[i..] switch
        {
            "1"       => "I",
            "2" or "9"  => "II",
            "3"       => "III",
            "4" or "11" => "IV",
            "5"       => "V",
            "6" or "13" => "VI",
            "7"       => "VII",
            var other => other,
        };
        return prefix + roman;
    }

    private static string BuildAsciiNotation(IReadOnlyList<ChordDiagramString> strings)
    {
        var parts = strings.Select(s => s.Muted ? "x" : s.Open ? "0" : s.FrettedAt!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return $"[{string.Join(" ", parts)}]";
    }

    private static ChordDiagram BuildDiagram(Voicing voicing) => ChordDiagramBuilder.FromVoicing(voicing);
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
