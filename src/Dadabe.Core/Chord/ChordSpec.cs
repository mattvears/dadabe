using System.Collections.Immutable;
using Dadabe.Core.Memo;

namespace Dadabe.Core.Chord;

/// <summary>
/// One chord-tone projected onto the root: scale-degree function (e.g.
/// <c>b7</c>), exact semitone distance, the spelled <see cref="Note"/>
/// produced by the stacked-thirds letter walk (D12), and the derived
/// pitch class.
/// </summary>
public sealed record ChordTone(string Function, int Semitones, Note Note, PitchClass PitchClass);

/// <summary>
/// Chord-tone spec: the expansion of a <see cref="ChordSymbol"/> into its
/// tone set. Identity carries the spelled <see cref="Note"/> for every
/// tone (D17 / §8.1), so <c>F#maj7</c> and <c>Gbmaj7</c> hash distinctly.
/// </summary>
/// <param name="Bass">
/// Slash-chord bass note, or null for a root-position symbol. When set, a
/// voicing only satisfies this spec if its lowest sounding pitch has this
/// pitch class. A bass that is not already a chord tone is also appended to
/// <see cref="Tones"/> with the <see cref="BassFunction"/> label, so the
/// fretboard search can reach it at all.
/// </param>
public sealed record ChordSpec(
    Note Root,
    string Quality,
    ImmutableArray<ChordTone> Tones,
    ImmutableArray<string> Required,
    Note? Bass = null) : IContentHashable
{
    /// <summary>
    /// Function label for a slash bass that is foreign to the chord (the D of
    /// <c>C/D</c>). Deliberately not a scale-degree string: it must not be
    /// mistaken for a chord tone by the voicing classifier or the required-tone
    /// check. An inverted chord tone (the G of <c>C/G</c>) keeps its own
    /// function and never carries this label.
    /// </summary>
    public const string BassFunction = "bass";

    public IEnumerable<PitchClass> PitchClasses => Tones.Select(t => t.PitchClass);

    public ContentHash ContentHash
    {
        get
        {
            var c = new Canonical()
                .U8((byte)Root.Letter)
                .I8((sbyte)Root.Accidental)
                .Utf8(Quality)
                .U16BE((ushort)Tones.Length);
            foreach (var t in Tones)
            {
                c.Utf8(t.Function)
                 .U16BE((ushort)t.Semitones)
                 .U8((byte)t.Note.Letter)
                 .I8((sbyte)t.Note.Accidental)
                 .U8((byte)t.PitchClass.Value);
            }
            c.U16BE((ushort)Required.Length);
            foreach (var r in Required) { c.Utf8(r); }
            // Appended only when present, so every root-position spec keeps the
            // exact canonical bytes it had before slash chords existed and the
            // published voicing ids do not churn. Required is length-prefixed, so
            // the two extra bytes stay unambiguous.
            if (Bass is { } bass)
            {
                c.U8((byte)bass.Letter).I8((sbyte)bass.Accidental);
            }
            return Memo.ContentHash.FromCanonical(Namespaces.ChordSpec, 1, c.AsSpan());
        }
    }
}
