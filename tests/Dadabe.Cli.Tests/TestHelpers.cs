using Dadabe.Cli.Commands;
using Dadabe.Fretboard;
using DadabeEnv = Dadabe.Fretboard.Environment;

namespace Dadabe.Cli.Tests;

internal static class TestHelpers
{
    /// <summary>Walk up from the test binary to locate the repo root (where <c>schemas/</c> lives).</summary>
    public static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "schemas"))
                && Directory.Exists(Path.Combine(dir, "src")))
            {
                return dir;
            }
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) { break; }
            dir = parent;
        }
        throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
    }

    public static string SchemaPath(string name) => Path.Combine(RepoRoot(), "schemas", name);

    /// <summary>Run a command into a temp file, return the JSON text.</summary>
    public static string RunToString(Action<DadabeEnv> commandRunner, bool pretty = true)
    {
        var path = Path.Combine(Path.GetTempPath(), "dadabe-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var env = DadabeEnv.Build(
                workingDirectory: RepoRoot(),
                handModel: HandModel.Default,
                output: new OutputTarget(path, pretty));
            commandRunner(env);
            return File.ReadAllText(path);
        }
        finally
        {
            if (File.Exists(path)) { File.Delete(path); }
        }
    }

    public static string RunVoicings(string chord, string tuning = "STANDARD", int limit = 5, bool pretty = true,
        SearchParams? searchParams = null) =>
        RunToString(env => VoicingsCommand.Run(env, chord, tuning, searchParams ?? SearchParams.Default, limit), pretty);

    public static string RunTuning(string tuning, bool pretty = true) =>
        RunToString(env => TuningCommand.Run(env, tuning), pretty);

    public static string RunChord(string chord, bool pretty = true) =>
        RunToString(env => ChordCommand.Run(env, chord), pretty);
}
