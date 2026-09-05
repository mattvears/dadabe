using Dadabe.Core.Chord;

namespace Dadabe.Core.Transform;

/// <summary>
/// <c>substitute</c> (D47): replaces a diatonic chord with another sharing
/// its harmonic function — tonic {I, iii, vi}, subdominant {ii, IV}, dominant
/// {V, vii°} — chosen by shared common tones rather than a fixed per-key
/// table, so the rule generalises to any key/mode from
/// <see cref="KeyInference"/> alone. A chord outside the inferred key falls
/// back to D46's default; no substitution is attempted for it. Not
/// invertible — substitution isn't generally self-inverse.
/// </summary>
internal sealed class SubstituteTransform(ChordGrammar grammar) : KeyAwareTransform(grammar)
{
    // Degree-keyed, not key-specific — the same table generalises to any key/mode.
    private static readonly Dictionary<int, int[]> GroupByDegree = new()
    {
        [0] = [0, 2, 5], [2] = [0, 2, 5], [5] = [0, 2, 5], // tonic: I, iii, vi
        [1] = [1, 3], [3] = [1, 3],                        // subdominant: ii, IV
        [4] = [4, 6], [6] = [4, 6],                        // dominant: V, vii°
    };

    public override string Id => "substitute";
    public override string DisplayName => "Substitute";
    public override bool IsInvertible => false;

    protected override TransformResult ApplyWithKey(
        IReadOnlyList<ChordSymbol> chords,
        IReadOnlyDictionary<string, object> parameters,
        (int RootPc, bool IsMinor)? key,
        bool strict)
    {
        var variant = TransformParams.GetInt(parameters, "variant", 0);
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

            var degree = ScaleTriads.DegreeOf(chord.Root.PitchClass.Value, key.Value.RootPc, scale);
            if (degree is null)
            {
                FlagOutOfKey(notes, i, chord);
                results.Add(chord);
                continue;
            }

            var chordTones = Expander.Expand(chord).PitchClasses.Select(pc => pc.Value).ToHashSet();
            var candidateDegree = ChooseCandidate(chordTones, key.Value.RootPc, scale, degree.Value, variant);
            if (candidateDegree is null)
            {
                notes.Add(new TransformNote(i, "skipped", $"'{chord.ToSymbol()}' has no functional-group substitute."));
                results.Add(chord);
                continue;
            }

            var newQuality = ScaleTriads.TriadQuality(scale, candidateDegree.Value);
            if (newQuality is null)
            {
                notes.Add(new TransformNote(i, "skipped", $"Degree {candidateDegree} has no plain triad to substitute in."));
                results.Add(chord);
                continue;
            }

            if (!ChordApproximation.TryApplyTriadQuality(chord, newQuality, strict, DisplayName, i, notes, out var requalified))
            {
                results.Add(chord);
                continue;
            }

            var newRootPc = (key.Value.RootPc + scale[candidateDegree.Value]) % 12;
            var newRoot = Note.Spell(newRootPc, chord.Root.Letter);
            results.Add(requalified with { Root = newRoot });
        }

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), notes, Invertible: false);
    }

    /// <summary>
    /// Ranks the other members of <paramref name="degree"/>'s functional
    /// group by shared common tones with the actual chord (verifying the
    /// "&gt;=2 common tones" rule live, rather than trusting the table
    /// blindly), ties broken toward the farthest degree — which is what
    /// makes <c>vi</c> (not <c>iii</c>) the default tonic substitute, the
    /// conventional choice.
    /// </summary>
    private static int? ChooseCandidate(HashSet<int> chordTones, int keyRootPc, int[] scale, int degree, int variant)
    {
        if (!GroupByDegree.TryGetValue(degree, out var group)) { return null; }

        var ranked = group
            .Where(d => d != degree)
            .Select(d => (Degree: d, Common: ScaleTriads.TriadPitchClasses(keyRootPc, scale, d).Count(chordTones.Contains)))
            .OrderByDescending(c => c.Common)
            .ThenByDescending(c => Math.Abs(c.Degree - degree))
            .ToArray();

        if (ranked.Length == 0) { return null; }
        return ranked[Math.Clamp(variant, 0, ranked.Length - 1)].Degree;
    }
}
