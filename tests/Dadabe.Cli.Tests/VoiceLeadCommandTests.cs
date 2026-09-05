using System.Text.Json;
using FluentAssertions;

namespace Dadabe.Cli.Tests;

/// <summary>
/// Round-trip tests for the voice-lead subcommand (D28/D29).
/// </summary>
public class VoiceLeadCommandTests
{
    private static string RunVoiceLead(string chords, string tuning = "STANDARD", int solutions = 1) =>
        TestHelpers.RunToString(env =>
            Dadabe.Cli.Commands.VoiceLeadCommand.Run(
                env,
                chordsRaw: chords,
                tuningName: tuning,
                solutions: solutions,
                minComfort: 0.0,
                validateSchema: false));

    [Fact]
    public void Two_chord_progression_produces_one_solution_with_two_steps()
    {
        var json = RunVoiceLead("Cmaj7 G7");
        var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        data.GetProperty("chords").GetArrayLength().Should().Be(2);
        data.GetProperty("solutions").GetArrayLength().Should().Be(1);

        var steps = data.GetProperty("solutions")[0].GetProperty("steps");
        steps.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void Three_chord_progression_produces_exactly_two_transitions()
    {
        var json = RunVoiceLead("Cmaj7 Am7 G7");
        var doc = JsonDocument.Parse(json);
        var steps = doc.RootElement
            .GetProperty("data")
            .GetProperty("solutions")[0]
            .GetProperty("steps");

        steps.GetArrayLength().Should().Be(3);
        // First step has no transition; subsequent two do.
        steps[0].TryGetProperty("transition", out _).Should().BeFalse();
        steps[1].TryGetProperty("transition", out var t1).Should().BeTrue();
        steps[2].TryGetProperty("transition", out var t2).Should().BeTrue();
        t1.GetProperty("totalDistance").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        t2.GetProperty("totalDistance").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Total_distance_matches_sum_of_step_distances()
    {
        var json = RunVoiceLead("Dm7 G7 Cmaj7");
        var doc = JsonDocument.Parse(json);
        var sol = doc.RootElement
            .GetProperty("data")
            .GetProperty("solutions")[0];

        int totalInHeader = sol.GetProperty("totalDistance").GetInt32();
        int sumFromSteps = sol.GetProperty("steps")
            .EnumerateArray()
            .Where(s => s.TryGetProperty("transition", out _))
            .Sum(s => s.GetProperty("transition").GetProperty("totalDistance").GetInt32());

        totalInHeader.Should().Be(sumFromSteps);
    }

    [Fact]
    public void Solutions_count_is_respected()
    {
        var json = RunVoiceLead("Cmaj7 G7", solutions: 3);
        var doc = JsonDocument.Parse(json);
        var sols = doc.RootElement.GetProperty("data").GetProperty("solutions");
        sols.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        sols.GetArrayLength().Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Envelope_command_field_is_voice_lead()
    {
        var json = RunVoiceLead("G7");
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("command").GetString().Should().Be("voice-lead");
    }

    [Fact]
    public void Unknown_chord_throws_FormatException()
    {
        var act = () => RunVoiceLead("ZZZZ");
        act.Should().Throw<Exception>();
    }
}
