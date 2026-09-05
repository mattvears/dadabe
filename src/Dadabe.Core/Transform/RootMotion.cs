using Dadabe.Core.Chord;

namespace Dadabe.Core.Transform;

/// <summary>
/// Common semitone labels for named-interval transpose (D45), keyed the same
/// way <see cref="Interval.DefaultQuality"/> emits them.
/// </summary>
internal static class IntervalQualities
{
    public static readonly Dictionary<string, int> SemitonesByQuality = BuildTable();

    private static Dictionary<string, int> BuildTable()
    {
        var table = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var s = 0; s <= 21; s++)
        {
            var quality = Interval.DefaultQuality(s);
            table.TryAdd(quality, s);
        }
        return table;
    }
}

/// <summary>
/// <c>transpose</c> (D45): every root moves by a named interval or a
/// best-spelled semitone count; qualities are unchanged. Invertible by
/// negating the interval.
/// </summary>
public sealed class TransposeTransform : ITransformation
{
    public string Id => "transpose";
    public string DisplayName => "Transpose";
    public bool IsInvertible => true;

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var interval = ResolveInterval(chords, parameters);
        var notes = new List<TransformNote>();
        var results = new List<ChordSymbol>(chords.Count);

        for (var i = 0; i < chords.Count; i++)
        {
            var chord = chords[i];
            var newRoot = chord.Root.Transpose(interval, out var rootNote);
            if (rootNote is not null) { notes.Add(new TransformNote(i, "respelled", rootNote)); }

            Note? newBass = null;
            if (chord.Bass is { } bass)
            {
                newBass = bass.Transpose(interval, out var bassNote);
                if (bassNote is not null) { notes.Add(new TransformNote(i, "respelled", bassNote)); }
            }

            results.Add(chord with { Root = newRoot, Bass = newBass });
        }

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), notes, Invertible: true);
    }

    private static Interval ResolveInterval(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var label = TransformParams.GetString(parameters, "interval");
        if (label is not null)
        {
            if (!IntervalQualities.SemitonesByQuality.TryGetValue(label, out var semitones))
            {
                throw new ArgumentException($"Unknown interval quality '{label}'.");
            }
            return new Interval(semitones, label);
        }

        var semitoneRequest = TransformParams.GetInt(parameters, "semitones")
            ?? throw new ArgumentException("transpose requires either 'interval' or 'semitones'.");
        return BestSpelledInterval(semitoneRequest, chords.Select(c => c.Root).ToArray());
    }

    /// <summary>
    /// Picks the diatonic letter-step count whose resulting spelling has the
    /// fewest accidentals across every root, ties broken toward flats (D45).
    /// </summary>
    internal static Interval BestSpelledInterval(int semitones, IReadOnlyCollection<Note> roots)
    {
        Interval? best = null;
        var bestScore = int.MaxValue;
        var bestSigned = int.MaxValue;

        for (var letterSteps = 0; letterSteps < 7; letterSteps++)
        {
            var candidate = new Interval(semitones, (letterSteps + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            var score = 0;
            var signed = 0;
            foreach (var root in roots)
            {
                var transposed = root.Transpose(candidate);
                score += Math.Abs(transposed.Accidental);
                signed += transposed.Accidental;
            }

            if (score < bestScore || (score == bestScore && signed < bestSigned))
            {
                best = candidate;
                bestScore = score;
                bestSigned = signed;
            }
        }

        return best!.Value;
    }
}

/// <summary><c>retrograde</c> (D47): reverse the chord order. Its own inverse.</summary>
public sealed class RetrogradeTransform : ITransformation
{
    public string Id => "retrograde";
    public string DisplayName => "Retrograde";
    public bool IsInvertible => true;

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters) =>
        new(chords.Reverse().Select(c => c.ToSymbol()).ToArray(), [], Invertible: true);
}

/// <summary><c>rotate</c> (D47): cyclic shift by <c>by</c> positions. Inverse negates <c>by</c>.</summary>
public sealed class RotateTransform : ITransformation
{
    public string Id => "rotate";
    public string DisplayName => "Rotate";
    public bool IsInvertible => true;

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        if (chords.Count == 0)
        {
            return new TransformResult([], [], Invertible: true);
        }

        var by = TransformParams.GetInt(parameters, "by", 0);
        var n = chords.Count;
        var offset = ((by % n) + n) % n;
        var rotated = chords.Skip(offset).Concat(chords.Take(offset));
        return new TransformResult(rotated.Select(c => c.ToSymbol()).ToArray(), [], Invertible: true);
    }
}

