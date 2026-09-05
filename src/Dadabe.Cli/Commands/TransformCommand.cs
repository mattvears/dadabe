using Dadabe.Cli.Io;
using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using Dadabe.Fretboard;
using Environment = Dadabe.Fretboard.Environment;

namespace Dadabe.Cli.Commands;

/// <summary>
/// <c>dadabe transform --chords "C F G" --chain "retrograde,transpose:M2" [--all] [options]</c>:
/// applies a transform chain (or the whole catalogue, ranked) to a chord
/// progression and reports playability on the active tuning (D50, D51).
/// </summary>
public static class TransformCommand
{
    public static void Run(
        Environment env,
        string? chordsRaw,
        string? progressionSlug,
        string? chainRaw,
        bool all,
        string tuningName,
        double minComfort,
        bool validateSchema)
    {
        ArgumentNullException.ThrowIfNull(env);

        var source = ResolveChords(env, chordsRaw, progressionSlug);
        var parser = new ChordParser(env.Catalogs.ChordGrammar);
        var expander = new ChordExpander(env.Catalogs.ChordGrammar);
        var catalog = TransformCatalog.CreateDefault(env.Catalogs.ChordGrammar);
        var tuning = VoicingsCommand.ResolveTuning(env, tuningName);

        var chains = all
            ? catalog.Select(t => (IReadOnlyList<TransformStep>)[new TransformStep(t.Id, DefaultParamsForCatalogueRun(t.Id))]).ToList()
            : [ParseChain(chainRaw ?? throw new FormatException("--chain is required unless --all is set."))];

        var variants = chains
            .Select(chain => BuildVariant(catalog, parser, source, chain, tuning, env, minComfort))
            .ToList();

        if (all)
        {
            // Unplayable-count ascending, worst-comfort descending, total-distance ascending (D51).
            variants = variants
                .OrderBy(v => (v.Playability?.Unplayable.Count) ?? v.Chords.Count)
                .ThenByDescending(v => v.Playability?.WorstComfort ?? 0)
                .ThenBy(v => v.Playability?.TotalDistance ?? int.MaxValue)
                .ToList();
        }

        var payload = new TransformResultDto(source, variants);

        var envelope = new Envelope<TransformResultDto>(
            Tool: JsonEnvelope.ToolName,
            Version: JsonEnvelope.ToolVersion,
            SchemaVersion: JsonEnvelope.SchemaVersion,
            Command: "transform",
            Input: new InputDto(Chord: chordsRaw, Tuning: tuningName, HandModel: null),
            Data: payload,
            Warnings: Array.Empty<string>());

        JsonEnvelope.Write(envelope, env.Output);

        if (validateSchema)
        {
            JsonEnvelope.ValidateSchema(envelope, "transform", env.WorkingDirectory, env.Output);
        }
    }

    private static string[] ResolveChords(Environment env, string? chordsRaw, string? progressionSlug)
    {
        if (!string.IsNullOrWhiteSpace(chordsRaw))
        {
            return chordsRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        if (!string.IsNullOrWhiteSpace(progressionSlug))
        {
            throw new FormatException(
                $"--progression '{progressionSlug}' cannot be resolved from the CLI in this release; use --chords.");
        }
        throw new FormatException("--chords or --progression is required.");
    }

    /// <summary>Parses "type,type:arg,type:k=v;k=v" into a step list.</summary>
    internal static IReadOnlyList<TransformStep> ParseChain(string chainRaw)
    {
        var steps = new List<TransformStep>();
        foreach (var raw in chainRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIndex = raw.IndexOf(':', StringComparison.Ordinal);
            if (colonIndex < 0)
            {
                steps.Add(new TransformStep(raw, new Dictionary<string, object>()));
                continue;
            }

            var type = raw[..colonIndex];
            var argText = raw[(colonIndex + 1)..];
            var parameters = new Dictionary<string, object>(StringComparer.Ordinal);

            if (argText.Contains('=', StringComparison.Ordinal))
            {
                foreach (var pair in argText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var eq = pair.IndexOf('=', StringComparison.Ordinal);
                    if (eq < 0)
                    {
                        throw new FormatException($"Malformed transform argument '{pair}' in step '{raw}'.");
                    }
                    parameters[pair[..eq]] = pair[(eq + 1)..];
                }
            }
            else
            {
                // Single positional argument: transpose:M2, rotate:2, quality-map:m7.
                parameters[SingleArgKey(type)] = argText;
            }

            steps.Add(new TransformStep(type, parameters));
        }
        return steps;
    }

    /// <summary>
    /// Sensible defaults for transforms that require a parameter, so <c>--all</c>
    /// (D51 §5, "discovery mode") can run the whole catalogue without prompting
    /// per-transform. Parameter-free transforms return an empty bag.
    /// </summary>
    private static Dictionary<string, object> DefaultParamsForCatalogueRun(string id) => id switch
    {
        "transpose" => new() { ["interval"] = "M2" },
        "invert" => new() { ["axis"] = "C" },
        "quality-map" => new() { ["to"] = "m7" },
        "plr" => new() { ["op"] = "P" },
        "parallel-mode" => new() { ["mode"] = "mixolydian" },
        _ => new(),
    };

    private static string SingleArgKey(string type) => type switch
    {
        "transpose" => "interval",
        "rotate" => "by",
        "invert" => "axis",
        "quality-map" => "to",
        "reduce" => "level",
        "tritone-sub" => "positions",
        "plr" => "op",
        "interval-multiply" => "factor",
        "diatonic-transpose" => "by",
        "parallel-mode" => "mode",
        "substitute" => "variant",
        "negative-harmony" => "axis",
        _ => throw new FormatException($"Transform '{type}' does not accept a single positional argument; use k=v."),
    };

    private static TransformVariantDto BuildVariant(
        TransformCatalog catalog,
        ChordParser parser,
        IReadOnlyList<string> source,
        IReadOnlyList<TransformStep> chain,
        Dadabe.Core.Tuning tuning,
        Environment env,
        double minComfort)
    {
        var result = TransformChain.Apply(catalog, parser, source, chain);

        PlayabilityDto? playability = null;
        if (result.Chords.Count > 0)
        {
            var score = PlayabilityRanking.Score(
                result.Chords, tuning, env.HandModel, SearchParams.Default,
                env.Catalogs.VoicingCategories, parser, new ChordExpander(env.Catalogs.ChordGrammar), minComfort);
            playability = new PlayabilityDto(score.WorstComfortPct, score.TotalDistance, score.MinFret, score.MaxFret, score.Unplayable);
        }

        var notes = result.Notes.Select(n => new TransformNoteDto(n.Index, n.Kind, n.Message)).ToList();
        var chainDtos = chain
            .Select(s => new TransformStepDto(s.Type, s.Params.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty, StringComparer.Ordinal)))
            .ToList();

        return new TransformVariantDto(chainDtos, result.Chords.ToList(), playability, KeyBefore: null, KeyAfter: null, notes);
    }
}
