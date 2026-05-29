namespace Dadabe.Fretboard;

/// <summary>
/// Runtime context for one CLI invocation (D22): the resolved catalogs,
/// the resolved hand model, and the output target. Built once per run via
/// <see cref="Build(string?)"/>; commands consume it instead of dragging
/// every config flag through the call graph.
/// </summary>
/// <remarks>
/// Not content-hashable: <see cref="Environment"/> is plumbing, not a
/// domain identity. Its constituent parts (each catalog record,
/// <see cref="HandModel"/>) are individually content-hashable so memo
/// keys remain stable.
/// </remarks>
public sealed record Environment(
    Catalogs Catalogs,
    HandModel HandModel,
    OutputTarget Output,
    ModelVersion Version = ModelVersion.V1)
{
    /// <summary>
    /// Build with embedded-only catalogs (no overlays), default hand
    /// model, stdout output. Convenient for tests and library callers.
    /// </summary>
    public static Environment Default() => new(
        Catalogs.Default(),
        Dadabe.Fretboard.HandModel.Default,
        OutputTarget.Stdout());

    /// <summary>
    /// Load catalogs from <paramref name="workingDirectory"/> (overlays
    /// honoured when present), apply <paramref name="handModel"/> if given
    /// (otherwise default), wrap <paramref name="output"/> if given.
    /// </summary>
    public static Environment Build(
        string? workingDirectory,
        HandModel? handModel = null,
        OutputTarget? output = null) => new(
            Catalogs.Load(workingDirectory),
            handModel ?? Dadabe.Fretboard.HandModel.Default,
            output ?? OutputTarget.Stdout());
}
