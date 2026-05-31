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
    string? Error)
{
    public static PredictionResultModel FromError(string chord, string error) =>
        new(chord, [], error);
}

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

public sealed record VoicingResultModel(
    string Chord,
    string TuningName,
    int TotalCount,
    IReadOnlyList<VoicingRow> Voicings,
    IReadOnlyList<NextChordEntry> NextChords,
    string? Error)
{
    public static VoicingResultModel FromError(string chord, string tuning, string error) =>
        new(chord, tuning, 0, [], [], error);
}

public sealed record VoicingRow(
    int Index,
    string Structure,
    int ComfortPct,
    string Positions);

public sealed record NextChordEntry(string Symbol, double Probability);
