using Dadabe.Core;
using Dadabe.Core.Chord;

namespace Dadabe.Fretboard;

/// <summary>
/// The three resolved catalogs (D22 / §10): tuning names,
/// chord grammar, voicing categories. Each is loaded by its own loader
/// from embedded defaults overlaid with a same-named file in the working
/// directory (if present).
/// </summary>
public sealed record Catalogs(
    TuningCatalog Tunings,
    ChordGrammar ChordGrammar,
    VoicingCategoryCatalog VoicingCategories)
{
    /// <summary>Embedded-only load (no overlays).</summary>
    public static Catalogs Default() => Load(workingDirectory: null);

    /// <summary>
    /// Load all three catalogs at <paramref name="workingDirectory"/>.
    /// Pass <c>null</c> to skip overlays.
    /// </summary>
    public static Catalogs Load(string? workingDirectory) => new(
        TuningCatalog.Load(workingDirectory),
        ChordGrammarLoader.Load(workingDirectory),
        VoicingCategoryCatalog.Load(workingDirectory));
}
