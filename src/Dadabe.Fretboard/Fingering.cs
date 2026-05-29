using System.Collections.Immutable;
using Dadabe.Core.Memo;

namespace Dadabe.Fretboard;

/// <summary>
/// Closed enumeration of legal mute sources (D21). The legality rule for
/// each variant is enforced by the fingering solver; the schema enforces
/// the closed set on output.
/// </summary>
public enum MuteSource
{
    AdjacentUnderside,
    BarreExtended,
    ThumbWrap,
    OuterHand,
    Unfretted,
}

/// <summary>
/// One sounded string in a fingering: <c>Finger</c> is <c>null</c> iff the
/// string is open (<c>Fret == 0</c>); never null when <c>Fret &gt; 0</c>.
/// </summary>
public sealed record FingerAssignment(int String, int Fret, Finger? Finger);

/// <summary>
/// A barre: one finger flattened across the strings in <c>[Low, High]</c>
/// at <c>Fret</c>. Strings outside the range may still be sounded normally.
/// </summary>
public sealed record BarreGroup(Finger Finger, int Fret, int LowStringInclusive, int HighStringInclusive);

public sealed record MuteAssignment(int String, MuteSource Source);

/// <summary>
/// A concrete assignment of fingers, barres, and mutes that realises a
/// candidate set of positions. A <c>Fingering</c> is *valid* iff all
/// eight rules in design.md §4 hold; the solver enforces them.
/// </summary>
public sealed record Fingering(
    ImmutableArray<FingerAssignment> Assignments,
    ImmutableArray<BarreGroup> Barres,
    ImmutableArray<MuteAssignment> Mutes) : IContentHashable
{
    public ContentHash ContentHash
    {
        get
        {
            // Identity = ordered (string, fret) positions + hand-model hash.
            // The hand-model hash gets folded in by the caller via
            // FingeringContentHash since Fingering itself doesn't reference
            // a HandModel.
            return FingeringContentHash.For(Assignments, Mutes, handModel: null);
        }
    }
}

/// <summary>
/// Helper that owns the canonical-byte layout for a fingering's content
/// hash. Callers with a <see cref="HandModel"/> in scope should pass it to
/// hash the (positions, hand-model) pair — that's the identity used by the
/// memo's <c>FingeringKey</c>.
/// </summary>
public static class FingeringContentHash
{
    public static ContentHash For(
        ImmutableArray<FingerAssignment> assignments,
        ImmutableArray<MuteAssignment> mutes,
        HandModel? handModel)
    {
        var positions = assignments
            .Select(a => (a.String, a.Fret))
            .Concat(mutes.Select(m => (m.String, Fret: -1)))
            .OrderBy(p => p.String)
            .ToArray();

        var c = new Canonical().U16BE((ushort)positions.Length);
        foreach (var (s, f) in positions)
        {
            c.U8((byte)s).I16BE((short)f);
        }
        if (handModel is not null)
        {
            c.HashDigest(handModel.ContentHash);
        }
        return ContentHash.FromCanonical(Namespaces.Fingering, 1, c.AsSpan());
    }
}