/// <summary>
/// <c>invert</c> (D47): mirrors roots (not chord tones) about an axis note —
/// always emittable, unlike true pitch-class-set inversion. Its own inverse.
/// </summary>
public sealed class InvertTransform : ITransformation
{
    public string Id => "invert";
    public string DisplayName => "Invert";
    public bool IsInvertible => true;

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var axisText = TransformParams.GetString(parameters, "axis")
            ?? throw new ArgumentException("invert requires an 'axis' note.");
        var axis = Note.Parse(axisText);
        var axisPc = axis.PitchClass.Value;

        var results = chords.Select(chord =>
        {
            var mirroredRootPc = (2 * axisPc) - chord.Root.PitchClass.Value;
            var newRoot = Note.Spell(mirroredRootPc, chord.Root.Letter);

            Note? newBass = null;
            if (chord.Bass is { } bass)
            {
                var mirroredBassPc = (2 * axisPc) - bass.PitchClass.Value;
                newBass = Note.Spell(mirroredBassPc, bass.Letter);
            }

            return chord with { Root = newRoot, Bass = newBass };
        });

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), [], Invertible: true);
    }
}

/// <summary><c>interval-negate</c> (D49): negates the root-motion intervals. Its own inverse.</summary>
public sealed class IntervalNegateTransform : ITransformation
{
    public string Id => "interval-negate";
    public string DisplayName => "Negate Intervals";
    public bool IsInvertible => true;

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var newRoots = RootMotionSpelling.RebuildFromMotions(
            chords, motion => (12 - motion) % 12);
        var results = chords.Select((c, i) => c with { Root = newRoots[i] });
        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), [], Invertible: true);
    }
}

/// <summary><c>interval-reverse</c> (D49): reverses the motion list, re-derived from the original start. Its own inverse.</summary>
public sealed class IntervalReverseTransform : ITransformation
{
    public string Id => "interval-reverse";
    public string DisplayName => "Reverse Intervals";
    public bool IsInvertible => true;

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        if (chords.Count == 0)
        {
            return new TransformResult([], [], Invertible: true);
        }

        var pcs = chords.Select(c => c.Root.PitchClass.Value).ToArray();
        var motions = new int[pcs.Length - 1];
        for (var i = 0; i < motions.Length; i++) { motions[i] = ((pcs[i + 1] - pcs[i]) % 12 + 12) % 12; }
        Array.Reverse(motions);

        var newRoots = new Note[chords.Count];
        newRoots[0] = chords[0].Root;
        var pc = pcs[0];
        for (var i = 0; i < motions.Length; i++)
        {
            pc = (pc + motions[i]) % 12;
            newRoots[i + 1] = Note.Spell(pc, chords[i + 1].Root.Letter);
        }

        var results = chords.Select((c, i) => c with { Root = newRoots[i] });
        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), [], Invertible: true);
    }
}

/// <summary>
/// <c>interval-multiply</c> (D49): serial M5/M7 — multiplies each root's
/// absolute pitch class by 5 or 7 mod 12. Factor 7 maps chromatic steps onto
/// the circle of fifths; ship as an algorithmic-composition tool, not a
/// reharmonisation. Not invertible.
/// </summary>
public sealed class IntervalMultiplyTransform : ITransformation
{
    public string Id => "interval-multiply";
    public string DisplayName => "Multiply Intervals";
    public bool IsInvertible => false;

    public TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters)
    {
        var factor = TransformParams.GetInt(parameters, "factor", 7);
        if (factor is not (5 or 7))
        {
            throw new ArgumentException("interval-multiply requires factor 5 or 7.");
        }

        var results = chords.Select(chord =>
        {
            var pc = (chord.Root.PitchClass.Value * factor) % 12;
            var newRoot = Note.Spell(pc, chord.Root.Letter);
            return chord with { Root = newRoot };
        });

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), [], Invertible: false);
    }
}

/// <summary>Shared root-motion reconstruction for interval-negate/-reverse.</summary>
internal static class RootMotionSpelling
{
    public static Note[] RebuildFromMotions(IReadOnlyList<ChordSymbol> chords, Func<int, int> transformMotion)
    {
        var newRoots = new Note[chords.Count];
        if (chords.Count == 0) { return newRoots; }

        newRoots[0] = chords[0].Root;
        var pc = chords[0].Root.PitchClass.Value;
        for (var i = 1; i < chords.Count; i++)
        {
            var motion = ((chords[i].Root.PitchClass.Value - chords[i - 1].Root.PitchClass.Value) % 12 + 12) % 12;
            pc = (pc + transformMotion(motion)) % 12;
            newRoots[i] = Note.Spell(pc, chords[i].Root.Letter);
        }
        return newRoots;
    }
}
