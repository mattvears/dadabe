using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

public class TransformGuardTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);

    [Theory]
    [InlineData("C5")]
    [InlineData("Csus4")]
    public void QualityMap_skips_thirdless_chords(string symbol)
    {
        var transform = new QualityMapTransform(Grammar);
        var chord = Parser.Parse(symbol);
        var result = transform.Apply([chord], new Dictionary<string, object> { ["to"] = "m7" });

        result.Chords[0].Should().Be(symbol);
        result.Notes.Should().ContainSingle(n => n.Kind == "skipped");
    }

    [Theory]
    [InlineData("C5")]
    [InlineData("Csus4")]
    public void Plr_skips_thirdless_chords(string symbol)
    {
        var transform = new PlrTransform();
        var chord = Parser.Parse(symbol);
        var result = transform.Apply([chord], new Dictionary<string, object> { ["op"] = "P" });

        result.Chords[0].Should().Be(symbol);
        result.Notes.Should().ContainSingle(n => n.Kind == "skipped");
    }

    [Fact]
    public void TritoneSub_skips_non_dominants()
    {
        var transform = new TritoneSubTransform(Grammar);
        var chord = Parser.Parse("Cmaj7");
        var result = transform.Apply([chord], new Dictionary<string, object>());

        result.Chords[0].Should().Be("Cmaj7");
        result.Notes.Should().ContainSingle(n => n.Kind == "skipped");
    }

    [Fact]
    public void TritoneSub_moves_dominant_root_by_augmented_fourth()
    {
        var transform = new TritoneSubTransform(Grammar);
        var chord = Parser.Parse("C7");
        var result = transform.Apply([chord], new Dictionary<string, object>());

        result.Chords[0].Should().Be("F#7");
    }

    [Fact]
    public void TritoneSub_drops_slash_bass_and_reports_it()
    {
        var transform = new TritoneSubTransform(Grammar);
        var chord = Parser.Parse("C7/E");
        var result = transform.Apply([chord], new Dictionary<string, object>());

        result.Chords[0].Should().Be("F#7");
        result.Notes.Should().ContainSingle(n => n.Kind == "bass-dropped");
    }

    [Fact]
    public void Transpose_moves_slash_bass_with_root()
    {
        var transform = new TransposeTransform();
        var chord = Parser.Parse("C/E");
        var result = transform.Apply([chord], new Dictionary<string, object> { ["interval"] = "M2" });

        result.Chords[0].Should().Be("D/F#");
    }
}
