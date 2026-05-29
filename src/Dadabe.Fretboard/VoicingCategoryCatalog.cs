using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dadabe.Fretboard;

/// <summary>
/// A function class: a set of chord-tone function names that any of which
/// satisfies the class. Built from a pipe-delimited string (e.g.
/// <c>"3|b3"</c>) or by name reference into
/// <see cref="VoicingCategoryCatalog.FunctionClasses"/>.
/// </summary>
public sealed record FunctionClass(ImmutableHashSet<string> Functions)
{
    public bool Contains(string function) => Functions.Contains(function);
}

/// <summary>A single match-entry with its bundle of rules. All rules must hold.</summary>
public sealed record CategoryMatch(
    int? NoteCount,
    (int Min, int Max)? NoteCountRange,
    ImmutableHashSet<string>? AllFunctionsIn,
    ImmutableArray<FunctionClass> RequireFunctions,
    ImmutableArray<FunctionClass> ForbidFunctions,
    ImmutableArray<FunctionClass> FunctionSequenceLowToHigh,
    int? AdjacentIntervalMinSemitones,
    bool MatchAny);

public sealed record VoicingCategory(
    string Name,
    string Description,
    ImmutableArray<CategoryMatch> Matches);

public sealed class VoicingCategoryCatalog
{
    public const string FileName = "VoicingCategories.json";
    private const string EmbeddedResourceName = "Dadabe.Fretboard.VoicingCategories.json";

    public ImmutableArray<string> Priority { get; }
    public ImmutableDictionary<string, FunctionClass> FunctionClasses { get; }
    public ImmutableArray<VoicingCategory> Categories { get; }

    private VoicingCategoryCatalog(
        ImmutableArray<string> priority,
        ImmutableDictionary<string, FunctionClass> functionClasses,
        ImmutableArray<VoicingCategory> categories)
    {
        Priority = priority;
        FunctionClasses = functionClasses;
        Categories = categories;
    }

    /// <summary>
    /// Load: embedded defaults, overlaid with <c>VoicingCategories.json</c>
    /// in <paramref name="workingDirectory"/> if present (D22). Overlay's
    /// <c>priority</c> list, if provided, fully replaces the embedded one;
    /// new categories sort to the end (before the matchAny fallthrough).
    /// </summary>
    public static VoicingCategoryCatalog Load(string? workingDirectory)
    {
        var @base = ParseEmbedded();
        if (workingDirectory is not null)
        {
            var overlayPath = Path.Combine(workingDirectory, FileName);
            if (File.Exists(overlayPath))
            {
                @base = Merge(@base, ParseFromFile(overlayPath));
            }
        }
        return ReorderByPriority(@base);
    }

    private static VoicingCategoryCatalog ParseEmbedded()
    {
        var asm = typeof(VoicingCategoryCatalog).Assembly;
        using var stream = asm.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{EmbeddedResourceName}' missing.");
        return ParseStream(stream);
    }

