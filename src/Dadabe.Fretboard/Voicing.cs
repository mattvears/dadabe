using System.Collections.Immutable;
using Dadabe.Core;
using Dadabe.Core.Chord;
using Dadabe.Core.Memo;

namespace Dadabe.Fretboard;

/// <summary>
/// A rendering of a chord onto a tuning by a hand: per-string positions
/// (sounded or muted), the fingering that realises them, the matched
/// voicing category, and a comfort hint. Identity binds the chord spec,
/// tuning, and hand model that produced it (§8.1 / D17), so the JSON
/// <c>id</c> is a faithful content hash of the JSON output.
/// </summary>
public sealed record Voicing : IContentHashable
{
    public ChordSpec ChordSpec { get; }
    public Tuning Tuning { get; }
    public HandModel HandModel { get; }
    public ImmutableArray<FretPosition> Positions { get; }
    public Fingering Fingering { get; }

    /// <summary>
    /// Structural category (drop-2, shell, triad, …). V1 value is the
    /// classifier result per D20. Renamed from <c>Category</c> per E8/D24.
    /// </summary>
    public string Structure { get; }

    /// <summary>
    /// Functional labels (altered-dominant, upper-structure, …). Empty in
    /// v1; v2 will populate via a function classifier per E8/D24.
    /// </summary>
    public IReadOnlyList<string> Functions { get; }

    public double Comfort { get; }

    public Voicing(
        ChordSpec chordSpec,
        Tuning tuning,
        HandModel handModel,
        ImmutableArray<FretPosition> positions,
        Fingering fingering,
        string structure,
        double comfort,
        IReadOnlyList<string>? functions = null)
    {
        ArgumentNullException.ThrowIfNull(chordSpec);
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(handModel);
        ArgumentNullException.ThrowIfNull(fingering);
        ArgumentNullException.ThrowIfNull(structure);
        ChordSpec = chordSpec;
        Tuning = tuning;
        HandModel = handModel;
        Positions = positions;
        Fingering = fingering;
        Structure = structure;
        Functions = functions ?? Array.Empty<string>();
        Comfort = comfort;
    }

    public IEnumerable<FretPosition> Sounded => Positions.Where(p => !p.Muted);

    public Pitch? BassNote => Sounded
        .Where(p => p.SoundingPitch is not null)
        .OrderBy(p => p.SoundingPitch!.Value.Midi)
        .FirstOrDefault()
        ?.SoundingPitch;

    public Pitch? TopNote => Sounded
        .Where(p => p.SoundingPitch is not null)
        .OrderByDescending(p => p.SoundingPitch!.Value.Midi)
        .FirstOrDefault()
        ?.SoundingPitch;

    public int OpenStrings => Positions.Count(p => p.Open);

    public int MutedStrings => Positions.Count(p => p.Muted);

    public int? LowestFret
    {
        get
        {
            var min = int.MaxValue;
            foreach (var p in Positions) { if (p.Fret is { } f && f > 0 && f < min) { min = f; } }
            return min == int.MaxValue ? null : min;
        }
    }

    public int? HighestFret
    {
        get
        {
            var max = int.MinValue;
            foreach (var p in Positions) { if (p.Fret is { } f && f > max) { max = f; } }
            return max == int.MinValue ? null : max;
        }
    }

    public int Span
    {
        get
        {
            var min = int.MaxValue;
            var max = int.MinValue;
            foreach (var p in Positions)
            {
                if (p.Fret is not { } f || f <= 0) { continue; }
                if (f < min) { min = f; }
                if (f > max) { max = f; }
            }
            return min == int.MaxValue ? 0 : max - min;
        }
    }

    public ContentHash ContentHash
    {
        get
        {
            var c = new Canonical()
                .HashDigest(ChordSpec.ContentHash)
                .HashDigest(Tuning.ContentHash)
                .HashDigest(HandModel.ContentHash)
                .U16BE((ushort)Positions.Length);
            foreach (var p in Positions.OrderBy(p => p.String))
            {
                c.U8((byte)p.String).I16BE((short)(p.Fret ?? -1));
            }
            return ContentHash.FromCanonical(Namespaces.Voicing, 1, c.AsSpan());
        }
    }

    /// <summary>Barre cost when nothing about the barre is awkward.</summary>
    private const double BarreBaseCost = 0.03;

    /// <summary>
    /// Fret at or above which a barre stops being fought by the nut. Barres
    /// are easiest from here up to roughly the octave.
    /// </summary>
    private const int EasyBarreFret = 5;

