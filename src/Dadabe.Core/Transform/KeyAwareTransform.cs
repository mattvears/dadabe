using Dadabe.Core.Chord;

namespace Dadabe.Core.Transform;

/// <summary>
/// Shared plumbing for the D47 key-aware family: key resolution (an
/// explicit override, else <see cref="KeyInference.InferKey"/>, else the
/// uniform "needs a key" note from D46) and the strict/loose branch from
/// D54. Subclasses implement only their own musical logic.
/// </summary>
internal abstract class KeyAwareTransform : ITransformation
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract bool IsInvertible { get; }

    protected readonly ChordExpander Expander;

    protected KeyAwareTransform(ChordGrammar grammar) => Expander = new ChordExpander(grammar);

    /// <summary>Override to say a key isn't needed at all — e.g. an explicit axis makes negative-harmony's mirror self-contained.</summary>
    protected virtual bool RequiresKey(IReadOnlyDictionary<string, object> parameters) => true;

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var key = ResolveKey(chords, parameters);
        if (key is null && RequiresKey(parameters))
        {
            return new TransformResult(
                chords.Select(c => c.ToSymbol()).ToArray(),
                [new TransformNote(null, "needs-key", $"{DisplayName} needs a key; set one here.")],
                Invertible: false);
        }

        var strict = TransformParams.GetString(parameters, "strictness") == "strict";
        return ApplyWithKey(chords, parameters, key, strict);
    }

    protected abstract TransformResult ApplyWithKey(
        IReadOnlyList<ChordSymbol> chords,
        IReadOnlyDictionary<string, object> parameters,
        (int RootPc, bool IsMinor)? key,
        bool strict);

    /// <summary>D46: default out-of-key handling — root and quality left as-is, always flagged (never silent).</summary>
    protected static void FlagOutOfKey(List<TransformNote> notes, int index, ChordSymbol chord) =>
        notes.Add(new TransformNote(index, "out-of-key",
            $"'{chord.ToSymbol()}' is not in the inferred key — chromatic/borrowed chord left unchanged (D46)."));

    /// <summary>Renders a key as the plain note text a "key" param reads back, e.g. "C", "Am", "F#".</summary>
    public static string FormatKey(int rootPc, bool isMinor) =>
        Note.Spell(rootPc, Letter.C).ToString() + (isMinor ? "m" : string.Empty);

    private (int RootPc, bool IsMinor)? ResolveKey(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var overrideText = TransformParams.GetString(parameters, "key");
        if (!string.IsNullOrWhiteSpace(overrideText))
        {
            return ParseKeyOverride(overrideText);
        }
        return KeyInference.InferKey(chords.Select(c => Expander.Expand(c)).ToArray());
    }

    private static (int RootPc, bool IsMinor) ParseKeyOverride(string text)
    {
        var trimmed = text.Trim();
        var isMinor = trimmed.Length > 1 && trimmed.EndsWith('m');
        var noteText = isMinor ? trimmed[..^1] : trimmed;
        var note = Note.Parse(noteText);
        return (note.PitchClass.Value, isMinor);
    }
}
