using System.Net;
using Dadabe.Core;
using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;
using Dadabe.Fretboard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dadabe.Editor.Routes;

/// <summary>
/// The transform panel (v0.5.2 §7): chain builder, diff preview, playability
/// and key annotations, apply-all "Explore", and save/replace actions with
/// <see cref="DerivedFrom"/> provenance.
/// </summary>
public static class TransformRoutes
{
    public static void MapTransformRoutes(this WebApplication app)
    {
        app.MapGet("/api/progressions/{slug}/transform", (string slug, [FromServices] ProgressionService progressions) =>
        {
            var progression = progressions.Get(slug);
            return progression is null
                ? Results.NotFound()
                : Results.RazorSlice<TransformPanel, TransformPanelModel>(new TransformPanelModel(progression));
        });

        app.MapPost("/api/progressions/{slug}/transform/apply", async (string slug, HttpRequest req,
            [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs,
            [FromServices] ChordParser parser, [FromServices] ChordExpander expander) =>
        {
            var progression = progressions.Get(slug);
            if (progression is null) { return Results.NotFound(); }

            var form = await req.ReadFormAsync();
            var chainInputs = ReadChainInputs(form);
            var model = RunTransform(progression, chainInputs, form["tuning"].ToString(), form["tuningName"].ToString(),
                catalogs, parser, expander);
            return Results.RazorSlice<Dadabe.Editor.Slices.TransformResult, TransformResultViewModel>(model);
        });

        app.MapPost("/api/progressions/{slug}/transform/explore", async (string slug, HttpRequest req,
            [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs,
            [FromServices] ChordParser parser, [FromServices] ChordExpander expander) =>
        {
            var progression = progressions.Get(slug);
            if (progression is null) { return Results.NotFound(); }

            var form = await req.ReadFormAsync();
            var model = RunExplore(progression, form["tuning"].ToString(), form["tuningName"].ToString(), catalogs, parser, expander);
            return Results.RazorSlice<TransformExplore, TransformExploreModel>(model);
        });

        app.MapPost("/api/progressions/{slug}/transform/save", async (string slug, HttpRequest req,
            [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser) =>
        {
            var progression = progressions.Get(slug);
            if (progression is null) { return Results.NotFound(); }

            var form = await req.ReadFormAsync();
            var name = form["name"].ToString();
            var (ok, error, chords, derivedFrom) = ApplyChainForCommit(progression, ReadChainInputs(form), catalogs, parser);
            if (!ok) { return ErrorFragment(error!); }

            var (createOk, createError) = progressions.Create(name, chords!, progression.Tempo, derivedFrom);
            return createOk ? SuccessFragment($"Saved as '{name}'.") : ErrorFragment(createError);
        });

        app.MapPost("/api/progressions/{slug}/transform/replace", async (string slug, HttpRequest req,
            [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser) =>
        {
            var progression = progressions.Get(slug);
            if (progression is null) { return Results.NotFound(); }

            var form = await req.ReadFormAsync();
            var (ok, error, chords, derivedFrom) = ApplyChainForCommit(progression, ReadChainInputs(form), catalogs, parser);
            if (!ok) { return ErrorFragment(error!); }

            var (updateOk, updateError) = progressions.Update(progression.Slug, progression.Name, chords!, progression.Tempo, derivedFrom);
            return updateOk ? SuccessFragment($"Replaced '{progression.Name}'.") : ErrorFragment(updateError);
        });
    }

    // ── helpers ──

    private static List<(string Type, string ParamsRaw)> ReadChainInputs(IFormCollection form)
    {
        var types = form["type"];
        var paramsRaw = form["paramsRaw"];
        var result = new List<(string, string)>();
        for (var i = 0; i < types.Count; i++)
        {
            var type = types[i]?.Trim();
            if (string.IsNullOrWhiteSpace(type)) { continue; }
            result.Add((type, i < paramsRaw.Count ? paramsRaw[i] ?? string.Empty : string.Empty));
        }
        return result;
    }

    private static Dictionary<string, object> ParseParams(string raw)
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw)) { return dict; }
        foreach (var pair in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
            if (eq < 0) { throw new FormatException($"Malformed parameter '{pair}' — expected key=value."); }
            dict[pair[..eq]] = pair[(eq + 1)..];
        }
        return dict;
    }

    private static TransformResultViewModel RunTransform(
        ProgressionModel progression, List<(string Type, string ParamsRaw)> chainInputs,
        string tuningName, string tuningLabel, Catalogs catalogs, ChordParser parser, ChordExpander expander)
    {
        if (chainInputs.Count == 0)
        {
            return new TransformResultViewModel(progression.Slug, [], null, null, null, [], chainInputs, "Add at least one transform step.");
        }

        var catalog = TransformCatalog.CreateDefault(catalogs.ChordGrammar);
        Dadabe.Core.Transform.TransformResult result;
        try
        {
            var steps = chainInputs.Select(c => new TransformStep(c.Type, ParseParams(c.ParamsRaw))).ToList();
            result = TransformChain.Apply(catalog, parser, progression.Chords, steps);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or KeyNotFoundException)
        {
            return new TransformResultViewModel(progression.Slug, [], null, null, null, [], chainInputs, ex.Message);
        }

        var diff = new List<DiffChordRow>();
        var maxLen = Math.Max(progression.Chords.Count, result.Chords.Count);
        for (var i = 0; i < maxLen; i++)
        {
            var original = i < progression.Chords.Count ? progression.Chords[i] : "";
            var transformed = i < result.Chords.Count ? result.Chords[i] : "";
            diff.Add(new DiffChordRow(i, original, transformed, !string.Equals(original, transformed, StringComparison.Ordinal)));
        }

        var keyBefore = KeyDisplay(progression.Chords, parser, expander);
        var keyAfter = KeyDisplay(result.Chords, parser, expander);
        var playability = TryScorePlayability(result.Chords, tuningName, tuningLabel, catalogs, parser, expander);
        var notes = result.Notes.Select(n => new TransformNoteRow(n.Index, n.Kind, n.Message)).ToList();

        return new TransformResultViewModel(progression.Slug, diff, keyBefore, keyAfter, playability, notes, chainInputs, null);
    }

    private static TransformExploreModel RunExplore(
        ProgressionModel progression, string tuningName, string tuningLabel,
        Catalogs catalogs, ChordParser parser, ChordExpander expander)
    {
        var catalog = TransformCatalog.CreateDefault(catalogs.ChordGrammar);
        var chords = progression.Chords.Select(parser.Parse).ToArray();
        var rows = new List<TransformExploreRow>();

        foreach (var transform in catalog)
        {
            Dadabe.Core.Transform.TransformResult result;
            try { result = transform.Apply(chords, DefaultParamsForExplore(transform.Id)); }
            catch (Exception ex) when (ex is FormatException or ArgumentException) { continue; }

            var playability = TryScorePlayability(result.Chords, tuningName, tuningLabel, catalogs, parser, expander);
            rows.Add(new TransformExploreRow(transform.Id, transform.DisplayName, result.Chords, playability, result.Notes.Count));
        }

        var ranked = rows
            .OrderBy(r => r.Playability?.Unplayable.Count ?? 0)
            .ThenByDescending(r => r.Playability?.WorstComfortPct ?? 0)
            .ThenBy(r => r.Playability?.TotalDistance ?? int.MaxValue)
            .ToList();

        return new TransformExploreModel(progression.Slug, ranked);
    }

    /// <summary>Re-applies a chain for a commit action (save/replace), producing the final chord list plus provenance.</summary>
    private static (bool Ok, string? Error, List<string>? Chords, DerivedFrom? DerivedFrom) ApplyChainForCommit(
        ProgressionModel progression, List<(string Type, string ParamsRaw)> chainInputs, Catalogs catalogs, ChordParser parser)
    {
        if (chainInputs.Count == 0) { return (false, "No transform chain to commit.", null, null); }

        var catalog = TransformCatalog.CreateDefault(catalogs.ChordGrammar);
        try
        {
            var steps = chainInputs.Select(c => new TransformStep(c.Type, ParseParams(c.ParamsRaw))).ToList();
            var result = TransformChain.Apply(catalog, parser, progression.Chords, steps);
            var derivedFrom = new DerivedFrom(
                progression.Slug,
                steps.Select(s => new TransformStepDto(
                    s.Type, s.Params.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty, StringComparer.Ordinal))).ToList());
            return (true, null, result.Chords.ToList(), derivedFrom);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or KeyNotFoundException)
        {
            return (false, ex.Message, null, null);
        }
    }

