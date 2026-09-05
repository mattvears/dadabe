using Dadabe.Core.Chord;
using FluentAssertions;

namespace Dadabe.Core.Tests.Chord;

/// <summary>
/// Cross-checks the Core-native <see cref="DiatonicModes"/> table against the
/// literal values shipped in the Editor's reference glossary
/// (src/Dadabe.Editor/data/reference/modes/*.json) — the two tables aren't
/// wired together at runtime (D47 uses Core-only data deliberately), so
/// nothing else would catch them drifting apart.
/// </summary>
public class DiatonicModesTests
{
    [Theory]
    [InlineData("ionian", new[] { 0, 2, 4, 5, 7, 9, 11 })]
    [InlineData("dorian", new[] { 0, 2, 3, 5, 7, 9, 10 })]
    [InlineData("phrygian", new[] { 0, 1, 3, 5, 7, 8, 10 })]
    [InlineData("lydian", new[] { 0, 2, 4, 6, 7, 9, 11 })]
    [InlineData("mixolydian", new[] { 0, 2, 4, 5, 7, 9, 10 })]
    [InlineData("aeolian", new[] { 0, 2, 3, 5, 7, 8, 10 })]
    [InlineData("locrian", new[] { 0, 1, 3, 5, 6, 8, 10 })]
    public void Matches_the_reference_glossary_json(string mode, int[] expected)
    {
        DiatonicModes.ByName[mode].Should().Equal(expected);
    }

    [Fact]
    public void Major_and_minor_are_aliases_for_ionian_and_aeolian()
    {
        DiatonicModes.ByName["major"].Should().Equal(DiatonicModes.ByName["ionian"]);
        DiatonicModes.ByName["minor"].Should().Equal(DiatonicModes.ByName["aeolian"]);
    }
}
