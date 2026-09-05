using System.Collections.Immutable;
using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

public class VoicingComfortTests
{
    private const int MaxFret = 15;

    private static BarreGroup Barre(int fret, int low = 0, int high = 5, Finger finger = Finger.Index) =>
        new(finger, fret, low, high);

    private static double Penalty(int fret, int low = 0, int high = 5, Finger finger = Finger.Index) =>
        Voicing.BarrePenalty(Barre(fret, low, high, finger), MaxFret);

    // Only Muted (i.e. Fret is null) matters to the mute-counting helpers, so
    // these stand-ins leave the pitch fields empty.
    private static FretPosition Sounded(int s) => new(s, 3, null, null, "1");

    private static FretPosition Muted(int s) => FretPosition.Mute(s);

    private static int InteriorMutes(IReadOnlyList<FretPosition> p) =>
        p.Count(x => x.Muted) - Voicing.CountEdgeMutes(p);

    [Fact]
    public void Isolated_mute_lowers_comfort_more_than_an_interior_block_mute()
    {
        // Same span, mute count, barres and lowest fret — only the
        // muted-string arrangement differs.
        var blockMutes = Voicing.ComputeComfort(
            span: 1, interiorMutedStrings: 2, isolatedMutedStrings: 0,
            barres: [], lowestFret: 1, maxSpan: 4, maxFret: MaxFret);

        var isolatedMutes = Voicing.ComputeComfort(
            span: 1, interiorMutedStrings: 2, isolatedMutedStrings: 2,
            barres: [], lowestFret: 1, maxSpan: 4, maxFret: MaxFret);

        isolatedMutes.Should().BeLessThan(blockMutes);
    }

    [Fact]
    public void Drop2_with_sandwiched_mutes_scores_below_edge_muted_triad_shape()
    {
        // [2 1 2 x x x]: three contiguous strings sounded, the rest simply not
        // struck — the mutes run off the end of the neck and are free.
        var edgeMuted = Voicing.ComputeComfort(
            span: 1, interiorMutedStrings: 0, isolatedMutedStrings: 0,
            barres: [], lowestFret: 1, maxSpan: 4, maxFret: MaxFret);

        // [2 1 x 3 x 3]: a drop-2 shape where two non-adjacent strings must
        // be deadened between sounded neighbours — much trickier in practice.
        var sandwichMuted = Voicing.ComputeComfort(
            span: 2, interiorMutedStrings: 2, isolatedMutedStrings: 2,
            barres: [], lowestFret: 1, maxSpan: 4, maxFret: MaxFret);

        sandwichMuted.Should().BeLessThan(edgeMuted);
    }

    [Fact]
    public void Trailing_mutes_are_all_edge_mutes()
    {
        // [3 3 5 5 x x] — the top two strings are never struck.
        var positions = new[] { Sounded(0), Sounded(1), Sounded(2), Sounded(3), Muted(4), Muted(5) };
        Voicing.CountEdgeMutes(positions).Should().Be(2);
        Voicing.CountIsolatedMutes(positions).Should().Be(0);
    }

    [Fact]
    public void Leading_mutes_are_all_edge_mutes()
    {
        // [x x 5 0 5 0] — the bottom two strings are never struck.
        var positions = new[] { Muted(0), Muted(1), Sounded(2), Sounded(3), Sounded(4), Sounded(5) };
        Voicing.CountEdgeMutes(positions).Should().Be(2);
    }

    [Fact]
    public void Mutes_at_both_ends_are_all_edge_mutes()
    {
        var positions = new[] { Muted(0), Sounded(1), Sounded(2), Sounded(3), Muted(4), Muted(5) };
        Voicing.CountEdgeMutes(positions).Should().Be(3);
    }

    [Fact]
    public void Interior_mute_is_not_an_edge_mute()
    {
        // [3 3 x 5 x x] — string 2 sits between sounded neighbours and must
        // actually be deadened; strings 4-5 run off the end and are free.
        var positions = new[] { Sounded(0), Sounded(1), Muted(2), Sounded(3), Muted(4), Muted(5) };
        Voicing.CountEdgeMutes(positions).Should().Be(2);
        Voicing.CountIsolatedMutes(positions).Should().Be(1);
    }

    [Fact]
    public void All_muted_counts_every_string_as_an_edge_mute()
    {
        var positions = Enumerable.Range(0, 6).Select(Muted).ToArray();
        Voicing.CountEdgeMutes(positions).Should().Be(6);
    }

