namespace Dadabe.Core.Chord;

/// <summary>
/// Infers the most likely key centre from a list of context chord specs (D33).
/// Returns null when the context is empty or coverage falls below 50%.
/// </summary>
public static class KeyInference
{
    // Diatonic pitch-class sets for all 24 major/minor keys.
    // Major: W W H W W W H  (2 2 1 2 2 2 1 semitones)
    // Natural minor: W H W W H W W
    private static readonly int[] MajorIntervals = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] MinorIntervals = [0, 2, 3, 5, 7, 8, 10];

    /// <summary>
    /// Infer the key from context chords.
    /// </summary>
    /// <param name="context">
    /// Chord specs from the preceding progression, ordered oldest-first.
    /// </param>
    /// <returns>
    /// The inferred key (root pitch class, isMinor), or null when inference
    /// fails (empty context or below-50% coverage).
    /// </returns>
    public static (int RootPc, bool IsMinor)? InferKey(IReadOnlyList<ChordSpec> context)
    {
        if (context.Count == 0) return null;

        // Score each of the 24 keys against the context.
        // Later chords in context are weighted more heavily.
        double bestScore = -1;
        (int RootPc, bool IsMinor) bestKey = default;

        for (int root = 0; root < 12; root++)
        {
            var majorSet = BuildDiatonicSet(root, MajorIntervals);
            var minorSet = BuildDiatonicSet(root, MinorIntervals);

            double majorScore = ScoreContext(context, majorSet);
            double minorScore = ScoreContext(context, minorSet);

            if (majorScore > bestScore) { bestScore = majorScore; bestKey = (root, false); }
            if (minorScore > bestScore) { bestScore = minorScore; bestKey = (root, true); }
        }

        // Require at least 50% coverage (max possible score is context.Count).
        double maxScore = context
            .Select((_, i) => 1.0 + 0.2 * (context.Count - 1 - i))
            .Sum();
        if (maxScore <= 0 || bestScore / maxScore < 0.5) return null;

        return bestKey;
    }

    /// <summary>
    /// Scores a set of context chords against a diatonic pitch-class set.
    /// Recency weight: chord at index i from the end contributes (1 + 0.2 * (distance from end)).
    /// </summary>
    private static double ScoreContext(IReadOnlyList<ChordSpec> context, HashSet<int> diatonicPcs)
    {
        double score = 0;
        for (int i = 0; i < context.Count; i++)
        {
            int distFromEnd = context.Count - 1 - i;
            double weight = 1.0 + 0.2 * distFromEnd;
            if (diatonicPcs.Contains(context[i].Root.PitchClass.Value))
                score += weight;
        }
        return score;
    }

    private static HashSet<int> BuildDiatonicSet(int root, int[] intervals)
    {
        var set = new HashSet<int>(7);
        foreach (var interval in intervals)
            set.Add((root + interval) % 12);
        return set;
    }

    /// <summary>
    /// Returns the diatonic pitch-class set for the given key.
    /// </summary>
    public static HashSet<int> DiatonicSet(int rootPc, bool isMinor) =>
        BuildDiatonicSet(rootPc, isMinor ? MinorIntervals : MajorIntervals);

    /// <summary>
    /// Classifies a candidate's relationship to the inferred key (D33).
    /// </summary>
    public static CandidateRelationship Classify(
        string candidateSymbol,
        (int RootPc, bool IsMinor) key,
        int lastChordRootPc)
    {
        if (string.IsNullOrEmpty(candidateSymbol)) return CandidateRelationship.Unrelated;

        // Parse root and quality suffix.
        int i = 1;
        while (i < candidateSymbol.Length && candidateSymbol[i] is 'b' or '#') i++;
        var rootStr = candidateSymbol[..i];
        var suffix = candidateSymbol[i..];

        if (!TryParseRoot(rootStr, out int candidatePc)) return CandidateRelationship.Unrelated;

        var diatonic = DiatonicSet(key.RootPc, key.IsMinor);

        // Diatonic: root in diatonic set + quality consistent with diatonic chord at that degree.
        if (diatonic.Contains(candidatePc) && IsDiatonicQuality(candidatePc, key, suffix))
            return CandidateRelationship.Diatonic;

        // Valid non-diatonic: recognised harmonic moves.
        int interval = (candidatePc - lastChordRootPc + 12) % 12;
        if (IsValidNonDiatonic(interval, candidatePc, suffix, key))
            return CandidateRelationship.ValidNonDiatonic;

        return CandidateRelationship.Unrelated;
    }

    private static bool IsDiatonicQuality(int pc, (int RootPc, bool IsMinor) key, string suffix)
    {
        // Determine the diatonic degree (0-based) for this pc in the key.
        var intervals = key.IsMinor ? MinorIntervals : MajorIntervals;
        int degree = -1;
        for (int d = 0; d < intervals.Length; d++)
        {
            if ((key.RootPc + intervals[d]) % 12 == pc) { degree = d; break; }
        }
        if (degree < 0) return false;

        // Expected qualities per degree in major: M m m M M m dim
        // Expected qualities per degree in minor: m dim M m M M M (natural minor)
        bool major = !key.IsMinor;
        bool isMinorQuality = suffix is "m" or "m7" or "m9" or "m11" or "m13" or "m7b5" or "mMaj7";
        bool isMajorQuality = suffix is "" or "maj7" or "6" or "maj9";
        bool isDomQuality = suffix is "7" or "9" or "11" or "13";
        bool isDimQuality = suffix is "dim" or "dim7" or "m7b5";

        int[] minorDegreeTypes = major
            ? [0, 1, 1, 0, 0, 1, 2] // M m m M M m dim
            : [1, 2, 0, 1, 1, 0, 0]; // m dim M m M M M
        // 0=major 1=minor 2=dim

        return minorDegreeTypes[degree] switch
        {
            0 => isMajorQuality || isDomQuality,
            1 => isMinorQuality,
            2 => isDimQuality || isMinorQuality,
            _ => false,
        };
    }

    private static bool IsValidNonDiatonic(int interval, int candidatePc, string suffix, (int RootPc, bool IsMinor) key)
    {
        // Tritone substitution: interval 6
        if (interval == 6) return true;
        // Chromatic mediant: interval 3 or 4
        if (interval is 3 or 4) return true;
        // Chromatic approach: semitone above or below
        if (interval is 1 or 11) return true;
        // Borrowed chord (parallel mode root):
        var parallelDiatonic = DiatonicSet(key.RootPc, !key.IsMinor);
        if (parallelDiatonic.Contains(candidatePc)) return true;
        // Secondary dominant: dominant 7th that resolves by P5 to a diatonic chord.
        bool isDom7 = suffix is "7" or "9" or "11" or "13";
        if (isDom7)
        {
            int resolutionPc = (candidatePc + 5) % 12; // resolve P5 down = +5 semitones up
            var diatonic = DiatonicSet(key.RootPc, key.IsMinor);
            if (diatonic.Contains(resolutionPc)) return true;
        }
        return false;
    }

    private static bool TryParseRoot(string root, out int pc)
    {
        pc = 0;
        if (root.Length == 0) return false;
        int natural = root[0] switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5,
            'G' => 7, 'A' => 9, 'B' => 11, _ => -1,
        };
        if (natural < 0) return false;
        int acc = 0;
        for (int i = 1; i < root.Length; i++)
        {
            if (root[i] == '#') acc++;
            else if (root[i] == 'b') acc--;
            else return false;
        }
        pc = ((natural + acc) % 12 + 12) % 12;
        return true;
    }
}

public enum CandidateRelationship
{
    Diatonic,
    ValidNonDiatonic,
    Unrelated,
}
