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

    /// <summary>
    /// v0.1 comfort score (D15 / design.md §7 step 8). Reported, not used
    /// for ordering — emission order is deterministic per D5.
    /// </summary>
    /// <param name="interiorMutedStrings">
    /// Muted strings that are <em>not</em> part of an edge run — i.e. total
    /// mutes minus <see cref="CountEdgeMutes"/>. Edge mutes are excluded
    /// deliberately: an unstruck string is free.
    /// </param>
    public static double ComputeComfort(
        int span,
        int interiorMutedStrings,
        int isolatedMutedStrings,
        ImmutableArray<BarreGroup> barres,
        int lowestFret,
        int maxSpan,
        int maxFret)
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
            - (0.05 * ((double)lowestFret / Math.Max(1, maxFret)));
        return Math.Clamp(raw, 0.0, 1.0);
    }
}
