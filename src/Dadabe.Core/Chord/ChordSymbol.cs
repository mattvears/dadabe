using System.Collections.Immutable;
using System.Text;
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

    // Grammar-declared order (ChordGrammar.json "modifiers"), not input order — normalising
    // is the point: C7#9b13 and C7b13#9 both emit as C7#9b13 (D44).
    private static readonly ImmutableArray<string> ExtensionOrder = ["add9", "add11", "add13"];
    private static readonly ImmutableArray<string> AlterationOrder = ["b5", "#5", "b9", "#9", "#11", "b13"];

    /// <summary>
    /// Renders this symbol back to a string the parser accepts:
    /// Root + Quality + Extensions + Alterations + ("/" + Bass), each group in
    /// canonical grammar order. The round-trip identity is over parsed
    /// values (<c>parse(format(parse(s))) == parse(s)</c>), not over the
    /// original text — <c>C-</c> emits as <c>Cm</c>.
    /// </summary>
    public string ToSymbol()
    {
        var sb = new StringBuilder();
        sb.Append(Root).Append(Quality);
        foreach (var ext in ExtensionOrder)
        {
            if (Extensions.Contains(ext)) { sb.Append(ext); }
        }
        foreach (var alt in AlterationOrder)
        {
            if (Alterations.Contains(alt)) { sb.Append(alt); }
        }
        if (Bass is { } bass) { sb.Append('/').Append(bass); }
        return sb.ToString();
    }
}
