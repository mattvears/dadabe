using System.Text.Json.Nodes;
using FluentAssertions;

namespace Dadabe.Cli.Tests;

/// <summary>
/// Exit-code contract tests via the in-process CLI parser. We avoid
/// launching the actual binary so tests don't depend on Windows Smart App
/// Control or other shell quirks.
/// </summary>
public class ProgramTests
{
    private static int Run(params string[] args)
    {
        var root = Program.BuildRootCommand();
        return root.Parse(args).Invoke();
    }

    [Fact]
    public void Chord_command_succeeds()
    {
        // Write to a temp file so stdout isn't littered.
        var path = Path.Combine(Path.GetTempPath(), "dadabe-prog-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Run("chord", "Cmaj7", "--out", path).Should().Be(0);
            File.Exists(path).Should().BeTrue();
            new FileInfo(path).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    public void Bad_chord_returns_exit_one()
    {
        Run("chord", "NotAChord", "--out", "ignored.json").Should().Be(1);
    }

    [Fact]
    public void Slash_chord_succeeds_with_exit_zero()
    {
        var path = Path.Combine(Path.GetTempPath(), "dadabe-prog-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Run("chord", "C/G", "--out", path).Should().Be(0);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    public void Unknown_tuning_returns_exit_one()
    {
        var path = Path.Combine(Path.GetTempPath(), "dadabe-prog-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Run("tuning", "NOT_A_TUNING", "--out", path).Should().Be(1);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    // ---- voicings: nothing exercised the CLI parser's own option wiring for
    // this command before v0.6.1 adds --inversion alongside --require-root
    // here (docs/v0.6.1/design.md §1.8) — these pin the existing wiring so a
    // mistake in the new option doesn't silently also break an old one.

    [Fact]
    public void Voicings_command_succeeds_via_the_CLI_parser()
    {
        var path = Path.Combine(Path.GetTempPath(), "dadabe-prog-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Run("voicings", "Cmaj7", "--tuning", "STANDARD", "--out", path).Should().Be(0);
            File.Exists(path).Should().BeTrue();
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            root["data"]!["voicings"]!.AsArray().Should().NotBeEmpty();
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    public void Voicings_command_with_bad_chord_returns_exit_one()
    {
        Run("voicings", "NotAChord", "--out", "ignored.json").Should().Be(1);
    }

    [Fact]
    public void Require_root_flag_reaches_SearchParams_and_excludes_rootless_voicings()
    {
        var path = Path.Combine(Path.GetTempPath(), "dadabe-prog-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            // Fm7's default floor omits the root (D4), so --require-root must
            // visibly change the result if the flag is actually threaded
            // through to SearchParams.RequireRoot rather than just parsed.
            Run("voicings", "Fm7", "--tuning", "STANDARD", "--require-root", "--out", path).Should().Be(0);
            var voicings = JsonNode.Parse(File.ReadAllText(path))!["data"]!["voicings"]!.AsArray();
            voicings.Should().NotBeEmpty();
            static bool HasRootFunction(JsonNode? v) => v!["positions"]!.AsArray()
                .Any(p => p!["function"]?.GetValue<string>() == "1");
            voicings.All(HasRootFunction).Should().BeTrue("--require-root must exclude every rootless voicing");
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    [Fact]
    public void Min_comfort_flag_reaches_VoicingsCommand_and_filters_results()
    {
        var path = Path.Combine(Path.GetTempPath(), "dadabe-prog-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Run("voicings", "Cmaj7", "--tuning", "STANDARD", "--min-comfort", "1.1", "--out", path).Should().Be(0);
            var voicings = JsonNode.Parse(File.ReadAllText(path))!["data"]!["voicings"]!.AsArray();
            voicings.Should().BeEmpty("no voicing can score above the 0.0-1.0 comfort ceiling");
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }
}
