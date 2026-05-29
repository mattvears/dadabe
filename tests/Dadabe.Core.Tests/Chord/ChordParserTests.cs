using Dadabe.Core.Chord;
using FluentAssertions;

namespace Dadabe.Core.Tests.Chord;

public class ChordParserTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);

    [Theory]
    [InlineData("C", Letter.C, 0, "")]
    [InlineData("Cmaj7", Letter.C, 0, "maj7")]
    [InlineData("Cm7", Letter.C, 0, "m7")]
    [InlineData("Cdim7", Letter.C, 0, "dim7")]
    [InlineData("Cm7b5", Letter.C, 0, "m7b5")]
    [InlineData("F#maj7", Letter.F, 1, "maj7")]
    [InlineData("Bbm11", Letter.B, -1, "m11")]
    [InlineData("Caug", Letter.C, 0, "aug")]
    [InlineData("Csus4", Letter.C, 0, "sus4")]
    [InlineData("Csus2", Letter.C, 0, "sus2")]
    public void Parses_root_and_form(string input, Letter expectedLetter, int expectedAccidental, string expectedQuality)
    {
        var chord = Parser.Parse(input);
        chord.Root.Letter.Should().Be(expectedLetter);
        chord.Root.Accidental.Should().Be(expectedAccidental);
        chord.Quality.Should().Be(expectedQuality);
    }

    [Fact]
    public void Longest_token_first_prefers_maj7_over_m()
    {
        var chord = Parser.Parse("Cmaj7");
        chord.Quality.Should().Be("maj7");
        chord.Extensions.Should().BeEmpty();
        chord.Alterations.Should().BeEmpty();
    }

    [Fact]
    public void Bare_root_parses_to_empty_form()
    {
        var chord = Parser.Parse("F#");
        chord.Quality.Should().Be("");
        chord.Root.Should().Be(new Note(Letter.F, 1));
    }

    [Fact]
    public void Parses_alterations_after_form()
    {
        var chord = Parser.Parse("G7b9");
        chord.Quality.Should().Be("7");
        chord.Alterations.Should().Contain("b9");
    }

    [Fact]
    public void Parses_multiple_alterations_in_any_order()
    {
        var ab = Parser.Parse("C7b9#11");
        var ba = Parser.Parse("C7#11b9");
        ab.Alterations.Should().BeEquivalentTo(ba.Alterations);
    }

    [Fact]
    public void Parses_additions()
    {
        var chord = Parser.Parse("Cadd9");
        chord.Quality.Should().Be("");
        chord.Extensions.Should().Contain("add9");
    }

    [Theory]
    [InlineData("C/G")]
    [InlineData("Cmaj7/E")]
    public void Slash_chords_rejected_in_v01(string input)
    {
        var act = () => Parser.Parse(input);
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("Cxyz")]
    [InlineData("Cmaj7xyz")]
    [InlineData("H")]
    [InlineData("")]
    public void Unparseable_input_throws(string input)
    {
        var act = () => Parser.Parse(input);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Enharmonic_roots_produce_distinct_symbols()
    {
        var fSharp = Parser.Parse("F#maj7");
        var gFlat = Parser.Parse("Gbmaj7");
        fSharp.Root.Should().NotBe(gFlat.Root);
        fSharp.ContentHash.Should().NotBe(gFlat.ContentHash);
    }
}
