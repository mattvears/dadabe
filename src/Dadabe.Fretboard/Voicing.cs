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

    /// <summary>
    /// v0.1 comfort score (D15 / design.md §7 step 8). Reported, not used
    /// for ordering — emission order is deterministic per D5.
    /// </summary>
    public static double ComputeComfort(
        int span,
        int mutedStrings,
        int barreCount,
        int lowestFret,
        int maxSpan,
        int maxFret)
    {
        var raw = 1.0
            - (0.40 * ((double)span / Math.Max(1, maxSpan)))
            - (0.10 * mutedStrings)
            - (0.15 * barreCount)
            - (0.05 * ((double)lowestFret / Math.Max(1, maxFret)));
        return Math.Clamp(raw, 0.0, 1.0);
    }
}
