using System.Text.Json;
using System.Text.Json.Nodes;
using Dadabe.Cli.Io;
using Dadabe.Core.Chord;
using Environment = Dadabe.Fretboard.Environment;

namespace Dadabe.Cli.Commands;

/// <summary>
/// <c>dadabe predict --input &lt;file.json&gt;</c>: reads a prediction request,
/// runs <see cref="NextChordPredictor"/>, applies hard filters, and writes a
/// schema-valid <see cref="Envelope{PredictionResultDto}"/>.
/// </summary>
public static class PredictCommand
{
    private static readonly JsonSerializerOptions RequestOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static void Run(
        Environment env,
        string inputFilePath,
        double entropy,
        bool validateSchema)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(inputFilePath);

        // 1. Deserialize request.
        var requestJson = File.ReadAllText(inputFilePath);
        var request = JsonSerializer.Deserialize<NextChordPredictionRequestDto>(requestJson, RequestOptions)
            ?? throw new FormatException("Prediction request file is empty or invalid JSON.");

        if (string.IsNullOrWhiteSpace(request.Chord))
        {
            throw new FormatException("Prediction request must include a non-empty 'chord' field.");
        }

        // 2. Parse and expand chord.
        var parser = new ChordParser(env.Catalogs.ChordGrammar);
        var expander = new ChordExpander(env.Catalogs.ChordGrammar);
        var symbol = parser.Parse(request.Chord);
        var spec = expander.Expand(symbol);

        // 3. Predict — topN before filters, entropy overridable per-request.
        var topN = request.MaxResults ?? 10;
        var effectiveEntropy = request.Entropy ?? entropy;
        var candidates = NextChordPredictor.Predict(spec, topN, effectiveEntropy);

        // 4. Apply filters in declaration order.
        var warnings = new List<string>();
        var filtered = ApplyFilters(candidates, request.Filters, warnings);

        // 5. Map candidates → DTOs.
        var results = filtered
            .Select(c => new PredictionCandidateDto(c.Symbol, c.Probability, Reasons: null))
            .ToList();

        // 6. Build metadata.
        PredictionMetadataDto? metadata = BuildMetadata(request);

        var payload = new PredictionResultDto(results, metadata);

        // 7. Write envelope.
        var envelope = new Envelope<PredictionResultDto>(
            Tool: JsonEnvelope.ToolName,
            Version: JsonEnvelope.ToolVersion,
            SchemaVersion: JsonEnvelope.SchemaVersion,
            Command: "predict",
            Input: new InputDto(Chord: request.Chord, Tuning: null, HandModel: null),
            Data: payload,
            Warnings: warnings);

        JsonEnvelope.Write(envelope, env.Output);

        // 8. Optional schema validation.
        if (validateSchema)
        {
            JsonEnvelope.ValidateSchema(envelope, "predict", env.WorkingDirectory, env.Output);
        }
    }

    private static IReadOnlyList<NextChordCandidate> ApplyFilters(
        IReadOnlyList<NextChordCandidate> candidates,
        PredictionFilterDto[]? filters,
        List<string> warnings)
    {
        if (filters is null || filters.Length == 0) { return candidates; }

        var result = candidates.ToList();
        foreach (var filter in filters)
        {
            switch (filter.Type)
            {
                case "byChord":
                    var targetChord = GetStringParam(filter, "chord");
                    if (targetChord is not null)
                    {
                        result = result.Where(c =>
                            string.Equals(c.Symbol, targetChord, StringComparison.Ordinal)).ToList();
                    }
                    break;

                case "byQuality":
                    var quality = GetStringParam(filter, "quality");
                    if (quality is not null)
                    {
                        result = result.Where(c => ExtractSuffix(c.Symbol) == quality).ToList();
                    }
                    break;

                case "byScale":
                case "byMode":
                case "custom":
                    warnings.Add($"Filter type '{filter.Type}' is not implemented in v0.4; it was ignored.");
                    break;

                default:
                    warnings.Add($"Unknown filter type '{filter.Type}'; it was ignored.");
                    break;
            }
        }
        return result;
    }

    private static PredictionMetadataDto? BuildMetadata(NextChordPredictionRequestDto request)
    {
        var hasContext = request.Context is not null;
        var hasFilters = request.Filters is { Length: > 0 };
        if (!hasContext && !hasFilters) { return null; }

        JsonObject? filtersApplied = null;
        if (hasFilters)
        {
            var node = new JsonObject();
            foreach (var f in request.Filters!)
            {
                if (f.Type is not ("byScale" or "byMode" or "custom"))
                {
                    node[f.Type] = BuildParamsNode(f.Params);
                }
            }
            if (node.Count > 0) { filtersApplied = node; }
        }

        return new PredictionMetadataDto(
            ContextUsed: request.Context,
            FiltersApplied: filtersApplied);
    }

    private static JsonObject? BuildParamsNode(Dictionary<string, object>? params_)
    {
        if (params_ is null) { return null; }
        var node = new JsonObject();
        foreach (var kvp in params_)
        {
            node[kvp.Key] = kvp.Value switch
            {
                JsonElement je => JsonNode.Parse(je.GetRawText()),
                string s => JsonValue.Create(s),
                _ => JsonValue.Create(kvp.Value.ToString()),
            };
        }
        return node;
    }

    /// <summary>
    /// Strips the leading root (letter + optional accidental) from a chord symbol
    /// to get the quality suffix. E.g. "Gm7" → "m7", "Bb" → "", "F#maj7" → "maj7".
    /// </summary>
    private static string ExtractSuffix(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) { return string.Empty; }
        var i = 1;
        while (i < symbol.Length && symbol[i] is 'b' or '#') { i++; }
        return symbol[i..];
    }

    private static string? GetStringParam(PredictionFilterDto filter, string key)
    {
        if (filter.Params?.TryGetValue(key, out var val) != true) { return null; }
        return val switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            _ => val?.ToString(),
        };
    }
}
