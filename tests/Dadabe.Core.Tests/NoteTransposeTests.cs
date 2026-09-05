using FluentAssertions;

namespace Dadabe.Core.Tests;

public class NoteTransposeTests
{
    [Theory]
    [InlineData("C", "m2", 1, "Db")]
    [InlineData("C", "A1", 1, "C#")]
    [InlineData("C", "M2", 2, "D")]
    [InlineData("C", "m3", 3, "Eb")]
    [InlineData("C", "M3", 4, "E")]
    [InlineData("C", "P4", 5, "F")]
    [InlineData("C", "A4", 6, "F#")]
    [InlineData("C", "P5", 7, "G")]
    [InlineData("F#", "P5", 7, "C#")]
    [InlineData("Bb", "M3", 4, "D")]
    public void Transposes_by_named_interval_with_correct_spelling(
        string root, string quality, int semitones, string expected)
    {
        var note = Note.Parse(root);
        var interval = new Interval(semitones, quality);
        note.Transpose(interval).ToString().Should().Be(expected);
    }

    [Fact]
    public void Minor_second_and_augmented_unison_spell_differently_for_same_semitone_move()
    {
        var c = Note.Parse("C");
        c.Transpose(new Interval(1, "m2")).Should().Be(Note.Parse("Db"));
        c.Transpose(new Interval(1, "A1")).Should().Be(Note.Parse("C#"));
    }

    [Fact]
    public void Overflow_respells_enharmonically_instead_of_throwing()
    {
        // Fbb down a diminished unison wants a third flat on F (Fbbb) — outside ±2
        // accidentals, so it must respell rather than throw.
        var fDoubleFlat = new Note(Letter.F, -2);
        var act = () => fDoubleFlat.Transpose(new Interval(-1, "d1"), out _);
        act.Should().NotThrow();

        var result = fDoubleFlat.Transpose(new Interval(-1, "d1"), out var note);
        note.Should().NotBeNull();
        note!.Should().Contain("respelled as");
        result.Should().Be(new Note(Letter.E, -2));
        result.Accidental.Should().BeInRange(Note.MinAccidental, Note.MaxAccidental);
    }

    [Fact]
    public void In_range_transposition_reports_no_spelling_note()
    {
        Note.Parse("C").Transpose(new Interval(7, "P5"), out var note);
        note.Should().BeNull();
    }
}
