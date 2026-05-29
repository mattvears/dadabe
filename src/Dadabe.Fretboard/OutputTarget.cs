namespace Dadabe.Fretboard;

/// <summary>
/// Where and how to write JSON (D22). <c>FilePath == null</c> means
/// stdout. <c>Pretty</c> controls indentation. Plain data — the CLI's
/// JSON envelope is the only consumer; no I/O abstraction in v0.1.
/// </summary>
public sealed record OutputTarget(string? FilePath, bool Pretty)
{
    public static OutputTarget Stdout(bool pretty = false) => new(FilePath: null, Pretty: pretty);
}
