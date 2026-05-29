using FluentAssertions;

namespace Dadabe.Core.Tests;

public class TuningTests
{
    [Fact]
    public void Embedded_catalog_includes_default_tunings()
    {
        var catalog = TuningCatalog.Load(workingDirectory: null);
        var expected = new[] { "DADABE", "STANDARD", "DROP_D", "DADGAD", "OPEN_G", "OPEN_D" };
        catalog.ByName.Keys.Should().Contain(expected);
    }

    [Fact]
    public void DADABE_resolves_to_D2_A2_D3_A3_B3_E4()
    {
        var dadabe = TuningCatalog.Load(workingDirectory: null).Get("DADABE");
        dadabe.Strings.Select(p => p.ToString())
            .Should().Equal("D2", "A2", "D3", "A3", "B3", "E4");
    }

    [Fact]
    public void ParseSpec_handles_ad_hoc_strings()
    {
        var t = Tuning.ParseSpec("D2,A2,D3,A3,B3,E4");
        t.Strings.Should().HaveCount(6);
        t.Strings[0].ToString().Should().Be("D2");
        t.Strings[5].ToString().Should().Be("E4");
    }

    [Fact]
    public void Content_hash_is_stable_across_instantiations()
    {
        var a = TuningCatalog.Load(null).Get("DADABE");
        var b = Tuning.ParseSpec("D2,A2,D3,A3,B3,E4");
        a.ContentHash.Should().Be(b.ContentHash);
    }

    [Fact]
    public void Respelling_changes_content_hash()
    {
        // Same sounding pitches, different spelling: identity differs (D17).
        var d2 = Tuning.ParseSpec("D2");
        var cxx2 = Tuning.ParseSpec("C##2");
        d2.Strings[0].Midi.Should().Be(cxx2.Strings[0].Midi);
        d2.ContentHash.Should().NotBe(cxx2.ContentHash);
    }

    [Fact]
    public void Overlay_file_wins_on_name_collision()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dadabe-tuning-overlay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "Tunings.json"),
                """{ "schemaVersion": "1", "tunings": [ { "name": "DADABE", "strings": ["E2","E2","E2","E2","E2","E2"] } ] }""");
            var catalog = TuningCatalog.Load(dir);
            catalog.Get("DADABE").Strings[0].ToString().Should().Be("E2");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Overlay_file_unions_when_no_name_collision()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dadabe-tuning-overlay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "Tunings.json"),
                """{ "schemaVersion": "1", "tunings": [ { "name": "MY_TUNING", "strings": ["C2","G2","D3","A3"] } ] }""");
            var catalog = TuningCatalog.Load(dir);
            catalog.ByName.Should().ContainKey("MY_TUNING");
            catalog.ByName.Should().ContainKey("DADABE");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