    private static PlayabilityViewModel? TryScorePlayability(
        IReadOnlyList<string> chords, string tuningName, string tuningLabel,
        Catalogs catalogs, ChordParser parser, ChordExpander expander)
    {
        if (string.IsNullOrWhiteSpace(tuningName) || chords.Count == 0) { return null; }
        try
        {
            var tuning = VoicingRoutes.ResolveTuningPublic(catalogs, tuningName, tuningLabel);
            var score = PlayabilityRanking.Score(
                chords, tuning, HandModel.Default, SearchParams.Default, catalogs.VoicingCategories, parser, expander);
            return new PlayabilityViewModel(score.WorstComfortPct, score.TotalDistance, score.MinFret, score.MaxFret, score.Unplayable);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Key inference is display-only (D39) — nothing branches on it.</summary>
    private static string KeyDisplay(IReadOnlyList<string> chords, ChordParser parser, ChordExpander expander)
    {
        var specs = new List<ChordSpec>();
        foreach (var symbol in chords)
        {
            if (parser.TryParse(symbol, out var chord, out _)) { specs.Add(expander.Expand(chord)); }
        }
        var inferred = KeyInference.InferKey(specs);
        if (inferred is null) { return "no clear key centre"; }
        var note = Note.Spell(inferred.Value.RootPc, Letter.C);
        return $"{note} {(inferred.Value.IsMinor ? "minor" : "major")}";
    }

    /// <summary>Sensible defaults for transforms that require a parameter, so Explore can run the whole catalogue unattended.</summary>
    private static Dictionary<string, object> DefaultParamsForExplore(string id) => id switch
    {
        "transpose" => new() { ["interval"] = "M2" },
        "invert" => new() { ["axis"] = "C" },
        "quality-map" => new() { ["to"] = "m7" },
        "plr" => new() { ["op"] = "P" },
        _ => new(),
    };

    /// <summary>Raw "k=v;k=v" params matching <see cref="DefaultParamsForExplore"/>, for the Explore row's "Use" button.</summary>
    internal static string DefaultParamsRawFor(string id) => id switch
    {
        "transpose" => "interval=M2",
        "invert" => "axis=C",
        "quality-map" => "to=m7",
        "plr" => "op=P",
        _ => "",
    };

    private static IResult ErrorFragment(string? message) =>
        Results.Content($"""<p style="color:var(--wa-color-danger-600)">{WebUtility.HtmlEncode(message)}</p>""", "text/html");

    private static IResult SuccessFragment(string message) =>
        Results.Content(
            $"""<p style="color:var(--wa-color-success-600)">{WebUtility.HtmlEncode(message)}</p>""" +
            """<div hx-get="/progressions" hx-select="#progression-list" hx-target="#progression-list" hx-swap="innerHTML" hx-trigger="load" style="display:none"></div>""",
            "text/html");
}
