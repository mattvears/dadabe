using FluentAssertions;

namespace Dadabe.Core.Tests;

public class NoteTests
{
    [Theory]
    [InlineData(Letter.C, 0, 0)]
    [InlineData(Letter.C, 1, 1)]
    [InlineData(Letter.D, 0, 2)]
    [InlineData(Letter.D, -1, 1)]
    [InlineData(Letter.E, 1, 5)]
    [InlineData(Letter.F, 0, 5)]
    [InlineData(Letter.B, 1, 0)]
    [InlineData(Letter.C, -1, 11)]
    public void Pitch_class_derives_from_letter_plus_accidental(Letter letter, int accidental, int expectedPc)
    {
        new Note(letter, accidental).PitchClass.Value.Should().Be(expectedPc);
    }

    [Fact]
    public void Enharmonic_notes_share_pitch_class_but_differ_as_notes()
    {
        var bSharp = new Note(Letter.B, +1);
        var c = new Note(Letter.C, 0);
        bSharp.PitchClass.Should().Be(c.PitchClass);
        bSharp.Should().NotBe(c);
    }

    [Theory]
    [InlineData(Letter.C, 2, Letter.E)]
    [InlineData(Letter.E, 2, Letter.G)]
    [InlineData(Letter.G, 2, Letter.B)]
    [InlineData(Letter.B, 2, Letter.D)]
    [InlineData(Letter.F, 4, Letter.C)]
    [InlineData(Letter.C, -1, Letter.B)]
    public void Letter_plus_walks_the_diatonic_ladder(Letter from, int steps, Letter expected)
    {
        Note.LetterPlus(from, steps).Should().Be(expected);
    }

    [Theory]
    [InlineData("C", Letter.C, 0)]
    [InlineData("C#", Letter.C, 1)]
    [InlineData("Bb", Letter.B, -1)]
    [InlineData("E#", Letter.E, 1)]
    [InlineData("Bbb", Letter.B, -2)]
    [InlineData("F##", Letter.F, 2)]
    public void Parse_accepts_ascii_accidentals(string input, Letter letter, int accidental)
    {
        var note = Note.Parse(input);
        note.Letter.Should().Be(letter);
        note.Accidental.Should().Be(accidental);
    }

    [Theory]
    [InlineData("C♯", Letter.C, 1)]
    [InlineData("B♭", Letter.B, -1)]
    public void Parse_accepts_unicode_accidentals(string input, Letter letter, int accidental)
    {
        var note = Note.Parse(input);
        note.Letter.Should().Be(letter);
        note.Accidental.Should().Be(accidental);
    }

    [Theory]
    [InlineData("C")]
    [InlineData("C#")]
    [InlineData("Bb")]
    [InlineData("F##")]
    [InlineData("Bbb")]
    public void ToString_round_trips_Parse(string input)
    {
        Note.Parse(input).ToString().Should().Be(input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("H")]
    [InlineData("C###")]
    [InlineData("Cx")]
    public void Parse_rejects_malformed(string input)
    {
        var act = () => Note.Parse(input);
        act.Should().Throw<FormatException>();
    }
}
