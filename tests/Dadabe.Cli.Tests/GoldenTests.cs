using System.Text.Json;
using System.Text.Json.Nodes;
using Dadabe.Cli.Io;
using FluentAssertions;

namespace Dadabe.Cli.Tests;

/// <summary>
/// Stability tests: same inputs → same JSON (modulo tool version), and
/// every voicing id is a content hash so cross-run comparison is safe.
/// </summary>
public class GoldenTests
{
    [Fact]
    public void Same_chord_and_tuning_produces_same_voicing_ids_across_runs()
    {
        var a = TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 5);
        var b = TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 5);
        ExtractVoicingIds(a).Should().Equal(ExtractVoicingIds(b));
    }

    [Fact]
    public void Voicing_id_matches_content_hash_regex()
    {
        var json = TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 5);
        foreach (var id in ExtractVoicingIds(json))
        {
            id.Should().MatchRegex(@"^voicing:\d+:[0-9a-f]{32}$");
        }
    }

    [Fact]
    public void Tool_and_schemaVersion_constants_match_envelope()
    {
        var json = TestHelpers.RunChord("Cmaj7");
        var root = JsonNode.Parse(json)!.AsObject();
        root["tool"]!.GetValue<string>().Should().Be(JsonEnvelope.ToolName);
        root["schemaVersion"]!.GetValue<string>().Should().Be(JsonEnvelope.SchemaVersion);
        root["version"]!.GetValue<string>().Should().Be(JsonEnvelope.ToolVersion);
    }

    [Fact]
    public void Limit_truncates_prefix_lex_order_preserved()
    {
        var ten = TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 10);
        var three = TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 3);
        var idsTen = ExtractVoicingIds(ten);
        var idsThree = ExtractVoicingIds(three);
        idsThree.Should().Equal(idsTen.Take(3));
    }

    [Fact]
    public void FSharpMaj7_spells_with_sharps()
    {
        var json = TestHelpers.RunChord("F#maj7");
        var root = JsonNode.Parse(json)!.AsObject();
        var names = root["data"]!["pitchClasses"]!.AsArray()
            .Select(n => n!["name"]!.GetValue<string>())
            .ToArray();
        names.Should().Equal("F#", "A#", "C#", "E#");
    }

    [Fact]
    public void GbMaj7_spells_with_flats()
    {
        var json = TestHelpers.RunChord("Gbmaj7");
        var root = JsonNode.Parse(json)!.AsObject();
        var names = root["data"]!["pitchClasses"]!.AsArray()
            .Select(n => n!["name"]!.GetValue<string>())
            .ToArray();
        names.Should().Equal("Gb", "Bb", "Db", "F");
    }

    [Fact]
    public void DADABE_tuning_strings_are_D2_A2_D3_A3_B3_E4()
    {
        var json = TestHelpers.RunTuning("DADABE");
        var root = JsonNode.Parse(json)!.AsObject();
        var strings = root["data"]!["strings"]!.AsArray()
            .Select(n => n!.GetValue<string>())
            .ToArray();
        strings.Should().Equal("D2", "A2", "D3", "A3", "B3", "E4");
    }

    private static string[] ExtractVoicingIds(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        return root["data"]!["voicings"]!.AsArray()
            .Select(v => v!["id"]!.GetValue<string>())
            .ToArray();
    }
}
