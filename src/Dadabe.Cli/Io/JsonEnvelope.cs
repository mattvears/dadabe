using System.Text.Json;
using System.Text.Json.Serialization;
using Dadabe.Fretboard;

namespace Dadabe.Cli.Io;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Envelope<VoicingsPayload>))]
[JsonSerializable(typeof(Envelope<TuningPayload>))]
[JsonSerializable(typeof(Envelope<ChordPayload>))]
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
    public const string ToolVersion = "0.1.0";

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
