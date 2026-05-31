using System.Text.Json.Serialization;

namespace Dadabe.Cli.Io;

public record ProgressionDto(
    [property: JsonPropertyName("chords")] string[] Chords,
    [property: JsonPropertyName("tempo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Tempo = null);
