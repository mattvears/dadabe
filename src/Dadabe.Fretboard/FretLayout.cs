using Dadabe.Core;

namespace Dadabe.Fretboard;

/// <summary>
/// Enumerates every fret on every string up to a given fret window — the
/// raw chord-agnostic board. Spelling carries the open string's natural
/// spelling; chord-context spelling is layered on by <see cref="Reachability"/>.
/// </summary>
public static class FretLayout
{
    public static IEnumerable<BoardPosition> Positions(Tuning tuning, int maxFret)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentOutOfRangeException.ThrowIfNegative(maxFret);
        for (var s = 0; s < tuning.Strings.Length; s++)
        {
            var open = tuning.Strings[s];
            for (var f = 0; f <= maxFret; f++)
            {
                yield return new BoardPosition(s, f, open.Transpose(f));
            }
        }
    }
}
