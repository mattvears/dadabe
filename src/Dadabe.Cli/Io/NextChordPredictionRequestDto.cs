namespace Dadabe.Cli.Io
{
    // Request DTO for next-chord prediction
    public record NextChordPredictionRequestDto(ProgressionDto? Context = null, PredictionFilterDto[]? Filters = null, int? MaxResults = null);
}
