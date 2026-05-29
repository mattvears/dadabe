using System.Text.Json.Nodes;
using FluentAssertions;
using Json.Schema;

namespace Dadabe.Cli.Tests;

public class SchemaValidationTests
{
    private static EvaluationResults Validate(string json, string schemaFileName)
    {
        var schema = JsonSchema.FromFile(TestHelpers.SchemaPath(schemaFileName));
        var node = JsonNode.Parse(json)!;
        var opts = new EvaluationOptions { OutputFormat = OutputFormat.List };
        return schema.Evaluate(node, opts);
    }

    [Fact]
    public void Voicings_output_validates_against_schema()
    {
        var json = TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 10);
        var result = Validate(json, "voicings.schema.json");
        result.IsValid.Should().BeTrue(FormatErrors(result));
    }

    [Fact]
    public void Tuning_output_validates_against_schema()
    {
        var json = TestHelpers.RunTuning("DADABE");
        var result = Validate(json, "tuning.schema.json");
        result.IsValid.Should().BeTrue(FormatErrors(result));
    }

    [Fact]
    public void Chord_output_validates_against_schema()
    {
        var json = TestHelpers.RunChord("F#maj7");
        var result = Validate(json, "chord.schema.json");
        result.IsValid.Should().BeTrue(FormatErrors(result));
    }

    [Fact]
    public void Envelope_validates_for_each_command()
    {
        foreach (var (name, json) in new[]
        {
            ("voicings", TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 3)),
            ("tuning",   TestHelpers.RunTuning("DADABE")),
            ("chord",    TestHelpers.RunChord("Cmaj7")),
        })
        {
            var result = Validate(json, "envelope.schema.json");
            result.IsValid.Should().BeTrue($"{name} envelope: {FormatErrors(result)}");
        }
    }

    private static string FormatErrors(EvaluationResults r) =>
        r.IsValid
            ? "(valid)"
            : string.Join("\n", r.Details
                .Where(d => d.HasErrors && d.Errors is not null)
                .SelectMany(d => d.Errors!.Select(e => $"{d.EvaluationPath} -> {e.Key}: {e.Value}")));
}
