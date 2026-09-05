namespace Dadabe.Core.Transform;

/// <summary>
/// One step in a transform chain. <c>Type</c> + <c>Params</c> matches the
/// wire shape already committed in <c>schemas/transformation.schema.json</c>.
/// </summary>
public sealed record TransformStep(string Type, IReadOnlyDictionary<string, object> Params);

/// <param name="Chords">Emitted symbols.</param>
/// <param name="Notes">Skips, respellings, and other warnings.</param>
/// <param name="Invertible">Whether this transform declares itself invertible (D50).</param>
public sealed record TransformResult(
    IReadOnlyList<string> Chords,
    IReadOnlyList<TransformNote> Notes,
    bool Invertible);

/// <param name="Index">Chord index the note applies to, or null for whole-progression.</param>
/// <param name="Kind">Machine-readable category, e.g. "skipped", "respelled".</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record TransformNote(int? Index, string Kind, string Message);