    [Fact]
    public void Edge_mutes_cost_nothing_at_all()
    {
        // An unstruck string is free: [3 3 5 5 x x] must score exactly the same
        // as the same grip with every string sounding.
        var edgeMuted = new[] { Sounded(0), Sounded(1), Sounded(2), Sounded(3), Muted(4), Muted(5) };
        var allSounded = Enumerable.Range(0, 6).Select(Sounded).ToArray();

        InteriorMutes(edgeMuted).Should().Be(0, "trailing mutes are never struck");

        double Score(IReadOnlyList<FretPosition> p) => Voicing.ComputeComfort(
            span: 2, interiorMutedStrings: InteriorMutes(p),
            isolatedMutedStrings: Voicing.CountIsolatedMutes(p),
            barres: [], lowestFret: 3, maxSpan: 4, maxFret: MaxFret);

        Score(edgeMuted).Should().Be(Score(allSounded));
    }

    [Fact]
    public void Interior_mute_still_costs_while_edge_mutes_do_not()
    {
        var edgeOnly = new[] { Sounded(0), Sounded(1), Sounded(2), Sounded(3), Muted(4), Muted(5) };
        var withInterior = new[] { Sounded(0), Sounded(1), Muted(2), Sounded(3), Muted(4), Muted(5) };

        InteriorMutes(edgeOnly).Should().Be(0);
        InteriorMutes(withInterior).Should().Be(1);
    }

    [Fact]
    public void Nut_barre_costs_far_more_than_the_same_barre_up_the_neck()
    {
        // The F-major wall vs. the same full barre at the fifth.
        Penalty(fret: 1).Should().BeGreaterThan(Penalty(fret: 5) * 3);
    }

    [Fact]
    public void Barre_cost_falls_steeply_over_the_first_few_frets()
    {
        // Quadratic decay: by fret 3 a barre should already be routine, which a
        // linear ramp to EasyBarreFret would not deliver.
        var atNut = Penalty(fret: 1);
        var atThird = Penalty(fret: 3);
        var atFifth = Penalty(fret: 5);

        atThird.Should().BeLessThan(atNut / 2);
        atThird.Should().BeLessThan(atFifth * 2);
    }

    [Fact]
    public void Barre_cost_is_monotonically_non_increasing_up_to_the_easy_fret()
    {
        for (var fret = 1; fret < 5; fret++)
        {
            Penalty(fret).Should().BeGreaterThan(Penalty(fret + 1),
                $"a barre at fret {fret} is harder than one at fret {fret + 1}");
        }
    }

    [Fact]
    public void Very_high_barre_costs_more_than_a_mid_neck_one()
    {
        Penalty(fret: 15).Should().BeGreaterThan(Penalty(fret: 5));
    }

    [Fact]
    public void Wider_barre_costs_more_than_a_partial_at_the_same_fret()
    {
        Penalty(fret: 5, low: 0, high: 5).Should().BeGreaterThan(Penalty(fret: 5, low: 0, high: 1));
    }

    [Fact]
    public void Width_matters_less_than_fret_position()
    {
        // A narrow barre at the nut is still worse than a full barre mid-neck.
        Penalty(fret: 1, low: 0, high: 1).Should().BeGreaterThan(Penalty(fret: 5, low: 0, high: 5));
    }

    [Fact]
    public void Non_index_barre_is_penalised_over_an_index_barre()
    {
        Penalty(fret: 5, finger: Finger.Ring).Should().BeGreaterThan(Penalty(fret: 5, finger: Finger.Index));
    }

    [Fact]
    public void Clean_mid_neck_barre_is_cheaper_than_the_old_flat_penalty()
    {
        // The whole point of the rebalance: a comfortable barre no longer costs
        // the same 0.15 as the awkward ones.
        Penalty(fret: 5).Should().BeLessThan(0.15);
        Penalty(fret: 3).Should().BeLessThan(0.15);
    }

    [Fact]
    public void Awkward_barre_is_dearer_than_the_old_flat_penalty()
    {
        Penalty(fret: 1).Should().BeGreaterThan(0.15);
    }

    [Fact]
    public void Barres_accumulate()
    {
        var one = Voicing.ComputeComfort(
            span: 2, interiorMutedStrings: 0, isolatedMutedStrings: 0,
            barres: [Barre(5)], lowestFret: 5, maxSpan: 4, maxFret: MaxFret);

        var two = Voicing.ComputeComfort(
            span: 2, interiorMutedStrings: 0, isolatedMutedStrings: 0,
            barres: [Barre(5), Barre(7)], lowestFret: 5, maxSpan: 4, maxFret: MaxFret);

        two.Should().BeLessThan(one);
    }

    [Fact]
    public void Default_barre_array_is_treated_as_no_barres()
    {
        var comfort = Voicing.ComputeComfort(
            span: 1, interiorMutedStrings: 0, isolatedMutedStrings: 0,
            barres: default, lowestFret: 1, maxSpan: 4, maxFret: MaxFret);

        comfort.Should().Be(Voicing.ComputeComfort(
            span: 1, interiorMutedStrings: 0, isolatedMutedStrings: 0,
            barres: ImmutableArray<BarreGroup>.Empty, lowestFret: 1, maxSpan: 4, maxFret: MaxFret));
    }
}
