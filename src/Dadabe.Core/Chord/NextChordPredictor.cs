namespace Dadabe.Core.Chord;

/// <summary>Next-chord candidate with its predicted probability.</summary>
public sealed record NextChordCandidate(string Symbol, double Probability);

/// <summary>
/// Rules-based next-chord prediction (D24 / v0.2). Given a chord spec,
/// generates musically meaningful candidates, scores them, applies a
/// softmax whose temperature is derived from <paramref name="entropy"/>,
/// and returns the top-N by probability.
/// </summary>
public static class NextChordPredictor
{
    /// <summary>
    /// Predict the most likely next chords.
    /// </summary>
    /// <param name="spec">The current chord.</param>
    /// <param name="topN">Number of candidates to return. 0 returns an empty list.</param>
    /// <param name="entropy">
    /// Controls prediction diversity. Lower → sharper / more confident.
    /// Higher → flatter / more exploratory.
    /// </param>
    public static IReadOnlyList<NextChordCandidate> Predict(ChordSpec spec, int topN, double entropy)
    {
        if (topN <= 0) { return Array.Empty<NextChordCandidate>(); }

        var rootPc = spec.Root.PitchClass.Value;
        var rawCandidates = BuildCandidates(rootPc, spec.Quality);

        var scores = rawCandidates.Select(c => c.Score).ToArray();
        var probs = Softmax(scores, EntropyToTemperature(entropy));

        return rawCandidates
            .Zip(probs, (c, p) => new NextChordCandidate(c.Symbol, Math.Round(p, 4)))
            .OrderByDescending(c => c.Probability)
            .Take(topN)
            .ToList();
    }

    private static double EntropyToTemperature(double entropy)
        => Math.Clamp(1.0 / Math.Max(entropy, 0.01), 0.1, 10.0);

    private static double[] Softmax(double[] scores, double temperature)
    {
        var scaled = scores.Select(s => s / temperature).ToArray();
        var max = scaled.Max();
        var exps = scaled.Select(s => Math.Exp(s - max)).ToArray();
        var sum = exps.Sum();
        return exps.Select(e => e / sum).ToArray();
    }

    private static List<(string Symbol, double Score)> BuildCandidates(int rootPc, string quality)
    {
        var progressions = GetProgressions(ClassifyQuality(quality));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(string Symbol, double Score)>();
        foreach (var (intervalSemitones, suffix, score) in progressions)
        {
            var targetPc = (rootPc + intervalSemitones + 12) % 12;
            var symbol = PcName(targetPc) + suffix;
            if (seen.Add(symbol)) { result.Add((symbol, score)); }
        }
        return result;
    }

    // (interval in semitones from root, quality suffix, raw score)
    private static IEnumerable<(int, string, double)> GetProgressions(ChordQualityFamily family) =>
        family switch
        {
            ChordQualityFamily.Major =>
            [
                (5,  "",    3.0),   // IV
                (7,  "",    2.5),   // V
                (9,  "m",   2.0),   // vi
                (2,  "m",   2.0),   // ii
                (10, "",    1.5),   // bVII
                (4,  "m",   1.0),   // iii
                (0,  "maj7",1.0),   // I as maj7
            ],
            ChordQualityFamily.Maj7 =>
            [
                (5,  "maj7", 2.5),  // IVmaj7
                (7,  "7",    2.0),  // V7
                (2,  "m7",   2.0),  // iim7
                (9,  "m7",   1.5),  // vim7
                (4,  "m7",   1.0),  // iiim7
                (10, "maj7", 1.0),  // bVIImaj7
            ],
            ChordQualityFamily.Minor =>
            [
                (3,  "",    2.5),   // III
                (8,  "",    2.0),   // VI
                (10, "",    2.0),   // VII
                (5,  "m",   1.5),   // iv
                (7,  "m",   1.0),   // v
                (7,  "7",   1.5),   // V7 (borrowed)
                (0,  "m7",  1.0),   // i as m7
            ],
            ChordQualityFamily.M7 =>
            [
                (3,  "maj7", 2.5),  // IIImaj7
                (8,  "maj7", 2.0),  // VImaj7
                (10, "7",    2.0),  // VII7
                (5,  "m7",   1.5),  // ivm7
                (7,  "7",    1.5),  // V7
                (2,  "m7b5", 1.0),  // iim7b5
            ],
            ChordQualityFamily.Dominant =>
            [
                (5,  "",    3.0),   // IV (resolution target)
                (0,  "",    2.5),   // I (tonic resolution)
                (6,  "7",   2.0),   // bII7 (tritone sub)
                (2,  "m",   1.5),   // ii
                (5,  "m",   1.0),   // iv
                (10, "7",   1.0),   // bVII7
            ],
            ChordQualityFamily.Diminished =>
            [
                (1,  "",    3.0),   // semitone above → major
                (1,  "m",   2.5),   // semitone above → minor
                (6,  "7",   2.0),   // tritone → dom7
                (3,  "",    1.5),   // minor third up → major
                (9,  "m7",  1.0),   // sixth up
            ],
            ChordQualityFamily.Augmented =>
            [
                (5,  "",    2.5),   // IV
                (0,  "",    2.0),   // tonic (common resolution)
                (8,  "",    2.0),   // resolution up a minor third
                (7,  "7",   1.5),   // V7
                (10, "",    1.0),   // bVII
            ],
            ChordQualityFamily.Sus =>
            [
                (0,  "",    2.5),   // resolve to major
                (5,  "",    2.5),   // IV
                (7,  "",    2.0),   // V
                (9,  "m",   1.5),   // vi
                (2,  "m",   1.0),   // ii
            ],
            _ =>
            [
                (5,  "",    3.0),
                (7,  "",    2.5),
                (9,  "m",   2.0),
                (2,  "m",   2.0),
                (10, "",    1.5),
            ],
        };

    private static ChordQualityFamily ClassifyQuality(string quality) => quality switch
    {
        "maj"  or "6"                           => ChordQualityFamily.Major,
        "maj7" or "maj9" or "maj11" or "maj13"  => ChordQualityFamily.Maj7,
        "m"    or "m6"                          => ChordQualityFamily.Minor,
        "m7"   or "m9"   or "m11"  or "m13"
               or "mMaj7"                       => ChordQualityFamily.M7,
        "7"    or "9"    or "11"   or "13"      => ChordQualityFamily.Dominant,
        "dim"  or "dim7" or "m7b5"              => ChordQualityFamily.Diminished,
        "aug"                                   => ChordQualityFamily.Augmented,
        "sus2" or "sus4"                        => ChordQualityFamily.Sus,
        _                                       => ChordQualityFamily.Major,
    };

    private static string PcName(int pc) => pc switch
    {
        0  => "C",
        1  => "Db",
        2  => "D",
        3  => "Eb",
        4  => "E",
        5  => "F",
        6  => "Gb",
        7  => "G",
        8  => "Ab",
        9  => "A",
        10 => "Bb",
        11 => "B",
        _  => throw new ArgumentOutOfRangeException(nameof(pc)),
    };
}

internal enum ChordQualityFamily
{
    Major, Maj7, Minor, M7, Dominant, Diminished, Augmented, Sus,
}
