using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dadabe.Core.Chord;

/// <summary>
/// Loads <see cref="ChordGrammar"/> from the embedded
/// <c>ChordGrammar.json</c>, optionally overlaid with a same-named file in
/// a working directory (D22). On collision the overlay's entry wins; on
/// new tokens it unions. After merge, form tokens (and modifier tokens) are
/// re-sorted longest-first so the parser's longest-match invariant
/// survives user extensions (D19).
/// </summary>
public static class ChordGrammarLoader
{
    public const string FileName = "ChordGrammar.json";
    private const string EmbeddedResourceName = "Dadabe.Core.Chord.ChordGrammar.json";

    public static ChordGrammar Load(string? workingDirectory)
    {
        var grammar = ParseEmbedded();
        if (workingDirectory is not null)
        {
            var overlayPath = Path.Combine(workingDirectory, FileName);
            if (File.Exists(overlayPath))
            {
                grammar = Merge(grammar, ParseFromFile(overlayPath));
            }
        }
        return Normalize(grammar);
    }

    private static ChordGrammar ParseEmbedded()
    {
        var asm = typeof(ChordGrammarLoader).Assembly;
        using var stream = asm.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' missing.");
        return ParseStream(stream);
    }

    private static ChordGrammar ParseFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return ParseStream(stream);
    }

    private static ChordGrammar ParseStream(Stream stream)
    {
        var dto = JsonSerializer.Deserialize<ChordGrammarDto>(stream, SerializerOptions)
            ?? throw new InvalidDataException("ChordGrammar.json is empty or malformed.");
        return new ChordGrammar(
            (dto.Forms ?? new()).Select(MapForm).ToImmutableArray(),
            (dto.Modifiers ?? new()).Select(MapModifier).ToImmutableArray(),
            MapParseRules(dto.ParseRules));
    }

    private static GrammarForm MapForm(FormDto dto) => new(
        (dto.Tokens ?? throw new InvalidDataException("Form missing 'tokens'.")).ToImmutableArray(),
        dto.DisplayName ?? string.Empty,
        (dto.Tones ?? new()).Select(MapTone).ToImmutableArray(),
        (dto.Required ?? new()).ToImmutableArray());

    private static GrammarModifier MapModifier(ModifierDto dto) => new(
        (dto.Tokens ?? throw new InvalidDataException("Modifier missing 'tokens'.")).ToImmutableArray(),
        ParseKind(dto.Kind),
        dto.Displaces,
        (dto.AddTones ?? new()).Select(MapTone).ToImmutableArray(),
        (dto.Required ?? new()).ToImmutableArray());

    private static GrammarTone MapTone(GrammarToneDto dto) => new(
        dto.Function ?? throw new InvalidDataException("Tone missing 'function'."),
        dto.Semitones);

    private static GrammarParseRules MapParseRules(ParseRulesDto? dto) => new(
        RootRegex: dto?.RootRegex ?? "^([A-G])(bb|##|b|#)?",
        LongestTokenFirst: dto?.LongestTokenFirst ?? true,
        ModifierOrder: dto?.ModifierOrder ?? "any",
        ModifiersAllowedAfterEmptyForm: dto?.ModifiersAllowedAfterEmptyForm ?? true,
        RejectUnparsedTail: dto?.RejectUnparsedTail ?? true,
        RejectSlash: dto?.RejectSlash ?? true);

    private static ModifierKind ParseKind(string? raw) => raw switch
    {
        "addition" => ModifierKind.Addition,
        "alteration" => ModifierKind.Alteration,
        _ => throw new InvalidDataException($"Modifier 'kind' must be 'addition' or 'alteration'; got '{raw}'."),
    };

    /// <summary>
    /// Union forms/modifiers by their first token (canonical name). Overlay
    /// entries win on collision, otherwise append to the base.
    /// </summary>
    private static ChordGrammar Merge(ChordGrammar @base, ChordGrammar overlay)
    {
        static ImmutableArray<T> MergeOn<T>(
            ImmutableArray<T> baseItems,
            ImmutableArray<T> overlayItems,
            Func<T, string> keyOf)
        {
            var overlayByKey = overlayItems.ToDictionary(keyOf, StringComparer.Ordinal);
            var result = new List<T>();
            foreach (var item in baseItems)
            {
                if (overlayByKey.TryGetValue(keyOf(item), out var replaced))
                {
                    result.Add(replaced);
                    overlayByKey.Remove(keyOf(item));
                }
                else
                {
                    result.Add(item);
                }
            }
            foreach (var item in overlayItems)
            {
                if (overlayByKey.ContainsKey(keyOf(item)))
                {
                    result.Add(item);
                }
            }
            return result.ToImmutableArray();
        }

        return new ChordGrammar(
            MergeOn(@base.Forms, overlay.Forms, f => f.Tokens[0]),
            MergeOn(@base.Modifiers, overlay.Modifiers, m => m.Tokens[0]),
            overlay.ParseRules);
    }

    /// <summary>
    /// Re-assert post-merge invariants: each form's and modifier's token
    /// list is sorted longest-first so the parser's longest-match property
    /// holds (D19 §10.2).
    /// </summary>
    private static ChordGrammar Normalize(ChordGrammar grammar) => new(
        grammar.Forms
            .Select(f => f with { Tokens = SortLongestFirst(f.Tokens) })
            .ToImmutableArray(),
        grammar.Modifiers
            .Select(m => m with { Tokens = SortLongestFirst(m.Tokens) })
            .ToImmutableArray(),
        grammar.ParseRules);

    private static ImmutableArray<string> SortLongestFirst(ImmutableArray<string> tokens) =>
        tokens
            .OrderByDescending(t => t.Length)
            .ThenBy(t => t, StringComparer.Ordinal)
            .ToImmutableArray();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class ChordGrammarDto
    {
        [JsonPropertyName("schemaVersion")]
        public string? SchemaVersion { get; set; }

        [JsonPropertyName("forms")]
        public List<FormDto>? Forms { get; set; }

        [JsonPropertyName("modifiers")]
        public List<ModifierDto>? Modifiers { get; set; }

        [JsonPropertyName("parseRules")]
        public ParseRulesDto? ParseRules { get; set; }
    }

    private sealed class FormDto
    {
        [JsonPropertyName("tokens")] public List<string>? Tokens { get; set; }

        [JsonPropertyName("displayName")] public string? DisplayName { get; set; }

        [JsonPropertyName("tones")] public List<GrammarToneDto>? Tones { get; set; }

        [JsonPropertyName("required")] public List<string>? Required { get; set; }
    }

    private sealed class ModifierDto
    {
        [JsonPropertyName("tokens")] public List<string>? Tokens { get; set; }

        [JsonPropertyName("kind")] public string? Kind { get; set; }

        [JsonPropertyName("displaces")] public string? Displaces { get; set; }

        [JsonPropertyName("addTones")] public List<GrammarToneDto>? AddTones { get; set; }

        [JsonPropertyName("required")] public List<string>? Required { get; set; }
    }

    private sealed class GrammarToneDto
    {
        [JsonPropertyName("function")] public string? Function { get; set; }

        [JsonPropertyName("semitones")] public int Semitones { get; set; }
    }

    private sealed class ParseRulesDto
    {
        [JsonPropertyName("rootRegex")] public string? RootRegex { get; set; }

        [JsonPropertyName("longestTokenFirst")] public bool? LongestTokenFirst { get; set; }

        [JsonPropertyName("modifierOrder")] public string? ModifierOrder { get; set; }

        [JsonPropertyName("modifiersAllowedAfterEmptyForm")]
        public bool? ModifiersAllowedAfterEmptyForm { get; set; }

        [JsonPropertyName("rejectUnparsedTail")] public bool? RejectUnparsedTail { get; set; }

        [JsonPropertyName("rejectSlash")] public bool? RejectSlash { get; set; }
    }
}
