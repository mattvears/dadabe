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
    [InlineData("E7sus4", "7")]
    [InlineData("E7sus", "7")]
    [InlineData("E9sus4", "9")]
    [InlineData("E13sus4", "13")]
    [InlineData("E13sus", "13")]
    public void Sus4_combines_with_extended_dominant_forms(string input, string expectedQuality)
    {
        // D57: sus4 is also a modifier (not just a standalone form), so it can
        // displace the 3rd on any dominant/major form — the real-world
        // fakebook notation for 7sus4/9sus4/13sus4 chords.
        var chord = Parser.Parse(input);
        chord.Quality.Should().Be(expectedQuality);
        chord.Alterations.Should().Contain("sus4");
    }

    [Fact]
    public void Sus4_displaces_the_third_and_adds_the_fourth()
    {
        var spec = new ChordExpander(Grammar).Expand(Parser.Parse("E7sus4"));
        spec.Tones.Select(t => t.Function).Should().NotContain("3");
        spec.Tones.Select(t => t.Function).Should().Contain("4");
        spec.Required.Should().Contain("4");
    }

    [Fact]
    public void Sus4_and_an_alteration_combine_in_either_order()
    {
        var ab = Parser.Parse("E7sus4b9");
        var ba = Parser.Parse("E7b9sus4");
        ab.Alterations.Should().BeEquivalentTo(ba.Alterations);
    }

    [Fact]
    public void Esus13_is_not_valid_notation_sus_must_follow_the_extension_number()
    {
        var act = () => Parser.Parse("Esus13");
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("C/G", Letter.C, 0, "", Letter.G, 0)]
    [InlineData("Cmaj7/E", Letter.C, 0, "maj7", Letter.E, 0)]
    [InlineData("Fmaj7/A", Letter.F, 0, "maj7", Letter.A, 0)]
    [InlineData("C/Bb", Letter.C, 0, "", Letter.B, -1)]
    public void Slash_chords_parse_root_quality_and_bass(
        string input, Letter root, int acc, string quality, Letter bassLetter, int bassAcc)
    {
        var chord = Parser.Parse(input);
        chord.Root.Should().Be(new Note(root, acc));
        chord.Quality.Should().Be(quality);
        chord.Bass.Should().Be(new Note(bassLetter, bassAcc));
    }

    [Theory]
    [InlineData("C/xyz")]
    [InlineData("C/")]
    public void Slash_chord_bad_bass_throws(string input)
    {
        var act = () => Parser.Parse(input);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Slash_chord_double_slash_throws()
    {
        var act = () => Parser.Parse("C/E/G");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Slash_chord_rejection_survives_overlay()
    {
        // A grammar with rejectSlash:true should still reject slash chords.
        var rules = Grammar.ParseRules with { RejectSlash = true };
        var strictGrammar = Grammar with { ParseRules = rules };
        var strictParser = new ChordParser(strictGrammar);
        var act = () => strictParser.Parse("C/G");
        act.Should().Throw<FormatException>();
    }

    [Theory(Skip = "Polychord support is not yet implemented (future extension point in ChordParser)")]
    [InlineData("C|G", "C", "G")]
    [InlineData("Dm|F#", "Dm", "F#")]
    public void Polychords_parse_to_upper_and_lower_components(string input, string upper, string lower)
    {
        // When polychord parsing is implemented, the symbol "C|G" should yield
        // an upper chord of C and a lower chord of G.  This test documents the
        // desired shape; update and unskip when the feature lands.
        var upper_ = Parser.Parse(upper);
        var lower_ = Parser.Parse(lower);
        upper_.Root.Letter.ToString().Should().Be(upper.TrimEnd('m')[0].ToString());
        lower_.Root.Letter.ToString().Should().Be(lower.TrimEnd('m')[0].ToString());
        _ = input;
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

    [Theory]
    [InlineData("C5", Letter.C, 0)]
    [InlineData("F#5", Letter.F, 1)]
    [InlineData("Bb5", Letter.B, -1)]
    public void Power_chords_parse_to_the_fifth_form(string input, Letter letter, int accidental)
    {
        var chord = Parser.Parse(input);
        chord.Root.Should().Be(new Note(letter, accidental));
        chord.Quality.Should().Be("5");
        chord.Extensions.Should().BeEmpty();
        chord.Alterations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Cm7b5", "m7b5")]
    [InlineData("C7b5", "7")]
    [InlineData("C7#5", "7")]
    public void Fifth_form_does_not_shadow_altered_fifths(string input, string expectedQuality)
    {
        // The '5' form must only match a bare "5" straight after the root — the
        // 'b5'/'#5' alteration tokens and the 'm7b5' form still win.
        Parser.Parse(input).Quality.Should().Be(expectedQuality);
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
