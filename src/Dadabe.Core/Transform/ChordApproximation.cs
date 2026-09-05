using Dadabe.Core.Chord;

namespace Dadabe.Core.Transform;

/// <summary>
/// The "loose" half of D54: approximate rather than skip. Shared by
/// <see cref="ReduceTransform"/> (which needs only the triad/seventh core
/// table) and the key-aware family (D47), which additionally needs to swap
/// a triad quality while keeping the original extensions/alterations verbatim.
/// </summary>
internal static class ChordApproximation
{
    /// <summary>DisplayName -&gt; (triad core, seventh core).</summary>
    public static readonly IReadOnlyDictionary<string, (string Triad, string Seventh)> CoreByQuality = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["maj7"] = ("", "maj7"),
        ["maj9"] = ("", "maj7"),
        ["maj11"] = ("", "maj7"),
        ["maj13"] = ("", "maj7"),
        ["maj"] = ("", "maj"),
        [""] = ("", ""),
        ["m"] = ("m", "m"),
        ["m6"] = ("m", "m7"),
        ["m7"] = ("m", "m7"),
        ["m9"] = ("m", "m7"),
        ["m11"] = ("m", "m7"),
        ["m13"] = ("m", "m7"),
        ["mMaj7"] = ("m", "mMaj7"),
        ["6"] = ("", "7"),
        ["7"] = ("", "7"),
        ["9"] = ("", "7"),
        ["11"] = ("", "7"),
        ["13"] = ("", "7"),
        ["m7b5"] = ("dim", "m7b5"),
        ["dim7"] = ("dim", "dim7"),
        ["dim"] = ("dim", "dim"),
        ["aug"] = ("aug", "aug"),
        ["sus4"] = ("sus4", "sus4"),
        ["sus2"] = ("sus2", "sus2"),
        ["5"] = ("5", "5"),
    };

    /// <summary>True if <paramref name="chord"/>'s quality has a known triad/seventh core.</summary>
    public static bool HasKnownCore(ChordSymbol chord) => CoreByQuality.ContainsKey(chord.Quality);

    /// <summary>
    /// Loose-mode approximation (D54): swap the triad quality, keep the
    /// original Extensions/Alterations tokens verbatim ("re-extend"). What a
    /// carried-over token means once the triad is a different quality is a
    /// judgment call D54 delegates to "approximate and flag" — the caller is
    /// expected to attach a note explaining that, and the result stays
    /// inspectable/undoable by the user.
    /// </summary>
    public static ChordSymbol ReplaceTriadQualityKeepingExtensions(ChordSymbol chord, string newTriadQuality) =>
        chord with { Quality = newTriadQuality };

    /// <summary>
    /// Shared D54 strict/loose branch for the key-aware family (D47): a
    /// chord already sitting on its triad core gets the new quality
    /// directly; one with a known core but sitting on a seventh/extended
    /// quality (e.g. <c>maj7</c>, <c>9</c>) is skipped in strict mode and
    /// triad-reduced (losing the seventh) before requalifying in loose mode;
    /// a chord with no known core at all is always skipped.
    /// </summary>
    public static bool TryApplyTriadQuality(
        ChordSymbol chord, string newTriadQuality, bool strict, string transformName,
        int index, List<TransformNote> notes, out ChordSymbol result)
    {
        if (!CoreByQuality.TryGetValue(chord.Quality, out var core))
        {
            notes.Add(new TransformNote(index, "skipped", $"'{chord.ToSymbol()}' has no known triad core for {transformName}."));
            result = chord;
            return false;
        }

        if (core.Triad == chord.Quality)
        {
            result = ReplaceTriadQualityKeepingExtensions(chord, newTriadQuality);
            return true;
        }

        if (strict)
        {
            notes.Add(new TransformNote(index, "skipped", $"'{chord.ToSymbol()}' is not a plain triad — {transformName} in strict mode only handles plain triads."));
            result = chord;
            return false;
        }

        notes.Add(new TransformNote(index, "approximated", $"'{chord.ToSymbol()}' triad-reduced before applying {transformName}."));
        result = ReplaceTriadQualityKeepingExtensions(chord, newTriadQuality);
        return true;
    }
}
