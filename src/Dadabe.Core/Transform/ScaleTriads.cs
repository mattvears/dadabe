namespace Dadabe.Core.Transform;

/// <summary>
/// Generalizes the fixed degree/quality table <c>KeyInference.IsDiatonicQuality</c>
/// hardcodes for major/minor into a function of any 7-note scale, so the
/// key-aware transforms (D47) can reuse one implementation for the tonic
/// key's own scale and for an arbitrary target mode (<c>parallel-mode</c>).
/// </summary>
public static class ScaleTriads
{
    /// <summary>0-based degree of <paramref name="pc"/> in the scale rooted at <paramref name="keyRootPc"/>, or null if it isn't in the scale.</summary>
    public static int? DegreeOf(int pc, int keyRootPc, IReadOnlyList<int> scaleIntervals)
    {
        var normalizedPc = ((pc % 12) + 12) % 12;
        for (var d = 0; d < scaleIntervals.Count; d++)
        {
            if ((keyRootPc + scaleIntervals[d]) % 12 == normalizedPc) { return d; }
        }
        return null;
    }

    /// <summary>
    /// Grammar quality string ("" major, "m" minor, "dim" diminished, "aug"
    /// augmented) for the triad stacked in thirds on <paramref name="degree"/>
    /// of <paramref name="scaleIntervals"/>, or null if that stack isn't one
    /// of those four shapes.
    /// </summary>
    public static string? TriadQuality(IReadOnlyList<int> scaleIntervals, int degree) =>
        (ThirdAbove(scaleIntervals, degree, 2), ThirdAbove(scaleIntervals, degree, 4)) switch
        {
            (4, 7) => "",
            (3, 7) => "m",
            (3, 6) => "dim",
            (4, 8) => "aug",
            _ => null,
        };

    /// <summary>The three pitch classes (root, third, fifth) of the triad stacked on <paramref name="degree"/>.</summary>
    public static int[] TriadPitchClasses(int keyRootPc, IReadOnlyList<int> scaleIntervals, int degree)
    {
        var root = (keyRootPc + scaleIntervals[degree]) % 12;
        var third = (keyRootPc + scaleIntervals[(degree + 2) % scaleIntervals.Count]) % 12;
        var fifth = (keyRootPc + scaleIntervals[(degree + 4) % scaleIntervals.Count]) % 12;
        return [root, third, fifth];
    }

    private static int ThirdAbove(IReadOnlyList<int> scale, int degree, int steps)
    {
        var value = scale[(degree + steps) % scale.Count] - scale[degree];
        return value < 0 ? value + 12 : value;
    }
}
