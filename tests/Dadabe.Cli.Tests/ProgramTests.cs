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
    public void Slash_chord_returns_exit_one()
    {
        Run("chord", "C/G", "--out", "ignored.json").Should().Be(1);
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
}
