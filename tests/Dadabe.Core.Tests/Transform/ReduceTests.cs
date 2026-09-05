using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

public class ReduceTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly ReduceTransform Transform = new();

    private static string Apply(string chord, string level)
    {
        var result = Transform.Apply([Parser.Parse(chord)], new Dictionary<string, object> { ["level"] = level });
        return result.Chords[0];
    }

    [Theory]
    [InlineData("Cmaj9", "triad", "C")]
    [InlineData("F#m11", "triad", "F#m")]
    [InlineData("G13", "triad", "G")]
    [InlineData("G13", "seventh", "G7")]
    public void Reduces_to_the_expected_core(string input, string level, string expected)
    {
        Apply(input, level).Should().Be(expected);
    }
}
