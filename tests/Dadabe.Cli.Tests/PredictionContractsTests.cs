using System.Text.Json;
using Xunit;
using Dadabe.Cli.Io;
using System.Collections.Generic;

namespace Dadabe.Cli.Tests
{
    public class PredictionContractsTests
    {
        [Fact]
        public void PredictionRequest_SerializeRoundTrip()
        {
            var contextChords = new[] { "Dm7", "G7" };
            var context = new ProgressionDto(contextChords, 100);
            var filterParams = new Dictionary<string, object> { { "chord", "Gm" } };
            var filter = new PredictionFilterDto("byChord", filterParams);
            var req = new NextChordPredictionRequestDto(context, new[] { filter }, 10);

            var json = JsonSerializer.Serialize(req);
            var round = JsonSerializer.Deserialize<NextChordPredictionRequestDto>(json);

            Assert.NotNull(round);
            Assert.Equal(10, round.MaxResults);
            Assert.NotNull(round.Filters);
            Assert.Equal("byChord", round.Filters[0].Type);
        }
    }
}
