using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Dadabe.Cli.Io;

/// <summary>The shared envelope around every subcommand's payload (design.md §5).</summary>
public sealed record Envelope<TData>(
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("input")] InputDto Input,
    [property: JsonPropertyName("data")] TData Data,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings);

public sealed record InputDto(
    [property: JsonPropertyName("chord"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Chord,
    [property: JsonPropertyName("tuning"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Tuning,
    [property: JsonPropertyName("handModel"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    HandModelDto? HandModel);

public sealed record HandModelDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("stretch")] IReadOnlyDictionary<string, int> Stretch,
    [property: JsonPropertyName("thumb")] ThumbDto Thumb,
    [property: JsonPropertyName("maxBarres")] int MaxBarres);

public sealed record ThumbDto(
    [property: JsonPropertyName("allowed")] bool Allowed,
    [property: JsonPropertyName("maxFret")] int MaxFret,
    [property: JsonPropertyName("string")] int String);

public sealed record VoicingsPayload(
    [property: JsonPropertyName("chord")] ChordDto Chord,
    [property: JsonPropertyName("tuning")] TuningDto Tuning,
    [property: JsonPropertyName("voicings")] IReadOnlyList<VoicingDto> Voicings,
    [property: JsonPropertyName("nextChords")] IReadOnlyList<NextChordDto> NextChords);

public sealed record NextChordDto(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("probability")] double Probability);

public sealed record ChordDto(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("root")] string Root,
    [property: JsonPropertyName("quality")] string Quality,
    [property: JsonPropertyName("pitchClasses")] IReadOnlyList<ChordPcDto> PitchClasses,
    [property: JsonPropertyName("bassNote"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BassNote = null);

public sealed record ChordPcDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("function")] string Function);

public sealed record TuningDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("strings")] IReadOnlyList<string> Strings);

