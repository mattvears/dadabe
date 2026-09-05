using System.Text.Json.Nodes;
using Dadabe.Fretboard;
using FluentAssertions;

namespace Dadabe.Cli.Tests;

/// <summary>
/// End-to-end slash-chord behaviour through the CLI envelope: the bass is
/// echoed, the search is constrained to it, and an unplayable bass reports a
/// warning rather than silently falling back to root position.
/// </summary>
public class SlashChordTests
{
    private static JsonObject Data(string json) => JsonNode.Parse(json)!["data"]!.AsObject();

    private static string[] Warnings(string json) =>
        JsonNode.Parse(json)!["warnings"]!.AsArray().Select(w => w!.GetValue<string>()).ToArray();

    private static string[] VoicingBassNotes(string json) =>
        Data(json)["voicings"]!.AsArray()
            .Select(v => v!["bassNote"]!.GetValue<string>())
            .ToArray();

    [Fact]
    public void Chord_command_echoes_the_bass_note()
    {
        Data(TestHelpers.RunChord("C/G"))["bassNote"]!.GetValue<string>().Should().Be("G");
    }

    [Fact]
    public void Chord_command_omits_bassNote_for_root_position()
    {
        Data(TestHelpers.RunChord("C")).Should().NotContainKey("bassNote");
    }

    [Fact]
    public void Foreign_bass_appears_in_pitch_classes_with_the_bass_function()
    {
        var pcs = Data(TestHelpers.RunChord("C/D"))["pitchClasses"]!.AsArray();
        var bass = pcs.Single(p => p!["function"]!.GetValue<string>() == "bass");
        bass!["name"]!.GetValue<string>().Should().Be("D");
    }

    [Theory]
    [InlineData("C/G", "G")]
    [InlineData("C/E", "E")]
    [InlineData("C/D", "D")]
    public void Every_returned_voicing_sounds_the_requested_bass_lowest(string symbol, string expectedBass)
    {
        var json = TestHelpers.RunVoicings(symbol, "STANDARD", limit: 25);
        var basses = VoicingBassNotes(json);

        basses.Should().NotBeEmpty();
        // bassNote carries an octave (e.g. "G2"); compare on the note name only.
        basses.Select(b => b.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
              .Should().AllBe(expectedBass);
    }

    [Fact]
    public void Root_position_search_is_not_constrained()
    {
        // Emission is in deterministic lex order (D5), so a small limit returns a
        // run of voicings that happen to share a bass. Sample the whole set.
        var basses = VoicingBassNotes(TestHelpers.RunVoicings("C", "STANDARD", limit: 0))
            .Select(b => b.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
            .Distinct();
        basses.Should().BeEquivalentTo(["C", "E", "G"],
            "an unslashed chord may be voiced over any of its chord tones");
    }

    [Fact]
    public void Unplayable_bass_returns_no_voicings_and_warns()
    {
        // Deliberately starved search so no shape can place D lowest.
        var starved = SearchParams.Default with { MaxFret = 3, MaxSpan = 1, AllowOpen = false };
        var json = TestHelpers.RunVoicings("C/D", "STANDARD", limit: 25, searchParams: starved);

        Data(json)["voicings"]!.AsArray().Should().BeEmpty();
        Warnings(json).Should().ContainSingle()
            .Which.Should().Contain("D").And.Contain("bass");
    }

    [Fact]
    public void Playable_slash_chord_produces_no_warning()
    {
        Warnings(TestHelpers.RunVoicings("C/G", "STANDARD", limit: 5)).Should().BeEmpty();
    }
}
