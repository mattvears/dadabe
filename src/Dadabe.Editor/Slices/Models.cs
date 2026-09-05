using Dadabe.Editor.Services;

namespace Dadabe.Editor.Slices;

public sealed record DashboardModel(
    int ProgressionCount,
    int PredictionCount,
    int TuningCount,
    int ReferenceCount);

public sealed record PredictionsIndexModel(
    IReadOnlyList<PredictionModel> Predictions);

public sealed record PredictionFormModel(
    PredictionModel? Prediction,
    IReadOnlyList<ProgressionModel> Progressions);

public sealed record PredictionResultModel(
    string Chord,
    IReadOnlyList<PredictionCandidate> Results,
    string? Error,
    string? ContextName = null,
    IReadOnlyList<string>? AvailableTunings = null,
    IReadOnlyList<string>? ContextChords = null)
{
    public static PredictionResultModel FromError(string chord, string error) =>
        new(chord, [], error);
}

public sealed record VoicingFragmentModel(
    string Chord,
    string TuningName,
    IReadOnlyList<VoicingFragmentRow> Voicings,
    string? Error);

public sealed record VoicingFragmentRow(
    string AsciiNotation,
    string Structure,
    int ComfortPct);

public sealed record PredictionCandidate(string Chord, double Score);

public sealed record ReferenceIndexModel(
    string Tab,
    IReadOnlyList<CadenceModel> Cadences,
    IReadOnlyList<ModeModel> Modes,
    IReadOnlyList<ScaleModel> Scales,
    string? VoicingCategoriesJson);

public sealed record VoicingsIndexModel(
    IReadOnlyList<string> EmbeddedTunings,
    IReadOnlyList<TuningModel> SavedTunings);

/// <param name="Notice">
/// Non-fatal message shown above the results — the request was valid but
/// produced nothing, e.g. a slash chord whose bass is unplayable on this
/// tuning. Distinct from <paramref name="Error"/>, which means the request
/// itself was rejected.
/// </param>
/// <param name="TuningValue">
/// The raw tuning the search ran with (catalog name or comma spec), as opposed
/// to the display label in <paramref name="TuningName"/>. Result cards stack,
/// so the "next chords" drill-down embeds this rather than re-reading the form,
/// which may have moved on to a different tuning.
/// </param>
public sealed record VoicingResultModel(
    string Chord,
    string TuningName,
    int TotalCount,
    IReadOnlyList<VoicingRow> Voicings,
    IReadOnlyList<NextChordEntry> NextChords,
    string? Error,
    string? Notice = null,
    string? TuningValue = null)
{
    public static VoicingResultModel FromError(string chord, string tuning, string error) =>
        new(chord, tuning, 0, [], [], error);
}

public sealed record VoicingRow(
    int Index,
    string Structure,
    int ComfortPct,
    string AsciiNotation,
    string Notes,
    string Intervals,
    ChordDiagram Diagram);

public sealed record ChordDiagram(
    IReadOnlyList<ChordDiagramString> Strings,
    int StartFret,
    int NumFrets);

public sealed record ChordDiagramString(
    string Name,
    bool Muted,
    bool Open,
    int? FrettedAt);

public sealed record NextChordEntry(string Symbol, double Probability);

public sealed record VoiceLeadIndexModel(
    IReadOnlyList<string> EmbeddedTunings,
    IReadOnlyList<TuningModel> SavedTunings);

public sealed record VoiceLeadResultModel(
    string Chords,
    string TuningName,
    IReadOnlyList<VoiceLeadSolutionRow> Solutions,
    string? Error,
    int StringCount = 6)
{
    public static VoiceLeadResultModel FromError(string chords, string tuning, string error) =>
        new(chords, tuning, [], error);
}

public sealed record VoiceLeadSolutionRow(
    int SolutionNumber,
    int TotalDistance,
    IReadOnlyList<VoiceLeadStepRow> Steps);

public sealed record VoiceLeadStepRow(
    string Chord,
    string AsciiNotation,
    string Structure,
    int ComfortPct,
    int? TransitionDistance,
    IReadOnlyList<VoiceMoveRow>? Moves = null);

/// <summary>Per-string transition detail (v0.5.2 §10) — "only the B string moves, 2 frets".</summary>
public sealed record VoiceMoveRow(int String, int? FromFret, int? ToFret, int Distance);

public sealed record SongsFormModel(
    SongModel? Song,
    IReadOnlyList<string> EmbeddedTunings,
    IReadOnlyList<TuningModel> SavedTunings);

public sealed record SongDetailModel(
    SongModel Song,
    string? KeyDisplay,
    IReadOnlyList<SectionViewModel> Sections,
    IReadOnlyList<ProgressionModel> SavedProgressions,
    string? Error = null);

public sealed record SectionViewModel(
    SongSection Section,
    IReadOnlyList<SlotViewModel> Slots,
    string? PositionRange,
    string? HardestChordLabel,
    int PinnedCount);

public sealed record SlotViewModel(
    int Index,
    SongSlot Slot,
    ChordDiagram? Diagram,
    bool Stale);

public sealed record SongChartModel(
    SongModel Song,
    string? KeyDisplay,
    IReadOnlyList<SectionViewModel> Sections);

public sealed record TransformPanelModel(ProgressionModel Progression);

public sealed record TransformResultViewModel(
    string SourceSlug,
    IReadOnlyList<DiffChordRow> Diff,
    string? KeyBefore,
    string? KeyAfter,
    PlayabilityViewModel? Playability,
    IReadOnlyList<TransformNoteRow> Notes,
    IReadOnlyList<(string Type, string ParamsRaw)> Chain,
    string? Error);

public sealed record DiffChordRow(int Index, string Original, string Result, bool Changed);

public sealed record TransformNoteRow(int? Index, string Kind, string Message);

public sealed record PlayabilityViewModel(int WorstComfortPct, int TotalDistance, int MinFret, int MaxFret, IReadOnlyList<string> Unplayable);

public sealed record TransformExploreModel(
    string SourceSlug,
    IReadOnlyList<TransformExploreRow> Results);

public sealed record TransformExploreRow(
    string Type, string DisplayName, IReadOnlyList<string> Chords,
    PlayabilityViewModel? Playability, int NoteCount);
