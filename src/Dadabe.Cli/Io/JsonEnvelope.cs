using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dadabe.Fretboard;
using Json.Schema;

namespace Dadabe.Cli.Io;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Envelope<VoicingsPayload>))]
[JsonSerializable(typeof(Envelope<TuningPayload>))]
[JsonSerializable(typeof(Envelope<ChordPayload>))]
[JsonSerializable(typeof(Envelope<PredictionResultDto>))]
public partial class DadabeJsonContext : JsonSerializerContext
{
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Envelope<VoicingsPayload>))]
[JsonSerializable(typeof(Envelope<TuningPayload>))]
[JsonSerializable(typeof(Envelope<ChordPayload>))]
[JsonSerializable(typeof(Envelope<PredictionResultDto>))]
public partial class DadabeJsonContextPretty : JsonSerializerContext
{
}

/// <summary>
/// Writes JSON envelopes to <see cref="OutputTarget"/>: stdout or a file,
/// indented or not (D22). The only consumer of <see cref="OutputTarget"/>.
/// </summary>
public static class JsonEnvelope
{
    /// <summary>Tool semver (envelope <c>version</c>).</summary>
    public const string ToolVersion = "0.4.0";

    /// <summary>JSON contract version (envelope <c>schemaVersion</c>, D13).</summary>
    public const string SchemaVersion = "1";

    public const string ToolName = "dadabe";

    public static InputDto BuildInput(
        HandModel hand,
        string? chord = null,
        string? tuning = null)
    {
        var stretch = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in EnumeratePairs())
        {
            stretch[$"{(int)pair.Low}-{(int)pair.High}"] = hand.StretchBetween(pair.Low, pair.High);
        }
        var dto = new HandModelDto(
            Name: hand.Name,
            Stretch: stretch,
            Thumb: new ThumbDto(hand.Thumb.Allowed, hand.Thumb.MaxFret, hand.Thumb.String),
            MaxBarres: hand.MaxBarres);
        return new InputDto(chord, tuning, dto);
    }

    public static void Write<T>(Envelope<T> envelope, OutputTarget target)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(target);
        var info = ResolveTypeInfo<T>(target.Pretty);
        if (target.FilePath is null)
        {
            using var stdout = Console.OpenStandardOutput();
            JsonSerializer.Serialize(stdout, envelope, info);
            // Trailing newline so terminal prompts land on their own line.
            stdout.Write("\n"u8);
        }
        else
        {
            using var file = File.Create(target.FilePath);
            JsonSerializer.Serialize(file, envelope, info);
        }
    }

    public static string Serialize<T>(Envelope<T> envelope, bool pretty)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var info = ResolveTypeInfo<T>(pretty);
        return JsonSerializer.Serialize(envelope, info);
    }

    /// <summary>
    /// Validates the written envelope JSON against the appropriate schema (D25).
    /// Emits all errors to stderr. Throws <see cref="SchemaViolationException"/> if any are found.
    /// </summary>
    public static void ValidateSchema<T>(Envelope<T> envelope, string command, string? workingDirectory, OutputTarget output)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(command);

        var json = output.FilePath is not null
            ? File.ReadAllText(output.FilePath)
            : Serialize(envelope, output.Pretty);

        var schemaFile = command == "predict" ? "prediction-result.schema.json" : "envelope.schema.json";
        var schema = LoadSchema(schemaFile, workingDirectory);

        var node = JsonNode.Parse(json);
        var opts = new EvaluationOptions { OutputFormat = OutputFormat.List };
        var result = schema.Evaluate(node, opts);

        if (!result.IsValid)
        {
            foreach (var detail in result.Details)
            {
                if (detail.HasErrors && detail.Errors is not null)
                {
                    foreach (var (key, msg) in detail.Errors)
                    {
                        Console.Error.WriteLine($"{detail.EvaluationPath} -> {key}: {msg}");
                    }
                }
            }
            throw new SchemaViolationException();
        }
    }

    private static JsonSchema LoadSchema(string schemaFile, string? workingDirectory)
    {
        if (workingDirectory is not null)
        {
            var schemasDir = Path.Combine(workingDirectory, "schemas");
            if (Directory.Exists(schemasDir))
            {
                // Pre-register all schemas so $ref resolution works during evaluation.
                foreach (var path in Directory.GetFiles(schemasDir, "*.json"))
                {
                    JsonSchema.FromFile(path);
                }
                var mainPath = Path.Combine(schemasDir, schemaFile);
                if (File.Exists(mainPath))
                {
                    return JsonSchema.FromFile(mainPath);
                }
            }
        }
        // Fall back to embedded resource.
        var asm = typeof(JsonEnvelope).Assembly;
        var resourceName = $"Dadabe.Cli.schemas.{schemaFile}";
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Schema '{schemaFile}' not found in '{workingDirectory}/schemas/' or as embedded resource '{resourceName}'.");
        using var reader = new System.IO.StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    private static System.Text.Json.Serialization.Metadata.JsonTypeInfo<Envelope<T>> ResolveTypeInfo<T>(bool pretty)
    {
        var ctx = pretty
            ? (JsonSerializerContext)DadabeJsonContextPretty.Default
            : DadabeJsonContext.Default;
        var ti = ctx.GetTypeInfo(typeof(Envelope<T>))
            ?? throw new InvalidOperationException($"No JsonTypeInfo for Envelope<{typeof(T).Name}>.");
        return (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Envelope<T>>)ti;
    }

    private static IEnumerable<FingerPair> EnumeratePairs()
    {
        yield return new FingerPair(Finger.Index, Finger.Middle);
        yield return new FingerPair(Finger.Index, Finger.Ring);
        yield return new FingerPair(Finger.Index, Finger.Pinky);
        yield return new FingerPair(Finger.Middle, Finger.Ring);
        yield return new FingerPair(Finger.Middle, Finger.Pinky);
        yield return new FingerPair(Finger.Ring, Finger.Pinky);
    }
}

/// <summary>Thrown when --validate-schema detects a schema violation (exit code 3).</summary>
public sealed class SchemaViolationException : Exception
{
    public SchemaViolationException() : base("Output JSON failed schema validation.") { }
}
