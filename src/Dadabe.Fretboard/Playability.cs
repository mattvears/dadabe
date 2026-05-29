namespace Dadabe.Fretboard;

/// <summary>
/// Cheap geometric prunes used during the search to drop obviously
/// impossible candidates before the fingering solver runs (design.md §7
/// step 4). Each predicate is O(n) in the string count.
/// </summary>
public static class Playability
{
    /// <summary>
    /// True if the candidate fits the hand's geometric envelope: span ≤
    /// <see cref="HandModel.MaxSpan"/> and sounded-string count in
    /// <see cref="HandModel.MinStrings"/>..<see cref="HandModel.MaxStrings"/>.
    /// </summary>
    public static bool Admissible(ReadOnlySpan<int> candidate, HandModel hand)
    {
        ArgumentNullException.ThrowIfNull(hand);
        var sounded = 0;
        var minFret = int.MaxValue;
        var maxFret = int.MinValue;
        foreach (var f in candidate)
        {
            if (f < 0) { continue; }
            sounded++;
            if (f > 0)
            {
                if (f < minFret) { minFret = f; }
                if (f > maxFret) { maxFret = f; }
            }
        }
        if (sounded < hand.MinStrings || sounded > hand.MaxStrings) { return false; }
        if (maxFret == int.MinValue) { return true; }  // only open strings
        return maxFret - minFret <= hand.MaxSpan;
    }
}