    /// <summary>Extra cost of a barre right at the nut, decaying to zero by <see cref="EasyBarreFret"/>.</summary>
    private const double NutBarreSurcharge = 0.18;

    /// <summary>Extra cost of a barre up where the frets crowd together and the body blocks the hand.</summary>
    private const double HighBarreSurcharge = 0.05;

    /// <summary>Extra cost of a full-width barre over a two-string partial.</summary>
    private const double WideBarreSurcharge = 0.03;

    /// <summary>Barring with anything but the index finger is far harder.</summary>
    private const double NonIndexBarreMultiplier = 1.7;

    /// <summary>
    /// Difficulty of one barre, in comfort points.
    /// <para>
    /// Barre difficulty is dominated by <em>where</em> the barre sits, not by
    /// how many strings it covers. At the nut the string is at its longest and
    /// tightest and the frets are at their widest, which is why the fret-1
    /// barre is the wall every player hits; by the third or fourth fret the
    /// same shape is routine. The nut surcharge therefore decays
    /// <em>quadratically</em> to zero at <see cref="EasyBarreFret"/> — a linear
    /// ramp would still be charging half price at fret 3, where the hand is
    /// already comfortable.
    /// </para>
    /// <para>
    /// Width is a much weaker signal: once the finger is flat, covering six
    /// strings is not greatly worse than covering two, so it contributes at
    /// most <see cref="WideBarreSurcharge"/>. Barring with a finger other than
    /// the index is the genuinely awkward case and is scaled, not just
    /// incremented.
    /// </para>
    /// </summary>
    public static double BarrePenalty(BarreGroup barre, int maxFret)
    {
        ArgumentNullException.ThrowIfNull(barre);

        var width = barre.HighStringInclusive - barre.LowStringInclusive + 1;

        var towardNut = Math.Clamp(
            (EasyBarreFret - barre.Fret) / (double)Math.Max(1, EasyBarreFret - 1), 0.0, 1.0);
        var nut = NutBarreSurcharge * towardNut * towardNut;

        var high = HighBarreSurcharge * Math.Clamp(
            (barre.Fret - EasyBarreFret) / (double)Math.Max(1, maxFret - EasyBarreFret), 0.0, 1.0);

        var wide = WideBarreSurcharge * Math.Clamp((width - 2) / 4.0, 0.0, 1.0);

        var cost = BarreBaseCost + nut + high + wide;
        return barre.Finger == Finger.Index ? cost : cost * NonIndexBarreMultiplier;
    }

    /// <summary>
    /// Muted strings forming an unbroken run off either end of the neck.
    /// <para>
    /// These are not really muted: the picking hand simply never strikes them,
    /// so they cost the fretting hand nothing and carry no comfort penalty.
    /// Only mutes with sounded strings on both sides require actual deadening.
    /// </para>
    /// <para><paramref name="positions"/> must be ordered by string index.</para>
    /// </summary>
    public static int CountEdgeMutes(IReadOnlyList<FretPosition> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var n = positions.Count;

        var leading = 0;
        while (leading < n && positions[leading].Muted) { leading++; }
        if (leading == n) { return n; }  // nothing sounded at all

        var trailing = 0;
        while (trailing < n - leading && positions[n - 1 - trailing].Muted) { trailing++; }
        return leading + trailing;
    }

    /// <summary>
    /// Counts muted strings that sit between two sounded strings — these
    /// require deadening one string with a finger while neighbours on both
    /// sides are actively fretted/picked, which is harder than muting a
    /// block of adjacent strings or strings at the edge of the chord.
    /// <para><paramref name="positions"/> must be ordered by string index.</para>
    /// </summary>
    public static int CountIsolatedMutes(IReadOnlyList<FretPosition> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var count = 0;
        for (var i = 1; i < positions.Count - 1; i++)
        {
            if (positions[i].Muted && !positions[i - 1].Muted && !positions[i + 1].Muted)
            {
                count++;
            }
        }
        return count;
    }

    // ── hand-biomechanics penalties ──
    // Rules 1/2/4 read finger assignments; rule 3 reads the sounded/open/muted
    // shape of the string set. Both are optional (default empty) so existing
    // callers that only have the scalar summary (span, mute counts, …) keep
    // their prior comfort values unchanged — only VoicingSearch, which has the
    // full Fingering and Positions in scope, supplies them.

    /// <summary>Flat cost per qualifying reverse-stretch pair (Rule 1), scaled by how far the fret gap is.</summary>
    private const double HandAnglePerFret = 0.05;

