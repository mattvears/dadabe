using System.Text.Json;
using System.Text.Json.Nodes;
using Dadabe.Cli.Commands;
using Dadabe.Cli.Io;
using Dadabe.Fretboard;
using DadabeEnv = Dadabe.Fretboard.Environment;

namespace Dadabe.Cli.Tests;

public class PredictCommandTests
{
    private static string RunPredict(
        string requestJson,
        double entropy = 0.5,
        bool pretty = true)
    {
        var requestFile = Path.Combine(Path.GetTempPath(), $"dadabe-predict-{Guid.NewGuid():N}.json");
        var outFile = Path.Combine(Path.GetTempPath(), $"dadabe-predict-out-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(requestFile, requestJson);
            var env = DadabeEnv.Build(
                workingDirectory: TestHelpers.RepoRoot(),
                handModel: HandModel.Default,
                output: new OutputTarget(outFile, pretty));
            PredictCommand.Run(env, requestFile, entropy, validateSchema: false);
            return File.ReadAllText(outFile);
        }
        finally
        {
            if (File.Exists(requestFile)) { File.Delete(requestFile); }
            if (File.Exists(outFile)) { File.Delete(outFile); }
        }
    }

    [Fact]
    public void Predict_returns_valid_envelope_shape()
    {
        var json = RunPredict("""{"chord":"Dm7"}""");
        var doc = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("dadabe", doc["tool"]!.GetValue<string>());
        Assert.Equal("predict", doc["command"]!.GetValue<string>());
        Assert.Equal("1", doc["schemaVersion"]!.GetValue<string>());
        Assert.NotNull(doc["data"]);
        Assert.NotNull(doc["data"]!["results"]);
    }

    [Fact]
    public void Predict_returns_results_with_score_in_range()
    {
        var json = RunPredict("""{"chord":"Cmaj7","maxResults":5}""");
        var results = JsonNode.Parse(json)!["data"]!["results"]!.AsArray();

        Assert.True(results.Count > 0);
        foreach (var r in results)
        {
            var score = r!["score"]!.GetValue<double>();
            Assert.True(score >= 0.0 && score <= 1.0, $"score {score} out of range");
        }
    }

    [Fact]
    public void Predict_input_echo_contains_chord()
    {
        var json = RunPredict("""{"chord":"G7"}""");
        var input = JsonNode.Parse(json)!["input"]!;
        Assert.Equal("G7", input["chord"]!.GetValue<string>());
    }

    [Fact]
    public void ByQuality_filter_keeps_only_matching_suffix()
    {
        var json = RunPredict("""
            {
              "chord": "Dm7",
              "filters": [{ "type": "byQuality", "params": { "quality": "7" } }],
              "maxResults": 10
            }
            """);
        var results = JsonNode.Parse(json)!["data"]!["results"]!.AsArray();
        foreach (var r in results)
        {
            var chord = r!["chord"]!.GetValue<string>();
            Assert.EndsWith("7", chord);
        }
    }

    [Fact]
    public void ByChord_filter_keeps_only_exact_match()
    {
        var json = RunPredict("""
            {
              "chord": "C",
              "filters": [{ "type": "byChord", "params": { "chord": "G" } }],
              "maxResults": 10
            }
            """);
        var results = JsonNode.Parse(json)!["data"]!["results"]!.AsArray();
        foreach (var r in results)
        {
            Assert.Equal("G", r!["chord"]!.GetValue<string>());
        }
    }

    [Fact]
    public void ByChord_filter_all_eliminated_returns_empty_results()
    {
        var json = RunPredict("""
            {
              "chord": "C",
              "filters": [{ "type": "byChord", "params": { "chord": "XYZZY" } }],
              "maxResults": 10
            }
            """);
        var results = JsonNode.Parse(json)!["data"]!["results"]!.AsArray();
        Assert.Empty(results);
    }

    [Fact]
    public void Deferred_filter_type_adds_warning()
    {
        var json = RunPredict("""
            {
              "chord": "C",
              "filters": [{ "type": "byScale", "params": {} }]
            }
            """);
        var warnings = JsonNode.Parse(json)!["warnings"]!.AsArray();
        Assert.Contains(warnings, w => w!.GetValue<string>().Contains("byScale"));
    }

    [Fact]
    public void Context_echoed_in_metadata_contextUsed()
    {
        var json = RunPredict("""
            {
              "chord": "Dm7",
              "context": { "chords": ["Cmaj7", "Am7"], "tempo": 120 }
            }
            """);
        var metadata = JsonNode.Parse(json)!["data"]!["metadata"];
        Assert.NotNull(metadata);
        var chords = metadata!["contextUsed"]!["chords"]!.AsArray();
        Assert.Equal(2, chords.Count);
    }

    [Fact]
    public void Per_request_entropy_override_is_honoured()
    {
        // Very low entropy → sharp distribution (top candidate has high probability).
        var lowJson = RunPredict("""{"chord":"C","entropy":0.01}""");
        var highJson = RunPredict("""{"chord":"C","entropy":10.0}""");

        var lowTop = JsonNode.Parse(lowJson)!["data"]!["results"]![0]!["score"]!.GetValue<double>();
        var highTop = JsonNode.Parse(highJson)!["data"]!["results"]![0]!["score"]!.GetValue<double>();

        Assert.True(lowTop > highTop, $"Low-entropy top score {lowTop} should exceed high-entropy top score {highTop}");
    }

    [Fact]
    public void Predict_validates_schema_and_passes()
    {
        var requestFile = Path.Combine(Path.GetTempPath(), $"dadabe-predict-{Guid.NewGuid():N}.json");
        var outFile = Path.Combine(Path.GetTempPath(), $"dadabe-predict-out-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(requestFile, """{"chord":"Dm7"}""");
            var env = DadabeEnv.Build(
                workingDirectory: TestHelpers.RepoRoot(),
                handModel: HandModel.Default,
                output: new OutputTarget(outFile, Pretty: false));
            // Should not throw SchemaViolationException.
            PredictCommand.Run(env, requestFile, entropy: 0.5, validateSchema: true);
        }
        finally
        {
            if (File.Exists(requestFile)) { File.Delete(requestFile); }
            if (File.Exists(outFile)) { File.Delete(outFile); }
        }
    }

    [Fact]
    public void PredictionRequest_round_trip_with_chord_field()
    {
        var req = new NextChordPredictionRequestDto(
            Chord: "Dm7",
            Context: new ProgressionDto(["Cmaj7", "Am7"], 120),
            Filters: [new PredictionFilterDto("byQuality", new Dictionary<string, object> { { "quality", "7" } })],
            MaxResults: 5,
            Entropy: 0.8);

        var json = JsonSerializer.Serialize(req);
        var round = JsonSerializer.Deserialize<NextChordPredictionRequestDto>(json);

        Assert.NotNull(round);
        Assert.Equal("Dm7", round!.Chord);
        Assert.Equal(5, round.MaxResults);
        Assert.Equal(0.8, round.Entropy);
        Assert.Equal("byQuality", round.Filters![0].Type);
    }
}
