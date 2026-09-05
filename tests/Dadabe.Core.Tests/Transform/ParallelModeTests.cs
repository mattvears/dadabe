using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

public class ParallelModeTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly TransformCatalog Catalog = TransformCatalog.CreateDefault(Grammar);

    private static TransformResult Apply(string[] chords, Dictionary<string, object> parameters) =>
        Catalog["parallel-mode"].Apply(chords.Select(Parser.Parse).ToArray(), parameters);

    [Theory]
    [InlineData("mixolydian", new[] { "C", "F", "Gm" })]
    [InlineData("dorian", new[] { "Cm", "F", "Gm" })]
    [InlineData("aeolian", new[] { "Cm", "Fm", "Gm" })]
    [InlineData("lydian", new[] { "C", "F#dim", "G" })]
    public void Reproduces_the_design_docs_worked_examples(string mode, string[] expected)
    {
        var result = Apply(["C", "F", "G"], new() { ["mode"] = mode, ["key"] = "C" });
        result.Chords.Should().Equal(expected);
    }

    [Fact]
    public void Exactly_one_chord_moves_for_mixolydian()
    {
        var result = Apply(["C", "F", "G"], new() { ["mode"] = "mixolydian", ["key"] = "C" });
        result.Notes.Should().ContainSingle(n => n.Kind == "mode-shift");
    }

    [Fact]
    public void Positions_restricts_selective_borrowing()
    {
        // [C,F,G] -> [Cm,F,G]: dorian would also move G, but position 0 only borrows the tonic (doc line 97).
        var result = Apply(["C", "F", "G"], new() { ["mode"] = "dorian", ["key"] = "C", ["positions"] = "0" });
        result.Chords.Should().Equal("Cm", "F", "G");
    }

    [Fact]
    public void Unknown_mode_throws()
    {
        var act = () => Apply(["C"], new() { ["mode"] = "not-a-mode", ["key"] = "C" });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Missing_key_produces_the_uniform_needs_key_note()
    {
        var result = Apply([], new() { ["mode"] = "mixolydian" });
        result.Notes.Should().ContainSingle(n => n.Kind == "needs-key");
    }

    [Fact]
    public void Out_of_key_chord_falls_back_to_D46_default()
    {
        var result = Apply(["Db"], new() { ["mode"] = "mixolydian", ["key"] = "C" });
        result.Chords[0].Should().Be("Db");
        result.Notes.Should().ContainSingle(n => n.Kind == "out-of-key");
    }
}
