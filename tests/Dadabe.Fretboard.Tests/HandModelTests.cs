using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

public class HandModelTests
{
    [Fact]
    public void Default_matches_D11_constants()
    {
        var h = HandModel.Default;
        h.MaxFret.Should().Be(15);
        h.MaxSpan.Should().Be(4);
        h.MinStrings.Should().Be(3);
        h.MaxStrings.Should().Be(6);
        h.MaxBarres.Should().Be(1);
        h.Thumb.Allowed.Should().BeFalse();
        h.Thumb.MaxFret.Should().Be(5);
    }

    [Theory]
    [InlineData(Finger.Index, Finger.Middle, 2)]
    [InlineData(Finger.Middle, Finger.Ring, 2)]
    [InlineData(Finger.Ring, Finger.Pinky, 2)]
    [InlineData(Finger.Index, Finger.Pinky, 4)]
    [InlineData(Finger.Index, Finger.Ring, 3)]
    public void Stretch_matrix_matches_D11(Finger a, Finger b, int expected)
    {
        HandModel.Default.StretchBetween(a, b).Should().Be(expected);
        HandModel.Default.StretchBetween(b, a).Should().Be(expected);
    }

    [Fact]
    public void Content_hash_is_stable()
    {
        HandModel.Default.ContentHash.Should().Be(HandModel.Default.ContentHash);
    }
}
