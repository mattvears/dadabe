using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

/// <summary>
/// Verifies <see cref="ScaleTriads"/> reproduces the degree/quality
/// categorization <c>KeyInference.IsDiatonicQuality</c> already hardcodes for
/// major and natural minor, so unifying the two didn't silently change
/// existing key-inference behavior.
/// </summary>
public class ScaleTriadsTests
{
    private static readonly int[] MajorIntervals = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] MinorIntervals = [0, 2, 3, 5, 7, 8, 10];

    // KeyInference.IsDiatonicQuality's own table: M m m M M m dim.
    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "m")]
    [InlineData(2, "m")]
    [InlineData(3, "")]
    [InlineData(4, "")]
    [InlineData(5, "m")]
    [InlineData(6, "dim")]
    public void Major_scale_degree_qualities_match_KeyInference(int degree, string expected)
    {
        ScaleTriads.TriadQuality(MajorIntervals, degree).Should().Be(expected);
    }

    // KeyInference.IsDiatonicQuality's own table (natural minor): i=m, ii=dim, III=M, iv=m, v=m, VI=M, VII=M.
    [Theory]
    [InlineData(0, "m")]
    [InlineData(1, "dim")]
    [InlineData(2, "")]
    [InlineData(3, "m")]
    [InlineData(4, "m")]
    [InlineData(5, "")]
    [InlineData(6, "")]
    public void Natural_minor_scale_degree_qualities_match_KeyInference(int degree, string expected)
    {
        ScaleTriads.TriadQuality(MinorIntervals, degree).Should().Be(expected);
    }

    [Fact]
    public void DegreeOf_finds_the_correct_degree_and_is_null_when_out_of_scale()
    {
        // C major: C=0(deg0), D=2(deg1), E=4(deg2), F=5(deg3), G=7(deg4), A=9(deg5), B=11(deg6).
        ScaleTriads.DegreeOf(7, keyRootPc: 0, MajorIntervals).Should().Be(4);
        ScaleTriads.DegreeOf(1, keyRootPc: 0, MajorIntervals).Should().BeNull(); // Db is not in C major
    }
}
