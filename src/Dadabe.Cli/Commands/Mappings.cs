using System.Collections.Immutable;
using Dadabe.Cli.Io;
using Dadabe.Core;
using Dadabe.Core.Chord;
using Dadabe.Fretboard;

namespace Dadabe.Cli.Commands;

/// <summary>Domain → DTO conversions for the JSON envelope.</summary>
internal static class Mappings
{
    public static string MuteSourceName(MuteSource source) => source switch
    {
        MuteSource.AdjacentUnderside => "adjacent-underside",
        MuteSource.BarreExtended => "barre-extended",
        MuteSource.ThumbWrap => "thumb-wrap",
        MuteSource.OuterHand => "outer-hand",
        MuteSource.Unfretted => "unfretted",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };

    public static MuteSource ParseMuteSourceName(string name) => name switch
    {
        "adjacent-underside" => MuteSource.AdjacentUnderside,
        "barre-extended" => MuteSource.BarreExtended,
        "thumb-wrap" => MuteSource.ThumbWrap,
        "outer-hand" => MuteSource.OuterHand,
        "unfretted" => MuteSource.Unfretted,
        _ => throw new FormatException($"Unknown mute source name '{name}'."),
    };

    public static TuningDto ToDto(Tuning tuning) => new(
        tuning.Name,
        tuning.Strings.Select(p => p.ToString()).ToList());

    public static ChordDto ToDto(ChordSpec spec, ChordSymbol symbol, string symbolText) => new(
        Symbol: symbolText,
        Root: spec.Root.ToString(),
        Quality: spec.Quality,
        PitchClasses: spec.Tones.Select(t => new ChordPcDto(t.Note.ToString(), t.Function)).ToList(),
        BassNote: symbol.Bass?.ToString());

    public static VoicingDto ToDto(Voicing v)
    {
        var positions = v.Positions
            .OrderBy(p => p.String)
            .Select(p => new PositionDto(
                String: p.String,
                Fret: p.Fret,
                Note: p.SoundingPitch?.ToString(),
                Function: p.Function,
                Muted: p.Muted ? true : null,
                Voice: p.Voice))
            .ToList();

        var barres = v.Fingering.Barres
            .Select(b => new BarreDto((int)b.Finger, b.Fret, b.LowStringInclusive, b.HighStringInclusive))
            .ToList();

        var fingering = new FingeringDto(
            Assignments: v.Fingering.Assignments
                .OrderBy(a => a.String)
                .Select(a => new AssignmentDto(a.String, a.Fret, a.Finger is null ? null : (int)a.Finger))
                .ToList(),
            Barres: barres,
            Mutes: v.Fingering.Mutes
                .OrderBy(m => m.String)
                .Select(m => new MuteDto(m.String, MuteSourceName(m.Source)))
                .ToList());

        return new VoicingDto(
            Id: v.ContentHash.ToString(),
            Structure: v.Structure,
            Functions: v.Functions,
            Positions: positions,
            Span: v.Span,
            LowestFret: v.LowestFret ?? 0,
            HighestFret: v.HighestFret ?? 0,
            Barres: barres,
            BassNote: v.BassNote?.ToString(),
            TopNote: v.TopNote?.ToString(),
            OpenStrings: v.OpenStrings,
            MutedStrings: v.MutedStrings,
            Comfort: Math.Round(v.Comfort, 4),
            Fingering: fingering);
    }

    public static ChordPayload ToChordPayload(ChordSpec spec, ChordSymbol symbol, string symbolText) => new(
        Symbol: symbolText,
        Root: spec.Root.ToString(),
        Quality: spec.Quality,
        Extensions: symbol.Extensions.ToList(),
        Alterations: symbol.Alterations.ToList(),
        PitchClasses: spec.Tones.Select(t => new ChordPcDto(t.Note.ToString(), t.Function)).ToList(),
        Required: spec.Required.ToList(),
        BassNote: symbol.Bass?.ToString());

    public static TuningPayload ToTuningPayload(Tuning tuning) => new(
        Name: tuning.Name,
        Strings: tuning.Strings.Select(p => p.ToString()).ToList());
}
