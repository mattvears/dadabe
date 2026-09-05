namespace Dadabe.Editor.Services;

/// <summary>
/// Short definitions for voicing structure names, shown as inline tooltips (D35).
/// </summary>
public static class VoicingStructureGlossary
{
    private static readonly Dictionary<string, string> Definitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["power"] = "Root and fifth only — no third, so it is neither major nor minor.",
        ["triad"] = "Root, third, fifth — the fundamental three-note chord.",
        ["shell"] = "Root + 3rd/7th only; omits 5th for a lean, functional sound.",
        ["drop-2"] = "Second-highest voice of a close-position chord dropped an octave; wide, open sound.",
        ["drop-3"] = "Third-highest voice dropped an octave; even wider spacing than drop-2.",
        ["spread"] = "No specific interval rule; spans more than one octave across the strings.",
    };

    public static string Describe(string structure) =>
        Definitions.TryGetValue(structure, out var def) ? def : structure;
}
