using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dadabe.Core;

/// <summary>
/// Named-tuning catalog (D3, D22). Loaded from the embedded
/// <c>Tunings.json</c> and overlaid with a same-named file from a working
/// directory if present. Overlay wins on name collision; otherwise unions.
/// </summary>
public sealed class TuningCatalog
{
    public const string FileName = "Tunings.json";
    private const string EmbeddedResourceName = "Dadabe.Core.Tunings.json";

    public ImmutableDictionary<string, Tuning> ByName { get; }

    private TuningCatalog(ImmutableDictionary<string, Tuning> byName)
    {
        ByName = byName;
    }

    public IEnumerable<Tuning> All => ByName.Values;

    public Tuning Get(string name)
    {
        if (ByName.TryGetValue(name, out var tuning))
        {
            return tuning;
        }
        throw new KeyNotFoundException($"No tuning named '{name}' in catalog.");
    }

    public bool TryGet(string name, out Tuning? tuning) => ByName.TryGetValue(name, out tuning);

    /// <summary>
    /// Load the catalog: embedded defaults overlaid with
    /// <c>Tunings.json</c> in <paramref name="workingDirectory"/> if it
    /// exists. Pass <c>null</c> to skip overlays.
    /// </summary>
    public static TuningCatalog Load(string? workingDirectory)
    {
        var embedded = LoadEmbedded();
        var merged = embedded.ToDictionary(t => t.Name, StringComparer.Ordinal);
        if (workingDirectory is not null)
        {
            var overlayPath = Path.Combine(workingDirectory, FileName);
            if (File.Exists(overlayPath))
            {
                foreach (var t in LoadFromFile(overlayPath))
                {
                    merged[t.Name] = t;
                }
            }
        }
        return new TuningCatalog(merged.ToImmutableDictionary(StringComparer.Ordinal));
    }

    private static List<Tuning> LoadEmbedded()
    {
        var asm = typeof(TuningCatalog).Assembly;
        using var stream = asm.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' missing. Resources present: "
                + string.Join(", ", asm.GetManifestResourceNames()));
        return Parse(stream);
    }

    private static List<Tuning> LoadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream);
    }

    private static List<Tuning> Parse(Stream stream)
    {
        var dto = JsonSerializer.Deserialize<TuningsFileDto>(stream, SerializerOptions)
            ?? throw new InvalidDataException("Tunings.json is empty or malformed.");
        if (dto.Tunings is null)
        {
            return new List<Tuning>();
        }
        return dto.Tunings
            .Select(t => new Tuning(
                t.Name ?? throw new InvalidDataException("Tuning entry missing 'name'."),
                (t.Strings ?? throw new InvalidDataException($"Tuning '{t.Name}' missing 'strings'."))
                    .Select(Pitch.Parse)))
            .ToList();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class TuningsFileDto
    {
        [JsonPropertyName("schemaVersion")]
        public string? SchemaVersion { get; set; }

        [JsonPropertyName("tunings")]
        public List<TuningDto>? Tunings { get; set; }
    }

    private sealed class TuningDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("strings")]
        public List<string>? Strings { get; set; }
    }
}
