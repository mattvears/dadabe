using System.Text.Json;
using Dadabe.Cli.Commands;
using FluentAssertions;

namespace Dadabe.Cli.Tests;

public class TransformCommandTests
{
    private static string RunTransform(
        string? chords = "C F G", string? chain = "retrograde,transpose:M2", bool all = false, bool validateSchema = false) =>
        TestHelpers.RunToString(env =>
            TransformCommand.Run(
                env,
                chordsRaw: chords,
                progressionSlug: null,
                chainRaw: chain,
                all: all,
                tuningName: "STANDARD",
                minComfort: 0.0,
                validateSchema: validateSchema));

    [Fact]
    public void Chain_parses_and_applies_in_order()
    {
        var json = RunTransform("C F G", "retrograde,transpose:M2");
        var doc = JsonDocument.Parse(json);
        var result = doc.RootElement.GetProperty("data").GetProperty("results")[0];
        var chords = result.GetProperty("chords").EnumerateArray().Select(e => e.GetString()).ToList();

        chords.Should().Equal("A", "G", "D");
    }

    [Fact]
    public void All_returns_the_catalogue_ranked()
    {
        var json = RunTransform(chords: "C F G", chain: null, all: true);
        var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("data").GetProperty("results");

        results.GetArrayLength().Should().Be(15);

        var unplayableCounts = results.EnumerateArray()
            .Select(r => r.TryGetProperty("playability", out var p)
                ? p.GetProperty("unplayable").GetArrayLength()
                : int.MaxValue)
            .ToList();
        unplayableCounts.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Envelope_validates_against_transform_result_schema()
    {
        var act = () => RunTransform("C F G", "retrograde", validateSchema: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void Envelope_command_field_is_transform()
    {
        var json = RunTransform();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("command").GetString().Should().Be("transform");
    }

    [Fact]
    public void Missing_chords_and_chain_throws()
    {
        var act = () => RunTransform(chords: null, chain: null);
        act.Should().Throw<FormatException>();
    }
}
