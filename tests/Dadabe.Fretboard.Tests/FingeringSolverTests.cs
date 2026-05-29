using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

public class FingeringSolverTests
{
    private static readonly HandModel Hand = HandModel.Default;

    [Fact]
    public void Open_C_chord_solves()
    {
        // Standard open-C: x32010 (string 0 muted, then 3,2,0,1,0)
        int[] candidate = { -1, 3, 2, 0, 1, 0 };
        var f = FingeringSolver.Solve(candidate, Hand);
        f.Should().NotBeNull();
        f!.Assignments.Where(a => a.Fret > 0).Should().HaveCount(3);
        f.Mutes.Should().HaveCount(1);
        f.Barres.Should().BeEmpty();
    }

    [Fact]
    public void All_open_strings_solve()
    {
        int[] candidate = { 0, 0, 0, 0, 0, 0 };
        var f = FingeringSolver.Solve(candidate, Hand);
        f.Should().NotBeNull();
        f!.Assignments.Should().AllSatisfy(a => a.Finger.Should().BeNull());
    }

    [Fact]
    public void Barre_at_one_fret_uses_one_finger()
    {
        // F-major-style barre: 1 3 3 2 1 1
        int[] candidate = { 1, 3, 3, 2, 1, 1 };
        var f = FingeringSolver.Solve(candidate, Hand);
        f.Should().NotBeNull();
        f!.Barres.Should().HaveCount(1);
        f.Barres[0].Finger.Should().Be(Finger.Index);
    }

    [Fact]
    public void Impossible_stretch_returns_null()
    {
        // Single fret on string 0, very far fret on string 1.
        int[] candidate = { 1, 14, -1, -1, -1, -1 };
        var f = FingeringSolver.Solve(candidate, Hand);
        f.Should().BeNull();
    }

    [Fact]
    public void Five_distinct_fret_groups_without_thumb_returns_null()
    {
        int[] candidate = { 1, 2, 3, 4, 5, -1 };
        var f = FingeringSolver.Solve(candidate, Hand);
        f.Should().BeNull();
    }

    [Fact]
    public void Five_fret_groups_with_thumb_allowed_and_string_0_lowest_solves()
    {
        var thumbHand = new HandModel(
            name: "Thumb",
            maxFret: 15,
            maxSpan: 10,
            minStrings: 3,
            maxStrings: 6,
            stretch: HandModel.Default.Stretch,
            thumb: new ThumbPolicy(Allowed: true, MaxFret: 5, String: 0),
            maxBarres: 1);
        int[] candidate = { 1, 2, 3, 4, 5, -1 };
        var f = FingeringSolver.Solve(candidate, thumbHand);
        f.Should().NotBeNull();
        f!.Assignments.Should().Contain(a => a.Finger == Finger.Thumb);
    }

    [Fact]
    public void All_muted_returns_null()
    {
        int[] candidate = { -1, -1, -1, -1, -1, -1 };
        var f = FingeringSolver.Solve(candidate, Hand);
        f.Should().BeNull();
    }

    [Fact]
    public void Solution_is_deterministic()
    {
        int[] candidate = { -1, 3, 2, 0, 1, 0 };
        var a = FingeringSolver.Solve(candidate, Hand);
        var b = FingeringSolver.Solve(candidate, Hand);
        a!.ContentHash.Should().Be(b!.ContentHash);
    }
}
