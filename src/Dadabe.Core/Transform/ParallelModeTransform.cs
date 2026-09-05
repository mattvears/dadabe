using Dadabe.Core.Chord;

namespace Dadabe.Core.Transform;

/// <summary>
/// <c>parallel-mode</c> (D47): rebuilds each chord on its scale degree in a
/// different mode of the same tonic (e.g. C major → C mixolydian moves only
/// the chord built on scale degree 4). Selective borrowing —
/// <c>[C,F,G] → [Cm,F,G]</c> — is this transform restricted to a
/// <c>positions</c> subset, not its own transform. Chords outside the
/// inferred key fall back to D46's default regardless of the target mode.
/// Not invertible (out-of-key chords and mode collisions both lose
/// information).
/// </summary>
internal sealed class ParallelModeTransform(ChordGrammar grammar) : KeyAwareTransform(grammar)
{
    public override string Id => "parallel-mode";
    public override string DisplayName => "Parallel Mode";
    public override bool IsInvertible => false;

    protected override TransformResult ApplyWithKey(
        IReadOnlyList<ChordSymbol> chords,
        IReadOnlyDictionary<string, object> parameters,
        (int RootPc, bool IsMinor)? key,
        bool strict)
    {
        var mode = TransformParams.GetString(parameters, "mode")
            ?? throw new ArgumentException("parallel-mode requires a 'mode'.");
        if (!DiatonicModes.ByName.TryGetValue(mode, out var targetScale))
        {
            throw new ArgumentException($"'{mode}' is not a known mode.");
        }

        var positions = TransformParams.GetPositions(parameters, chords.Count);
        var sourceScale = key!.Value.IsMinor ? KeyInference.MinorIntervals : KeyInference.MajorIntervals;

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

            var degree = ScaleTriads.DegreeOf(chord.Root.PitchClass.Value, key.Value.RootPc, sourceScale);
            if (degree is null)
            {
                FlagOutOfKey(notes, i, chord);
                results.Add(chord);
                continue;
            }

            var newQuality = ScaleTriads.TriadQuality(targetScale, degree.Value);
            if (newQuality is null)
            {
                notes.Add(new TransformNote(i, "skipped", $"Degree {degree} of '{mode}' has no plain triad."));
                results.Add(chord);
                continue;
            }

            var newRootPc = (key.Value.RootPc + targetScale[degree.Value]) % 12;
            var newRoot = Note.Spell(newRootPc, chord.Root.Letter);
            var moved = newRootPc != chord.Root.PitchClass.Value || newQuality != chord.Quality;

            if (!ChordApproximation.TryApplyTriadQuality(chord, newQuality, strict, DisplayName, i, notes, out var requalified))
            {
                results.Add(chord);
                continue;
            }

            // D47: the mode-shift itself is a visible diff, not an approximation — flagged even in strict mode.
            if (moved)
            {
                notes.Add(new TransformNote(i, "mode-shift", $"'{chord.ToSymbol()}' moved to '{mode}' at scale degree {degree}."));
            }

            results.Add(requalified with { Root = newRoot });
        }

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), notes, Invertible: false);
    }
}
