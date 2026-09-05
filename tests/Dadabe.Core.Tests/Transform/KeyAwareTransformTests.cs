using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

/// <summary>Cross-cutting behavior shared by the whole D47 key-aware family, checked once across all four rather than duplicated per-transform.</summary>
public class KeyAwareTransformTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly TransformCatalog Catalog = TransformCatalog.CreateDefault(Grammar);

    private static readonly (string Id, Dictionary<string, object> Params)[] KeyAwareTransforms =
    [
        ("diatonic-transpose", new Dictionary<string, object> { ["by"] = 1 }),
        ("parallel-mode", new Dictionary<string, object> { ["mode"] = "mixolydian" }),
        ("substitute", []),
        ("negative-harmony", []),
    ];

    public static IEnumerable<object[]> KeyAwareIds() => KeyAwareTransforms.Select(t => new object[] { t.Id, t.Params });

    private static TransformResult Apply(string id, string[] chords, Dictionary<string, object> parameters) =>
        Catalog[id].Apply(chords.Select(Parser.Parse).ToArray(), parameters);

    [Theory]
    [MemberData(nameof(KeyAwareIds))]
    public void Missing_key_produces_the_uniform_needs_key_note(string id, Dictionary<string, object> parameters)
    {
        var result = Apply(id, [], parameters);
        result.Notes.Should().ContainSingle(n => n.Index == null && n.Kind == "needs-key");
    }

    [Theory]
    [MemberData(nameof(KeyAwareIds))]
    public void Key_override_takes_precedence_over_inference(string id, Dictionary<string, object> parameters)
    {
        // "Am, Dm, Em" would infer as A minor / C major; an explicit override to G major must win.
        var withKey = new Dictionary<string, object>(parameters) { ["key"] = "G" };
        var result = Apply(id, ["Am", "Dm", "Em"], withKey);
        result.Notes.Should().NotContain(n => n.Kind == "needs-key");
    }

    [Fact]
    public void Diatonic_transpose_diverges_between_strict_and_loose_on_an_extended_chord()
    {
        var strict = Apply("diatonic-transpose", ["Cmaj7"], new() { ["by"] = 1, ["key"] = "C", ["strictness"] = "strict" });
        var loose = Apply("diatonic-transpose", ["Cmaj7"], new() { ["by"] = 1, ["key"] = "C", ["strictness"] = "loose" });

        strict.Chords[0].Should().Be("Cmaj7");
        loose.Chords[0].Should().Be("Dm");
    }
}
