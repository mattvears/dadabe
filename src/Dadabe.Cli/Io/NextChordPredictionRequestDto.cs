namespace Dadabe.Cli.Io;

public record NextChordPredictionRequestDto(
    string Chord,
    ProgressionDto? Context = null,
    PredictionFilterDto[]? Filters = null,
    int? MaxResults = null,
    double? Entropy = null);