    /// <summary>Cap on the total Rule-1 penalty regardless of how many qualifying pairs exist.</summary>
    private const double HandAngleCap = 0.30;

    /// <summary>Flat spike when the middle/ring tendon-interdependence condition fires (Rule 2).</summary>
    private const double TendonInterdependenceSpike = 0.35;

    /// <summary>Base cost per inner sounded string left open between two fretted neighbours (Rule 3).</summary>
    private const double ArchBaseCost = 0.08;

    /// <summary>Exponent base for the per-fret abduction surcharge on non-adjacent-string finger pairs (Rule 4).</summary>
    private const double AbductionExponentBase = 1.6;

    /// <summary>Scale applied to the abduction exponential before it is added to the comfort deduction.</summary>
    private const double AbductionScale = 0.02;

    /// <summary>
    /// Rule 1 — Natural Cascade vs. Reverse Stretch (hand-angle penalty).
    /// Baseline: the index finger sits toward the nut/bass side of a shape
    /// and the middle/ring fingers reach toward the body/treble side as fret
    /// and string both climb together. A <em>reverse stretch</em> is the
    /// same climb happening on the index while an inner finger is pinned
    /// further down the neck on a more treble string — an outer finger
    /// reaching past an inner one forces the wrist to supinate to keep the
    /// inner finger's joint upright. Penalty scales with how many frets the
    /// index has to reach past the inner finger, capped so one extreme
    /// outlier can't zero out an otherwise fine voicing on its own.
    /// </summary>
    private static double HandAnglePenalty(ImmutableArray<FingerAssignment> assignments)
    {
        if (assignments.IsDefaultOrEmpty) { return 0.0; }

        var index = assignments.FirstOrDefault(a => a.Finger == Fretboard.Finger.Index);
        if (index is null) { return 0.0; }

        var total = 0.0;
        foreach (var inner in assignments
            .Where(a => a.Finger is Fretboard.Finger.Middle or Fretboard.Finger.Ring)
            .GroupBy(a => a.Finger)
            .Select(g => g.First()))
        {
            if (index.String < inner.String && index.Fret < inner.Fret)
            {
                total += HandAnglePerFret * (inner.Fret - index.Fret);
            }
        }
        return Math.Min(total, HandAngleCap);
    }

    /// <summary>
    /// Rule 2 — Tendon Interdependence (flexor digitorum profundus
    /// constraint). The middle and ring fingers share tendon sheaths, so
    /// asking them to fret adjacent strings at different frets already
    /// fights their coupled mobility; asking that <em>while</em> the index
    /// and pinky are stretched away from each other in opposite directions
    /// (index below, pinky above the middle/ring pair) compounds it into a
    /// single severe spike rather than a graded cost — there's no "a little"
    /// version of this shape.
    /// </summary>
    private static double TendonInterdependencePenalty(ImmutableArray<FingerAssignment> assignments)
    {
        if (assignments.IsDefaultOrEmpty) { return 0.0; }

        var middle = assignments.FirstOrDefault(a => a.Finger == Fretboard.Finger.Middle);
        var ring = assignments.FirstOrDefault(a => a.Finger == Fretboard.Finger.Ring);
        var index = assignments.FirstOrDefault(a => a.Finger == Fretboard.Finger.Index);
        var pinky = assignments.FirstOrDefault(a => a.Finger == Fretboard.Finger.Pinky);
        if (middle is null || ring is null || index is null || pinky is null) { return 0.0; }

        var adjacentStrings = Math.Abs(middle.String - ring.String) == 1;
        var differentFrets = middle.Fret != ring.Fret;
        if (!adjacentStrings || !differentFrets) { return 0.0; }

        var innerLow = Math.Min(middle.Fret, ring.Fret);
        var innerHigh = Math.Max(middle.Fret, ring.Fret);
        var oppositeStretch = index.Fret < innerLow && pinky.Fret > innerHigh;

        return oppositeStretch ? TendonInterdependenceSpike : 0.0;
    }

