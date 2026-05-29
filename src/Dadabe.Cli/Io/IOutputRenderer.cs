namespace Dadabe.Cli.Io;

/// <summary>
/// Placeholder interface for output renderers (v0.3 scaffolding).
/// Abstracts the JSON-to-stdout sink so future diagram/tab renderers can
/// implement the same contract without touching command logic.
/// </summary>
public interface IOutputRenderer
{
    /// <summary>
    /// Render the envelope payload to the configured output target.
    /// </summary>
    void Render<T>(Envelope<T> envelope);
}
