using Dadabe.Editor.Slices;

namespace Dadabe.Editor.Services;

/// <summary>
/// Renders a per-string transition (v0.5.2 §10) the way a guitarist would
/// actually read it — "only the B string moves, 2 frets" and "common tones
/// on strings 1, 2, 5 — hold them" — rather than the bare scalar distance.
/// </summary>
public static class VoiceMoveSummary
{
    public static string Describe(IReadOnlyList<VoiceMoveRow>? moves, int stringCount)
    {
        if (moves is null) { return "—"; }
        if (moves.Count == 0) { return "Pivot — no strings move"; }

        var held = stringCount - moves.Count;
        if (moves.Count == 1)
        {
            var m = moves[0];
            var from = m.FromFret is null ? "x" : m.FromFret.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var to = m.ToFret is null ? "x" : m.ToFret.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var plural = m.Distance == 1 ? "" : "s";
            var summary = $"Only string {m.String + 1} moves ({from}→{to}, {m.Distance} fret{plural})";
            return held > 0 ? $"{summary} — {held} common tone{(held == 1 ? "" : "s")} held" : summary;
        }

        return held > 0
            ? $"{moves.Count} strings move — {held} common tone{(held == 1 ? "" : "s")} held"
            : $"{moves.Count} strings move";
    }
}
