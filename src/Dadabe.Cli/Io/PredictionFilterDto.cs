using System.Collections.Generic;

namespace Dadabe.Cli.Io
{
    // Filter DTO for next-chord prediction. "Type" is one of: byChord, byScale, byMode, byQuality, custom.
    public record PredictionFilterDto(string Type, Dictionary<string, object>? Params = null);
}
