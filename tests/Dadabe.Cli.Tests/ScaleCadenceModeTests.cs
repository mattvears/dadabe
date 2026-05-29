using System.Text.Json;
using Xunit;
using Dadabe.Cli.Io;

namespace Dadabe.Cli.Tests
{
    public class ScaleCadenceModeTests
    {
        [Fact]
        public void ScaleCadenceMode_SerializeRoundTrip()
        {
            var scale = new ScaleDto("Major", new[] { "C", "D", "E", "F", "G", "A", "B" }, new[] {0,2,4,5,7,9,11}, null, "Ionian");
            var cadence = new CadenceDto("authentic", new[] { "V", "I" }, "V->I", "Standard authentic cadence");
            var mode = new ModeDto("Dorian", "Major", 1, new[] {0,2,3,5,7,9,10}, new[] {"D","E","F","G","A","B","C"}, "Dorian mode");

            var sjson = JsonSerializer.Serialize(scale);
            var cjson = JsonSerializer.Serialize(cadence);
            var mjson = JsonSerializer.Serialize(mode);

            var s2 = JsonSerializer.Deserialize<ScaleDto>(sjson);
            var c2 = JsonSerializer.Deserialize<CadenceDto>(cjson);
            var m2 = JsonSerializer.Deserialize<ModeDto>(mjson);

            Assert.NotNull(s2);
            Assert.NotNull(c2);
            Assert.NotNull(m2);
            Assert.Equal(scale.Name, s2.Name);
            Assert.Equal(cadence.Type, c2.Type);
            Assert.Equal(mode.Name, m2.Name);
        }
    }
}
