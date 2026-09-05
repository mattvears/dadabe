namespace Dadabe.Core.Transform;

/// <summary>Strictness policy (D54): request-wide, not per-step.</summary>
public enum Strictness
{
    Strict,
    Loose,
}

/// <summary>
/// Request-wide options threaded through every step of a chain (D54, D46).
/// Deliberately not part of <see cref="TransformStep.Params"/> — the whole
/// point of D54 is one setting for the request, not one the user can vary
/// step-to-step.
/// </summary>
public sealed record TransformRequestOptions(
    Strictness Strictness = Strictness.Loose,
    (int RootPc, bool IsMinor)? KeyOverride = null)
{
    public static readonly TransformRequestOptions Default = new();
}