    /// <summary>
    /// Rule 3 — Inner-String Clearance &amp; Joint Collapsing (the arch
    /// penalty). A sounded-but-open string sandwiched between two fretted
    /// neighbours (fret the G and high E, leave the B ringing) needs the
    /// fretting fingers to arch clear of it at the proximal interphalangeal
    /// joint — the joint that flexes most naturally, so holding it extended
    /// mid-chord is a genuinely different ask than muting or a flat barre.
    /// Cost scales with the voicing's overall span: fingers already braced
    /// for a wide lateral stretch have less articulation left to spare for
    /// the arch, so the same open-string gap is worse in a wide shape than
    /// a narrow one.
    /// <para><paramref name="positions"/> must be ordered by string index.</para>
    /// </summary>
    private static double ArchPenalty(ImmutableArray<FretPosition> positions, int span, int maxSpan)
    {
        if (positions.IsDefaultOrEmpty) { return 0.0; }

        var isolatedOpens = 0;
        for (var i = 1; i < positions.Length - 1; i++)
        {
            if (!positions[i].Open) { continue; }
            var before = positions[i - 1];
            var after = positions[i + 1];
            if (before.Fret is > 0 && after.Fret is > 0) { isolatedOpens++; }
        }
        if (isolatedOpens == 0) { return 0.0; }

        var lateralTension = 1.0 + Math.Clamp((double)span / Math.Max(1, maxSpan), 0.0, 1.0);
        return ArchBaseCost * isolatedOpens * lateralTension;
    }

    /// <summary>
    /// Rule 4 — Abduction &amp; Fret Spacing Scale. The flat linear span term
    /// treats a 3-fret cascade across adjacent strings as the baseline cost
    /// of a wide shape. Skipping over a string entirely — the next fretted
    /// finger landing on a non-adjacent string — asks the hand to abduct
    /// (spread sideways) rather than just reach forward, and that gets
    /// harder <em>exponentially</em>, not linearly, as the fret distance
    /// between the two fingers grows: a one-fret gap across a skip is barely
    /// noticeable, a five-fret gap across the same skip is a very different
    /// shape.
    /// </summary>
    private static double AbductionPenalty(ImmutableArray<FingerAssignment> assignments)
    {
        if (assignments.IsDefaultOrEmpty || assignments.Length < 2) { return 0.0; }

        var ordered = assignments.OrderBy(a => a.String).ToArray();
        var total = 0.0;
        for (var i = 1; i < ordered.Length; i++)
        {
            var prev = ordered[i - 1];
            var next = ordered[i];
            var stringGap = next.String - prev.String;
            if (stringGap <= 1) { continue; }

            var fretDistance = Math.Abs(next.Fret - prev.Fret);
            total += AbductionScale * (Math.Pow(AbductionExponentBase, fretDistance) - 1.0);
        }
        return total;
    }

    /// <summary>
    /// v0.1 comfort score (D15 / design.md §7 step 8), extended with four
    /// hand-biomechanics penalties (v0.5.3): reverse-stretch hand angle,
    /// middle/ring tendon interdependence, inner-string arch, and lateral
    /// abduction across skipped strings. Reported, not used for ordering —
    /// emission order is deterministic per D5.
    /// </summary>
    /// <param name="interiorMutedStrings">
    /// Muted strings that are <em>not</em> part of an edge run — i.e. total
    /// mutes minus <see cref="CountEdgeMutes"/>. Edge mutes are excluded
    /// deliberately: an unstruck string is free.
    /// </param>
    /// <param name="assignments">
    /// Finger-to-string/fret assignments driving Rules 1, 2, and 4. Defaults
    /// to empty (no penalty) for callers that only have the scalar summary.
    /// </param>
    /// <param name="orderedPositions">
    /// Full per-string positions, ordered by string index, driving Rule 3.
    /// Defaults to empty (no penalty) for callers without the full shape.
    /// </param>
    public static double ComputeComfort(
        int span,
        int interiorMutedStrings,
        int isolatedMutedStrings,
        ImmutableArray<BarreGroup> barres,
        int lowestFret,
        int maxSpan,
        int maxFret,
        ImmutableArray<FingerAssignment> assignments = default,
        ImmutableArray<FretPosition> orderedPositions = default)
    {
        var barreCost = 0.0;
        if (!barres.IsDefault)
        {
            foreach (var barre in barres) { barreCost += BarrePenalty(barre, maxFret); }
        }

        var raw = 1.0
            - (0.40 * ((double)span / Math.Max(1, maxSpan)))
            - (0.10 * interiorMutedStrings)
            - (0.15 * isolatedMutedStrings)
            - barreCost
            - (0.05 * ((double)lowestFret / Math.Max(1, maxFret)))
            - HandAnglePenalty(assignments)
            - TendonInterdependencePenalty(assignments)
            - ArchPenalty(orderedPositions, span, maxSpan)
            - AbductionPenalty(assignments);
        return Math.Clamp(raw, 0.0, 1.0);
    }
}
