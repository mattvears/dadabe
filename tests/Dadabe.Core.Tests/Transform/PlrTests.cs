using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

public class PlrTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly PlrTransform Transform = new();

    private static string Apply(string chord, string op)
    {
        var result = Transform.Apply([Parser.Parse(chord)], new Dictionary<string, object> { ["op"] = op });
        return result.Chords[0];
    }

    [Theory]
    [InlineData("C", "P", "Cm")]
    [InlineData("C", "L", "Em")]
    [InlineData("C", "R", "Am")]
    public void Major_triad_operations_match_the_table(string input, string op, string expected)
    {
        Apply(input, op).Should().Be(expected);
    }

    [Theory]
    [InlineData("Cm", "P", "C")]
    [InlineData("Em", "L", "C")]
    [InlineData("Am", "R", "C")]
    public void Minor_triad_operations_are_the_inverse(string input, string op, string expected)
    {
        Apply(input, op).Should().Be(expected);
    }

    [Theory]
    [InlineData("P")]
    [InlineData("L")]
    [InlineData("R")]
    public void Each_operation_is_its_own_inverse(string op)
    {
        var chord = Parser.Parse("C");
        var once = Transform.Apply([chord], new Dictionary<string, object> { ["op"] = op });
        var onceChord = Parser.Parse(once.Chords[0]);
        var twice = Transform.Apply([onceChord], new Dictionary<string, object> { ["op"] = op });

        twice.Chords[0].Should().Be("C");
    }

    [Fact]
    public void NonTriads_are_skipped_and_reported()
    {
        var chord = Parser.Parse("Cmaj7");
        var result = Transform.Apply([chord], new Dictionary<string, object> { ["op"] = "P" });

        result.Chords[0].Should().Be("Cmaj7");
        result.Notes.Should().ContainSingle(n => n.Kind == "skipped" && n.Index == 0);
    }
}
