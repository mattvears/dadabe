using FluentAssertions;

namespace Dadabe.Core.Tests;

public class PitchTests
{
    [Theory]
    [InlineData("A4", 69)]
    [InlineData("C4", 60)]
    [InlineData("C0", 12)]
    [InlineData("C-1", 0)]
    [InlineData("G9", 127)]
    public void Midi_matches_12_TET_with_A4_equal_69(string spn, int expectedMidi)
    {
        Pitch.Parse(spn).Midi.Should().Be(expectedMidi);
    }

    [Fact]
    public void Cb4_and_B3_share_midi_but_keep_distinct_octaves()
    {
        var cb4 = Pitch.Parse("Cb4");
        var b3 = Pitch.Parse("B3");
        cb4.Midi.Should().Be(59);
        b3.Midi.Should().Be(59);
        cb4.Octave.Should().Be(4);
        b3.Octave.Should().Be(3);
        cb4.Should().NotBe(b3);
    }

    [Theory]
    [InlineData("C4")]
    [InlineData("C#4")]
    [InlineData("Bb3")]
    [InlineData("E#4")]
    [InlineData("C-1")]
    public void ToString_round_trips_Parse(string spn)
    {
        Pitch.Parse(spn).ToString().Should().Be(spn);
    }

    [Fact]
    public void Pitch_minus_Pitch_returns_semitone_interval()
    {
        var interval = Pitch.Parse("G4") - Pitch.Parse("C4");
        interval.Semitones.Should().Be(7);
        interval.Quality.Should().Be("P5");
    }

    [Fact]
    public void Pitch_plus_Interval_transposes_up()
    {
        var transposed = Pitch.Parse("C4") + Interval.Semi(7);
        transposed.Midi.Should().Be(Pitch.Parse("G4").Midi);
    }
}
