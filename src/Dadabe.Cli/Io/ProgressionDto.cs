using System.Text.Json.Serialization;

namespace Dadabe.Cli.Io
{
    // Placeholder DTO for progressions. Kept minimal for v0.3 scaffolding.
    public record ProgressionDto(string[] Chords, int? Tempo = null);
}
