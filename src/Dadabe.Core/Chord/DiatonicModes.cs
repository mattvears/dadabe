using System.Collections.Immutable;

namespace Dadabe.Core.Chord;

/// <summary>
/// The seven rotations of the major scale as semitone-interval sets from
/// each mode's own tonic (D47 <c>parallel-mode</c>). This is deliberately
/// not the Editor's reference-glossary <c>ModeModel</c> catalog (31 modes
/// including pentatonics, whole-tone, and the melodic/harmonic-minor
/// family): those don't have a triad on every scale degree, are out of
/// scope for "rebuild each chord on its degree", and live as file-backed
/// instance data an operator can edit or delete — a transform's
/// correctness must not depend on that. Values here are cross-checked
/// against the shipped <c>data/reference/modes/*.json</c> entries in
/// <c>DiatonicModesTests</c>.
/// </summary>
public static class DiatonicModes
{
    public static readonly IReadOnlyDictionary<string, ImmutableArray<int>> ByName = BuildTable();

    private static Dictionary<string, ImmutableArray<int>> BuildTable()
    {
        string[] names = ["ionian", "dorian", "phrygian", "lydian", "mixolydian", "aeolian", "locrian"];
        var table = new Dictionary<string, ImmutableArray<int>>(StringComparer.OrdinalIgnoreCase);
        for (var degree = 0; degree < names.Length; degree++)
        {
            table[names[degree]] = Rotate(KeyInference.MajorIntervals, degree);
        }
        table["major"] = table["ionian"];
        table["minor"] = table["aeolian"];
        return table;
    }

    private static ImmutableArray<int> Rotate(int[] majorIntervals, int degree) =>
        [.. Enumerable.Range(0, 7).Select(i => (majorIntervals[(degree + i) % 7] - majorIntervals[degree] + 12) % 12)];
}
