using System.Collections.Immutable;
using Dadabe.Core.Memo;

namespace Dadabe.Fretboard;

/// <summary>
/// The four fretting fingers plus the thumb. Open strings use
/// <c>null</c>; this enum is the closed set of human fingers used for
/// fretting (D11/D21).
/// </summary>
public enum Finger
{
    Index = 1,
    Middle = 2,
    Ring = 3,
    Pinky = 4,
    Thumb = 5,
}

public sealed record ThumbPolicy(bool Allowed, int MaxFret, int String)
{
    public static ThumbPolicy DefaultOff => new(Allowed: false, MaxFret: 5, String: 0);
}

/// <summary>
/// Parameters defining what a single human hand can play (D11). The
/// default profile is the only one shipped in v0.1; CLI flags can
/// override individual values. The resolved instance is echoed into the
/// JSON envelope so a run reproduces (D14).
/// </summary>
public sealed record HandModel : IContentHashable
{
    public const string DefaultProfileName = "Default";

    public string Name { get; }
    public int MaxFret { get; }
    public int MaxSpan { get; }
    public int MinStrings { get; }
    public int MaxStrings { get; }
    public ImmutableDictionary<FingerPair, int> Stretch { get; }
    public ThumbPolicy Thumb { get; }
    public int MaxBarres { get; }

    public HandModel(
        string name,
        int maxFret,
        int maxSpan,
        int minStrings,
        int maxStrings,
        ImmutableDictionary<FingerPair, int> stretch,
        ThumbPolicy thumb,
        int maxBarres)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(stretch);
        ArgumentNullException.ThrowIfNull(thumb);
        Name = name;
        MaxFret = maxFret;
        MaxSpan = maxSpan;
        MinStrings = minStrings;
        MaxStrings = maxStrings;
        Stretch = stretch;
        Thumb = thumb;
        MaxBarres = maxBarres;
    }

    /// <summary>D11 defaults: 15 frets, span 4, strings 3..6, stretch matrix as documented.</summary>
    public static HandModel Default { get; } = new(
        DefaultProfileName,
        maxFret: 15,
        maxSpan: 4,
        minStrings: 3,
        maxStrings: 6,
        stretch: ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<FingerPair, int>(new FingerPair(Finger.Index,  Finger.Middle), 2),
            new KeyValuePair<FingerPair, int>(new FingerPair(Finger.Middle, Finger.Ring),   2),
            new KeyValuePair<FingerPair, int>(new FingerPair(Finger.Ring,   Finger.Pinky),  2),
            new KeyValuePair<FingerPair, int>(new FingerPair(Finger.Index,  Finger.Ring),   3),
            new KeyValuePair<FingerPair, int>(new FingerPair(Finger.Middle, Finger.Pinky),  3),
            new KeyValuePair<FingerPair, int>(new FingerPair(Finger.Index,  Finger.Pinky),  4),
        }),
        thumb: ThumbPolicy.DefaultOff,
        maxBarres: 1);

    public int StretchBetween(Finger a, Finger b)
    {
        if (a == b) { return 0; }
        var pair = a < b ? new FingerPair(a, b) : new FingerPair(b, a);
        if (Stretch.TryGetValue(pair, out var v)) { return v; }
        throw new InvalidOperationException($"No stretch entry for {pair}.");
    }

    public ContentHash ContentHash
    {
        get
        {
            var c = new Canonical()
                .Utf8(Name)
                .U8((byte)MaxFret)
                .U8((byte)MaxSpan)
                .U8((byte)MinStrings)
                .U8((byte)MaxStrings)
                .U8((byte)MaxBarres)
                .U8(Thumb.Allowed ? (byte)1 : (byte)0)
                .U8((byte)Thumb.MaxFret)
                .U8((byte)Thumb.String);
            foreach (var pair in OrderedPairs)
            {
                c.U8((byte)pair.Low).U8((byte)pair.High).U8((byte)StretchBetween(pair.Low, pair.High));
            }
            return ContentHash.FromCanonical(Namespaces.HandModel, 1, c.AsSpan());
        }
    }

    private static readonly FingerPair[] OrderedPairs =
    {
        new(Finger.Index,  Finger.Middle),
        new(Finger.Index,  Finger.Ring),
        new(Finger.Index,  Finger.Pinky),
        new(Finger.Middle, Finger.Ring),
        new(Finger.Middle, Finger.Pinky),
        new(Finger.Ring,   Finger.Pinky),
    };
}

/// <summary>Unordered pair (<c>Low &lt; High</c>) for stretch-matrix lookups.</summary>
public readonly record struct FingerPair(Finger Low, Finger High);
