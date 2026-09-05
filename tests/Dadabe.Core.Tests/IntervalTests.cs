using FluentAssertions;

namespace Dadabe.Core.Tests;

public class IntervalTests
{
    [Theory]
    [InlineData("P1", 0)]
    [InlineData("m2", 1)]
    [InlineData("M2", 1)]
    [InlineData("m3", 2)]
    [InlineData("M3", 2)]
    [InlineData("P4", 3)]
    [InlineData("A4", 3)]
    [InlineData("P5", 4)]
    [InlineData("m6", 5)]
    [InlineData("M6", 5)]
    [InlineData("m7", 6)]
    [InlineData("M7", 6)]
    [InlineData("P8", 7)]
    [InlineData("m9", 8)]
    [InlineData("M9", 8)]
    [InlineData("A9", 8)]
    [InlineData("P11", 10)]
    [InlineData("A11", 10)]
    [InlineData("m13", 12)]
    [InlineData("M13", 12)]
    public void DiatonicSteps_is_the_quality_digits_minus_one(string quality, int expectedSteps)
    {
        new Interval(0, quality).DiatonicSteps.Should().Be(expectedSteps);
    }

    [Fact]
    public void DiatonicSteps_throws_when_quality_has_no_digits()
    {
        var act = () => new Interval(0, "st").DiatonicSteps;
        act.Should().Throw<FormatException>();
    }
}