    private static VoicingCategoryCatalog ParseFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return ParseStream(stream);
    }

    private static VoicingCategoryCatalog ParseStream(Stream stream)
    {
        var dto = JsonSerializer.Deserialize<FileDto>(stream, SerializerOptions)
            ?? throw new InvalidDataException("VoicingCategories.json is empty or malformed.");

        var fcBuilder = ImmutableDictionary.CreateBuilder<string, FunctionClass>(StringComparer.Ordinal);
        if (dto.FunctionClasses is not null)
        {
            foreach (var (k, v) in dto.FunctionClasses)
            {
                if (v is JsonElement el && el.ValueKind == JsonValueKind.Array)
                {
                    var names = el.EnumerateArray().Select(e => e.GetString()!).ToImmutableHashSet(StringComparer.Ordinal);
                    fcBuilder[k] = new FunctionClass(names);
                }
            }
        }
        var fc = fcBuilder.ToImmutable();

        var categories = (dto.Categories ?? new())
            .Select(c => MapCategory(c, fc))
            .ToImmutableArray();

        var priority = (dto.Priority ?? new()).ToImmutableArray();

        return new VoicingCategoryCatalog(priority, fc, categories);
    }

    private static VoicingCategory MapCategory(
        CategoryDto dto,
        ImmutableDictionary<string, FunctionClass> functionClasses) =>
        new(
            dto.Name ?? throw new InvalidDataException("Category missing 'name'."),
            dto.Description ?? string.Empty,
            (dto.Matches ?? new()).Select(m => MapMatch(m, functionClasses)).ToImmutableArray());

    private static CategoryMatch MapMatch(
        MatchDto dto,
        ImmutableDictionary<string, FunctionClass> functionClasses)
    {
        (int, int)? range = null;
        if (dto.NoteCountRange is not null && dto.NoteCountRange.Count >= 2)
        {
            range = (dto.NoteCountRange[0], dto.NoteCountRange[1]);
        }
        return new CategoryMatch(
            NoteCount: dto.NoteCount,
            NoteCountRange: range,
            AllFunctionsIn: dto.AllFunctionsIn?.ToImmutableHashSet(StringComparer.Ordinal),
            RequireFunctions: (dto.RequireFunctions ?? new()).Select(s => ResolveClass(s, functionClasses)).ToImmutableArray(),
            ForbidFunctions: (dto.ForbidFunctions ?? new()).Select(s => ResolveClass(s, functionClasses)).ToImmutableArray(),
            FunctionSequenceLowToHigh: (dto.FunctionSequenceLowToHigh ?? new()).Select(s => ResolveClass(s, functionClasses)).ToImmutableArray(),
            AdjacentIntervalMinSemitones: dto.AdjacentIntervalMinSemitones,
            MatchAny: dto.MatchAny ?? false);
    }

    private static FunctionClass ResolveClass(string spec, ImmutableDictionary<string, FunctionClass> named) =>
        named.TryGetValue(spec, out var fc)
            ? fc
            : new FunctionClass(spec.Split('|').ToImmutableHashSet(StringComparer.Ordinal));

    private static VoicingCategoryCatalog Merge(VoicingCategoryCatalog @base, VoicingCategoryCatalog overlay)
    {
        var byName = @base.Categories.ToDictionary(c => c.Name, StringComparer.Ordinal);
        foreach (var c in overlay.Categories) { byName[c.Name] = c; }

        var priority = overlay.Priority.IsDefaultOrEmpty ? @base.Priority : overlay.Priority;

        var fc = @base.FunctionClasses;
        foreach (var (k, v) in overlay.FunctionClasses) { fc = fc.SetItem(k, v); }

        return new VoicingCategoryCatalog(priority, fc, byName.Values.ToImmutableArray());
    }

    /// <summary>
    /// Sort categories so the <see cref="Priority"/> list comes first in
    /// order; any extras append before the matchAny fallthrough.
    /// </summary>
    private static VoicingCategoryCatalog ReorderByPriority(VoicingCategoryCatalog cat)
    {
        var index = cat.Priority
            .Select((name, i) => (name, i))
            .ToDictionary(p => p.name, p => p.i, StringComparer.Ordinal);
        bool IsFallthrough(VoicingCategory c) => c.Matches.Any(m => m.MatchAny);
        var ordered = cat.Categories
            .OrderBy(c => IsFallthrough(c) ? 1 : 0)
            .ThenBy(c => index.TryGetValue(c.Name, out var i) ? i : int.MaxValue / 2)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToImmutableArray();
        return new VoicingCategoryCatalog(cat.Priority, cat.FunctionClasses, ordered);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class FileDto
    {
        [JsonPropertyName("schemaVersion")] public string? SchemaVersion { get; set; }

        [JsonPropertyName("priority")] public List<string>? Priority { get; set; }

        [JsonPropertyName("functionClasses")]
        public Dictionary<string, object>? FunctionClasses { get; set; }

        [JsonPropertyName("categories")] public List<CategoryDto>? Categories { get; set; }
    }

    private sealed class CategoryDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("description")] public string? Description { get; set; }

        [JsonPropertyName("matches")] public List<MatchDto>? Matches { get; set; }
    }

    private sealed class MatchDto
    {
        [JsonPropertyName("noteCount")] public int? NoteCount { get; set; }

        [JsonPropertyName("noteCountRange")] public List<int>? NoteCountRange { get; set; }

        [JsonPropertyName("allFunctionsIn")] public List<string>? AllFunctionsIn { get; set; }

        [JsonPropertyName("requireFunctions")] public List<string>? RequireFunctions { get; set; }

        [JsonPropertyName("forbidFunctions")] public List<string>? ForbidFunctions { get; set; }

        [JsonPropertyName("functionSequenceLowToHigh")]
        public List<string>? FunctionSequenceLowToHigh { get; set; }

        [JsonPropertyName("adjacentIntervalMinSemitones")]
        public int? AdjacentIntervalMinSemitones { get; set; }

        [JsonPropertyName("matchAny")] public bool? MatchAny { get; set; }
    }
}
