using System.Text.Json;
using Xunit;
using Dadabe.Cli.Io;

namespace Dadabe.Cli.Tests
{
    public class ProgressionTests
    {
        [Fact]
        public void ProgressionDto_SerializesAndDeserializes()
        {
            var p = new ProgressionDto(new[] { "Cmaj7", "Am7" }, 120);
            var json = JsonSerializer.Serialize(p);
            var round = JsonSerializer.Deserialize<ProgressionDto>(json);
            Assert.NotNull(round);
            Assert.Equal(p.Chords, round.Chords);
            Assert.Equal(p.Tempo, round.Tempo);
        }
    }
}
