using Dadabe.Core.Chord;

namespace Dadabe.Core.Transform;

/// <summary>
/// <c>diatonic-transpose</c> (D47): moves each chord by scale degrees, not
/// semitones — quality changes as a side effect of staying on the scale
/// (in C major, <c>[C,F,G]</c> up one degree is <c>[Dm,G,Am]</c>). A chord
/// outside the inferred key falls back to D46's default: shift the actual
/// root by the semitone delta its nearest diatonic neighbour would move,
/// quality left alone, always flagged. Not invertible in general — an
/// out-of-key chord's approximated motion does not round-trip.
/// </summary>
internal sealed class DiatonicTransposeTransform(ChordGrammar grammar) : KeyAwareTransform(grammar)
{
    public override string Id => "diatonic-transpose";
    public override string DisplayName => "Diatonic Transpose";
    public override bool IsInvertible => false;

    protected override TransformResult ApplyWithKey(
        IReadOnlyList<ChordSymbol> chords,
        IReadOnlyDictionary<string, object> parameters,
        (int RootPc, bool IsMinor)? key,
        bool strict)
    {
        var by = TransformParams.GetInt(parameters, "by", 1);
        var positions = TransformParams.GetPositions(parameters, chords.Count);
        var scale = key!.Value.IsMinor ? KeyInference.MinorIntervals : KeyInference.MajorIntervals;

        var notes = new List<TransformNote>();
        var results = new List<ChordSymbol>(chords.Count);

        for (var i = 0; i < chords.Count; i++)
        {
            var chord = chords[i];
            if (!positions.Contains(i))
            {
                results.Add(chord);
                continue;
            }

            var rootPc = chord.Root.PitchClass.Value;
            var distFromKey = ((rootPc - key.Value.RootPc) % 12 + 12) % 12;
            var lowerDegree = NearestDegreeAtOrBelow(scale, distFromKey);
            var isDiatonic = scale[lowerDegree] == distFromKey;

            var motion = DegreeSemitone(scale, lowerDegree + by) - DegreeSemitone(scale, lowerDegree);
            var newRootPc = ((rootPc + motion) % 12 + 12) % 12;
            var newLetter = Note.LetterPlus(chord.Root.Letter, by);
            var newRoot = Note.Spell(newRootPc, newLetter);

            if (!isDiatonic)
            {
                FlagOutOfKey(notes, i, chord);
                results.Add(chord with { Root = newRoot });
                continue;
            }

            var newDegree = ((lowerDegree + by) % 7 + 7) % 7;
            var newQuality = ScaleTriads.TriadQuality(scale, newDegree);
            if (newQuality is null)
            {
                notes.Add(new TransformNote(i, "skipped", $"Degree {newDegree} of the target scale has no plain triad."));
                results.Add(chord);
                continue;
            }

            if (!ChordApproximation.TryApplyTriadQuality(chord, newQuality, strict, DisplayName, i, notes, out var requalified))
            {
                results.Add(chord);
                continue;
            }

            results.Add(requalified with { Root = newRoot });
        }

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), notes, Invertible: false);
    }

    /// <summary>Largest scale degree (0..6) whose interval is &lt;= <paramref name="distFromKey"/> — exact for a diatonic chord, an approximation anchor otherwise.</summary>
    private static int NearestDegreeAtOrBelow(int[] scale, int distFromKey)
    {
        var best = 0;
        for (var d = 0; d < scale.Length; d++)
        {
            if (scale[d] <= distFromKey) { best = d; }
        }
        return best;
    }

    /// <summary>Continuous (octave-aware) semitone offset of a possibly out-of-range degree index.</summary>
    private static int DegreeSemitone(int[] scale, int degree)
    {
        var octave = (int)Math.Floor(degree / 7.0);
        var local = ((degree % 7) + 7) % 7;
        return (octave * 12) + scale[local];
    }
}
