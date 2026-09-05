using System.Collections.Immutable;
using Dadabe.Fretboard;

namespace Dadabe.Editor.Services;

/// <summary>
/// Merges a song's hand constraints (D42) over <see cref="HandModel.Default"/> —
/// null fields fall back to the default, mirroring the advanced options
/// already on the voicings form (D36).
/// </summary>
public static class SongHandResolver
{
    public static HandModel ResolveHandModel(SongHand? hand)
    {
        var d = HandModel.Default;
        if (hand is null) { return d; }
        return new HandModel(
            name: d.Name,
            maxFret: hand.Frets ?? d.MaxFret,
            maxSpan: hand.Span ?? d.MaxSpan,
            minStrings: hand.MinStrings ?? d.MinStrings,
            maxStrings: hand.MaxStrings ?? d.MaxStrings,
            stretch: d.Stretch,
            thumb: hand.AllowThumb is { } allowThumb ? d.Thumb with { Allowed = allowThumb } : d.Thumb,
            maxBarres: hand.AllowBarre is false ? 0 : d.MaxBarres);
    }

    public static SearchParams ResolveSearchParams(SongHand? hand)
    {
        var d = SearchParams.Default;
        if (hand is null) { return d; }
        return new SearchParams(
            MaxFret: hand.Frets ?? d.MaxFret,
            MaxSpan: hand.Span ?? d.MaxSpan,
            MinStrings: hand.MinStrings ?? d.MinStrings,
            MaxStrings: hand.MaxStrings ?? d.MaxStrings,
            AllowOpen: hand.AllowOpen ?? d.AllowOpen,
            AllowBarre: hand.AllowBarre ?? d.AllowBarre,
            AllowThumb: hand.AllowThumb ?? d.AllowThumb,
            Categories: hand.Categories is { Count: > 0 } cats ? [.. cats] : d.Categories,
            RequireRoot: hand.RequireRoot ?? d.RequireRoot);
    }
}