public sealed record VoicingDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("structure")] string Structure,
    [property: JsonPropertyName("functions")] IReadOnlyList<string> Functions,
    [property: JsonPropertyName("positions")] IReadOnlyList<PositionDto> Positions,
    [property: JsonPropertyName("span")] int Span,
    [property: JsonPropertyName("lowestFret")] int LowestFret,
    [property: JsonPropertyName("highestFret")] int HighestFret,
    [property: JsonPropertyName("barres")] IReadOnlyList<BarreDto> Barres,
    [property: JsonPropertyName("bassNote"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BassNote,
    [property: JsonPropertyName("topNote"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TopNote,
    [property: JsonPropertyName("openStrings")] int OpenStrings,
    [property: JsonPropertyName("mutedStrings")] int MutedStrings,
    [property: JsonPropertyName("comfort")] double Comfort,
    [property: JsonPropertyName("fingering")] FingeringDto Fingering);

public sealed record PositionDto(
    [property: JsonPropertyName("string")] int String,
    [property: JsonPropertyName("fret")] int? Fret,
    [property: JsonPropertyName("note"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Note,
    [property: JsonPropertyName("function"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Function,
    [property: JsonPropertyName("muted"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Muted,
    [property: JsonPropertyName("voice"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Voice);

public sealed record BarreDto(
    [property: JsonPropertyName("finger")] int Finger,
    [property: JsonPropertyName("fret")] int Fret,
    [property: JsonPropertyName("lowString")] int LowString,
    [property: JsonPropertyName("highString")] int HighString);

public sealed record FingeringDto(
    [property: JsonPropertyName("assignments")] IReadOnlyList<AssignmentDto> Assignments,
    [property: JsonPropertyName("barres")] IReadOnlyList<BarreDto> Barres,
    [property: JsonPropertyName("mutes")] IReadOnlyList<MuteDto> Mutes);

public sealed record AssignmentDto(
    [property: JsonPropertyName("string")] int String,
    [property: JsonPropertyName("fret")] int Fret,
    [property: JsonPropertyName("finger")] int? Finger);

public sealed record MuteDto(
    [property: JsonPropertyName("string")] int String,
    [property: JsonPropertyName("source")] string Source);

/// <summary>Payload for the <c>tuning</c> subcommand: just the resolved strings.</summary>
public sealed record TuningPayload(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("strings")] IReadOnlyList<string> Strings);

/// <summary>Payload for the <c>chord</c> subcommand.</summary>
public sealed record ChordPayload(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("root")] string Root,
    [property: JsonPropertyName("quality")] string Quality,
    [property: JsonPropertyName("extensions")] IReadOnlyList<string> Extensions,
    [property: JsonPropertyName("alterations")] IReadOnlyList<string> Alterations,
    [property: JsonPropertyName("pitchClasses")] IReadOnlyList<ChordPcDto> PitchClasses,
    [property: JsonPropertyName("required")] IReadOnlyList<string> Required,
    [property: JsonPropertyName("bassNote"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BassNote = null);

/// <summary>Payload for the <c>voice-lead</c> subcommand.</summary>
public sealed record VoiceLeadingResultDto(
    [property: JsonPropertyName("chords")]
    IReadOnlyList<string> Chords,
    [property: JsonPropertyName("tuning")]
    string Tuning,
    [property: JsonPropertyName("solutions")]
    IReadOnlyList<VoiceLeadingSolutionDto> Solutions);

public sealed record VoiceLeadingSolutionDto(
    [property: JsonPropertyName("totalDistance")]
    int TotalDistance,
    [property: JsonPropertyName("steps")]
    IReadOnlyList<VoiceLeadingStepDto> Steps);

public sealed record VoiceLeadingStepDto(
    [property: JsonPropertyName("chord")]
    string Chord,
    [property: JsonPropertyName("voicing")]
    VoicingDto Voicing,
    [property: JsonPropertyName("transition"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    VoiceLeadingTransitionDto? Transition);

public sealed record VoiceLeadingTransitionDto(
    [property: JsonPropertyName("totalDistance")]
    int TotalDistance,
    [property: JsonPropertyName("moves")]
    IReadOnlyList<VoiceMoveDto> Moves);

public sealed record VoiceMoveDto(
    [property: JsonPropertyName("string")]
    int String,
    [property: JsonPropertyName("from")]
    int? From,
    [property: JsonPropertyName("to")]
    int? To,
    [property: JsonPropertyName("distance")]
    int Distance);

/// <summary>Payload for the <c>predict</c> subcommand.</summary>
public sealed record PredictionResultDto(
    [property: JsonPropertyName("results")]
    IReadOnlyList<PredictionCandidateDto> Results,
    [property: JsonPropertyName("metadata"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PredictionMetadataDto? Metadata);

public sealed record PredictionCandidateDto(
    [property: JsonPropertyName("chord")]   string Chord,
    [property: JsonPropertyName("score")]   double Score,
    [property: JsonPropertyName("reasons"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Reasons);

public sealed record PredictionMetadataDto(
    [property: JsonPropertyName("contextUsed"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ProgressionDto? ContextUsed,
    [property: JsonPropertyName("filtersApplied"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonObject? FiltersApplied);

/// <summary>Payload for the <c>transform</c> subcommand.</summary>
public sealed record TransformResultDto(
    [property: JsonPropertyName("source")]      IReadOnlyList<string> Source,
    [property: JsonPropertyName("results")]     IReadOnlyList<TransformVariantDto> Results);

public sealed record TransformVariantDto(
    [property: JsonPropertyName("chain")]       IReadOnlyList<TransformStepDto> Chain,
    [property: JsonPropertyName("chords")]      IReadOnlyList<string> Chords,
    [property: JsonPropertyName("playability"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                                                PlayabilityDto? Playability,
    [property: JsonPropertyName("keyBefore"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                                                string? KeyBefore,
    [property: JsonPropertyName("keyAfter"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                                                string? KeyAfter,
    [property: JsonPropertyName("notes")]       IReadOnlyList<TransformNoteDto> Notes);

public sealed record TransformStepDto(
    [property: JsonPropertyName("type")]        string Type,
    [property: JsonPropertyName("params")]      IReadOnlyDictionary<string, string> Params);

public sealed record PlayabilityDto(
    [property: JsonPropertyName("worstComfort")]  int WorstComfort,
    [property: JsonPropertyName("totalDistance")] int TotalDistance,
    [property: JsonPropertyName("minFret")]       int MinFret,
    [property: JsonPropertyName("maxFret")]       int MaxFret,
    [property: JsonPropertyName("unplayable")]    IReadOnlyList<string> Unplayable);

public sealed record TransformNoteDto(
    [property: JsonPropertyName("index"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                                                int? Index,
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("message")]     string Message);
