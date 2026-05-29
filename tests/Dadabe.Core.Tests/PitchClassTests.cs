using FluentAssertions;

namespace Dadabe.Core.Tests;

public class PitchClassTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(11, 11)]
    [InlineData(12, 0)]
    [InlineData(13, 1)]
    [InlineData(-1, 11)]
    [InlineData(-13, 11)]
    public void FromInt_wraps_into_0_to_11(int input, int expected)
    {
        PitchClass.FromInt(input).Value.Should().Be(expected);
    }

    [Fact]
    public void Constructor_rejects_out_of_range()
    {
        var act = () => new PitchClass(12);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 5, 5)]
    [InlineData(7, 5, 0)]
    [InlineData(0, -1, 11)]
    public void Plus_minus_semitones_wraps(int start, int delta, int expected)
    {
        var pc = new PitchClass(start);
        (pc + delta).Value.Should().Be(expected);
    }
}
