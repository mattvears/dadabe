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

    // ── hand-biomechanics penalties ──

    private static FingerAssignment Assign(int s, int fret, Finger finger) => new(s, fret, finger);

    private static FretPosition Open(int s) => new(s, 0, null, null, "1");

    private static double ComfortWith(
        ImmutableArray<FingerAssignment> assignments = default,
        ImmutableArray<FretPosition> positions = default,
        int span = 2) =>
        Voicing.ComputeComfort(
            span: span, interiorMutedStrings: 0, isolatedMutedStrings: 0,
            barres: [], lowestFret: 1, maxSpan: 4, maxFret: MaxFret,
            assignments: assignments, orderedPositions: positions);

    [Fact]
    public void Omitting_assignments_and_positions_leaves_comfort_unchanged()
    {
        // Backward compatibility: callers that only have the scalar summary
        // (no Fingering/Positions in scope) must get exactly the pre-Rule-1..4 score.
        ComfortWith().Should().Be(ComfortWith(assignments: default, positions: default));
    }

    [Fact]
    public void Rule1_reverse_stretch_index_below_and_behind_inner_finger_costs_more()
    {
        var baseline = ComfortWith();

        // Index on a lower (bass) string at a lower fret than a ring finger
        // on a higher (treble) string — the reverse-stretch shape.
        var reverseStretch = ComfortWith(assignments:
        [
            Assign(0, 1, Finger.Index),
            Assign(3, 5, Finger.Ring),
        ]);

        reverseStretch.Should().BeLessThan(baseline);
    }

    [Fact]
    public void Rule1_is_a_no_op_when_the_index_is_not_both_lower_string_and_lower_fret()
    {
        var baseline = ComfortWith();

        // Index on the higher string doesn't qualify — that's the ordinary
        // comfortable cascade, not a reverse stretch. Adjacent strings so
        // Rule 4 (abduction) doesn't also contribute here.
        var ordinaryCascade = ComfortWith(assignments:
        [
            Assign(1, 5, Finger.Index),
            Assign(0, 1, Finger.Ring),
        ]);

        ordinaryCascade.Should().Be(baseline);
    }

    [Fact]
    public void Rule1_barred_inner_finger_is_penalized_once_not_per_string()
    {
        // A barre assigns the same physical finger to multiple strings at the
        // same fret. Rule 1 must charge the reverse-stretch cost once per
        // finger, not once per string the barre happens to cover.
        var singleString = ComfortWith(assignments:
        [
            Assign(0, 1, Finger.Index),
            Assign(3, 5, Finger.Ring),
        ]);

        var barredAcrossThreeStrings = ComfortWith(assignments:
        [
            Assign(0, 1, Finger.Index),
            Assign(2, 5, Finger.Ring),
            Assign(3, 5, Finger.Ring),
            Assign(4, 5, Finger.Ring),
        ]);

        barredAcrossThreeStrings.Should().Be(singleString);
    }

    [Fact]
    public void Rule2_tendon_interdependence_spike_when_index_and_pinky_bracket_middle_and_ring()
    {
        var baseline = ComfortWith();

        // Middle/ring on adjacent strings at different frets, with index
        // reaching below and pinky reaching above both of them.
        var bracketed = ComfortWith(assignments:
        [
            Assign(0, 1, Finger.Index),
            Assign(1, 3, Finger.Middle),
            Assign(2, 5, Finger.Ring),
            Assign(3, 7, Finger.Pinky),
        ]);

        bracketed.Should().BeLessThan(baseline);
    }

    [Fact]
    public void Rule2_does_not_fire_when_middle_and_ring_share_the_same_fret()
    {
        var baseline = ComfortWith();

        // Index placed on the highest string (never "below" an inner finger,
        // so Rule 1 stays silent) and every fret equal (so Rule 4's abduction
        // term is zero regardless of string gaps) isolates Rule 2 alone.
        var samefret = ComfortWith(assignments:
        [
            Assign(1, 3, Finger.Middle),
            Assign(2, 3, Finger.Ring),
            Assign(3, 3, Finger.Pinky),
            Assign(4, 3, Finger.Index),
        ]);

        samefret.Should().Be(baseline);
    }

    [Fact]
    public void Rule2_does_not_fire_when_middle_and_ring_are_not_adjacent_strings()
    {
        var baseline = ComfortWith();

        var nonAdjacent = ComfortWith(assignments:
        [
            Assign(1, 3, Finger.Middle),
            Assign(3, 3, Finger.Ring),
            Assign(4, 3, Finger.Pinky),
            Assign(5, 3, Finger.Index),
        ]);

        nonAdjacent.Should().Be(baseline);
    }

    [Fact]
    public void Rule3_arch_penalty_for_an_open_string_sandwiched_between_two_fretted_strings()
    {
        var allFretted = ComfortWith(positions: [Sounded(0), Sounded(1), Sounded(2)]);
        var openInTheMiddle = ComfortWith(positions: [Sounded(0), Open(1), Sounded(2)]);

        openInTheMiddle.Should().BeLessThan(allFretted);
    }

    [Fact]
    public void Rule3_scales_with_span_lateral_tension()
    {
        var narrow = ComfortWith(positions: [Sounded(0), Open(1), Sounded(2)], span: 0);
        var wide = ComfortWith(positions: [Sounded(0), Open(1), Sounded(2)], span: 4);

        wide.Should().BeLessThan(narrow);
    }

    [Fact]
    public void Rule3_does_not_fire_for_an_edge_open_string()
    {
        var baseline = ComfortWith(positions: [Sounded(0), Sounded(1)]);
        var edgeOpen = ComfortWith(positions: [Open(0), Sounded(1)]);

        edgeOpen.Should().Be(baseline);
    }

    [Fact]
    public void Rule4_abduction_penalty_grows_with_fret_distance_across_a_skipped_string()
    {
        var baseline = ComfortWith();

        var smallGap = ComfortWith(assignments:
        [
            Assign(0, 3, Finger.Index),
            Assign(2, 4, Finger.Middle),
        ]);
        var largeGap = ComfortWith(assignments:
        [
            Assign(0, 1, Finger.Index),
            Assign(2, 8, Finger.Middle),
        ]);

        smallGap.Should().BeLessThan(baseline);
        largeGap.Should().BeLessThan(smallGap);
    }

    [Fact]
    public void Rule4_does_not_fire_for_adjacent_strings_regardless_of_fret_distance()
    {
        var baseline = ComfortWith();

        // Index on the higher string keeps Rule 1 silent too (it only
        // triggers when the index is on the *lower* string of the pair),
        // isolating Rule 4's adjacent-string exemption.
        var adjacent = ComfortWith(assignments:
        [
            Assign(0, 8, Finger.Middle),
            Assign(1, 1, Finger.Index),
        ]);

        adjacent.Should().Be(baseline);
    }
}
