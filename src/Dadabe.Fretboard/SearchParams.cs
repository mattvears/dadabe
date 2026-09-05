using System.Collections.Immutable;
using Dadabe.Core.Memo;

namespace Dadabe.Fretboard;

/// <summary>
/// Per-call search controls (D18 / §8.2). Excludes <c>--limit</c>: limit
/// is applied at read time so a memo entry serves any limit.
/// </summary>
public sealed record SearchParams(
    int MaxFret,
    int MaxSpan,
    int MinStrings,
    int MaxStrings,
    bool AllowOpen,
    bool AllowBarre,
    bool AllowThumb,
    ImmutableArray<string> Categories,
    bool RequireRoot = false) : IContentHashable
{
    /// <summary>Defaults matching CLI defaults in design.md §3.</summary>
    public static SearchParams Default => new(
        MaxFret: 15,
        MaxSpan: 4,
        MinStrings: 3,
        MaxStrings: 6,
        AllowOpen: true,
        AllowBarre: true,
        AllowThumb: false,
        Categories: ImmutableArray<string>.Empty,
        RequireRoot: false);

    public ContentHash ContentHash
    {
        get
        {
            var c = new Canonical()
                .U8((byte)MaxFret)
                .U8((byte)MaxSpan)
                .U8((byte)MinStrings)
                .U8((byte)MaxStrings)
                .U8(AllowOpen ? (byte)1 : (byte)0)
                .U8(AllowBarre ? (byte)1 : (byte)0)
                .U8(AllowThumb ? (byte)1 : (byte)0)
                .U8(RequireRoot ? (byte)1 : (byte)0)
                .U16BE((ushort)Categories.Length);
            foreach (var cat in Categories.OrderBy(s => s, StringComparer.Ordinal))
            {
                c.Utf8(cat);
            }
            return ContentHash.FromCanonical("search-params", 1, c.AsSpan());
        }
    }
}
