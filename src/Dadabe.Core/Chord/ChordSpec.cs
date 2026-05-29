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
public sealed record ChordSpec(
    Note Root,
    string Quality,
    ImmutableArray<ChordTone> Tones,
    ImmutableArray<string> Required) : IContentHashable
{
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
            return Memo.ContentHash.FromCanonical(Namespaces.ChordSpec, 1, c.AsSpan());
        }
    }
}
