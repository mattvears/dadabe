using Dadabe.Core;

namespace Dadabe.Fretboard;

/// <summary>
/// One per-string slot in a voicing. <see cref="Fret"/> is <c>null</c> iff
/// the string is muted; otherwise all four spelling/sounding fields are
/// populated. <see cref="Function"/> is the chord-tone this position
/// satisfies in its voicing's chord context (e.g. <c>"3"</c>, <c>"b7"</c>),
/// or <c>null</c> when muted (D12, §4).
/// </summary>
/// <summary>
/// <see cref="Voice"/> is null in v1; v2 inner-line generation will assign
/// a stable voice index (0 = bass, ascending) per E3/D23.
/// </summary>
public sealed record FretPosition(
    int String,
    int? Fret,
    Pitch? SoundingPitch,
    Note? DisplayNote,
    string? Function,
    int? Voice = null)
{
    public bool Muted => Fret is null;

    public bool Open => Fret == 0;

    public static FretPosition Mute(int @string) => new(@string, null, null, null, null);
}

/// <summary>
/// Chord-agnostic location on the fretboard. Used by
/// <see cref="FretLayout"/> and <see cref="Reachability"/> before a chord
/// context is layered on.
/// </summary>
public readonly record struct BoardPosition(int String, int Fret, Pitch Pitch);
