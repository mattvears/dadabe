using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

public class DiatonicTransposeTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly TransformCatalog Catalog = TransformCatalog.CreateDefault(Grammar);

    private static TransformResult Apply(string[] chords, Dictionary<string, object> parameters) =>
        Catalog["diatonic-transpose"].Apply(chords.Select(Parser.Parse).ToArray(), parameters);

    [Fact]
    public void Moves_by_scale_degrees_not_semitones()
    {
        var result = Apply(["C", "F", "G"], new() { ["by"] = 1, ["key"] = "C" });
        result.Chords.Should().Equal("Dm", "G", "Am");
    }

    [Fact]
    public void Out_of_key_chord_falls_back_to_chromatic_default_and_is_flagged()
    {
        // Db is not in C major; quality must be left alone (D46).
        var result = Apply(["C", "Db", "G"], new() { ["by"] = 1, ["key"] = "C" });
        result.Notes.Should().ContainSingle(n => n.Kind == "out-of-key" && n.Index == 1);
    }

    [Fact]
    public void Missing_key_produces_the_uniform_needs_key_note()
    {
        // Empty context: KeyInference.InferKey returns null (D33), triggering D46's uniform error.
        var result = Apply([], new() { ["by"] = 1 });
        result.Notes.Should().ContainSingle(n => n.Kind == "needs-key");
    }

    [Fact]
    public void Strict_mode_skips_extended_chords()
    {
        var result = Apply(["Cmaj7"], new() { ["by"] = 1, ["key"] = "C", ["strictness"] = "strict" });
        result.Chords[0].Should().Be("Cmaj7");
        result.Notes.Should().ContainSingle(n => n.Kind == "skipped");
    }

    [Fact]
    public void Loose_mode_approximates_extended_chords()
    {
        var result = Apply(["Cmaj7"], new() { ["by"] = 1, ["key"] = "C", ["strictness"] = "loose" });
        result.Chords[0].Should().Be("Dm");
        result.Notes.Should().ContainSingle(n => n.Kind == "approximated");
    }
}
