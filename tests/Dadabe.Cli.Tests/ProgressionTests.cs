using System.Text.Json;
using Dadabe.Cli.Io;
using Xunit;

namespace Dadabe.Cli.Tests
{
    public class ProgressionTests
    {
        [Fact]
        public void ProgressionDto_SerializesAndDeserializes()
        {
            var chords = new[] { "Cmaj7", "Am7" };
            var p = new ProgressionDto(chords, 120);
            var json = JsonSerializer.Serialize(p);
            var round = JsonSerializer.Deserialize<ProgressionDto>(json);
            Assert.NotNull(round);
            Assert.Equal(p.Chords, round.Chords);
            Assert.Equal(p.Tempo, round.Tempo);
        }
    }
}
