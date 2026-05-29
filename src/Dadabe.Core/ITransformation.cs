namespace Dadabe.Core;

/// <summary>
/// Placeholder interface for harmonic transformations (v0.3 scaffolding).
/// Implementations (tritone sub, modal interchange, negative harmony, etc.)
/// are deferred to a future release.
/// </summary>
public interface ITransformation
{
    /// <summary>Short machine-readable identifier, e.g. "tritone-sub".</summary>
    string Id { get; }

    /// <summary>Human-readable display name shown in frontends.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Opaque parameter bag passed to the transformation at runtime.
    /// Shape is transformation-specific; consumers validate against the
    /// transformation schema before invoking.
    /// </summary>
    IReadOnlyDictionary<string, object> Parameters { get; }
}
