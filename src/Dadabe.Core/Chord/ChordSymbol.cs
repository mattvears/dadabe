using System.Collections.Immutable;
using Dadabe.Core.Memo;

namespace Dadabe.Core.Chord;

/// <summary>
/// Parsed result of a chord symbol string (e.g. <c>Cmaj7</c>, <c>F#m11</c>).
/// Root and bass keep the spelling the user typed — <c>F#maj7</c> and
/// <c>Gbmaj7</c> never collapse (D12).
/// </summary>
public sealed record ChordSymbol(
    Note Root,
    string Quality,
    ImmutableArray<string> Extensions,
    ImmutableArray<string> Alterations,
    Note? Bass) : IContentHashable
{
    public ContentHash ContentHash
    {
        get
        {
            var c = new Canonical()
                .U8((byte)Root.Letter)
                .I8((sbyte)Root.Accidental)
                .Utf8(Quality)
                .U16BE((ushort)Extensions.Length);
            foreach (var ext in Extensions) { c.Utf8(ext); }
            c.U16BE((ushort)Alterations.Length);
            foreach (var alt in Alterations) { c.Utf8(alt); }
            c.U8(Bass is null ? (byte)0 : (byte)1);
            if (Bass is not null)
            {
                c.U8((byte)Bass.Value.Letter).I8((sbyte)Bass.Value.Accidental);
            }
            return Memo.ContentHash.FromCanonical(Namespaces.ChordSymbol, 1, c.AsSpan());
        }
    }
}
