using System.Text.Json.Nodes;
using Dadabe.Cli.Io;
using Dadabe.Fretboard;
using FluentAssertions;
using Json.Schema;
using DadabeEnv = Dadabe.Fretboard.Environment;

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

    [Fact]
    public void ValidateSchema_throws_on_invalid_output_file()
    {
        var outFile = Path.Combine(Path.GetTempPath(), $"dadabe-bad-{Guid.NewGuid():N}.json");
        try
        {
            // Write deliberately invalid JSON to the output path.
            File.WriteAllText(outFile, """{"not_a_valid_envelope": true}""");

            // ValidateSchema reads outFile (not the envelope object) when FilePath is non-null.
            var envelope = new Envelope<PredictionResultDto>(
                "dadabe", "0.4.0", "1", "predict",
                new InputDto("C", null, null),
                new PredictionResultDto([], null),
                []);
            var output = new OutputTarget(outFile, Pretty: false);

            var act = () => JsonEnvelope.ValidateSchema(envelope, "predict", TestHelpers.RepoRoot(), output);
            act.Should().Throw<SchemaViolationException>();
        }
        finally
        {
            if (File.Exists(outFile)) { File.Delete(outFile); }
        }
    }

    [Fact]
    public void ValidateSchema_passing_exits_zero_via_program()
    {
        var requestFile = Path.Combine(Path.GetTempPath(), $"dadabe-req-{Guid.NewGuid():N}.json");
        var outFile = Path.Combine(Path.GetTempPath(), $"dadabe-out-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(requestFile, """{"chord":"Cmaj7"}""");
            var root = Program.BuildRootCommand();
            var exit = root.Parse(["predict", "--input", requestFile, "--out", outFile, "--validate-schema"]).Invoke();
            exit.Should().Be(0);
        }
        finally
        {
            if (File.Exists(requestFile)) { File.Delete(requestFile); }
            if (File.Exists(outFile)) { File.Delete(outFile); }
        }
    }

    private static string FormatErrors(EvaluationResults r) =>
        r.IsValid
            ? "(valid)"
            : string.Join("\n", r.Details
                .Where(d => d.HasErrors && d.Errors is not null)
                .SelectMany(d => d.Errors!.Select(e => $"{d.EvaluationPath} -> {e.Key}: {e.Value}")));
}
