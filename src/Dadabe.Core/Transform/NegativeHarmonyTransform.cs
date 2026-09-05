using Dadabe.Core.Chord;

namespace Dadabe.Core.Transform;

/// <summary>
/// <c>negative-harmony</c> (D47): the pragmatic version of the key-blind
/// <c>invert</c> that users actually search for — mirrors roots about an
/// axis and flips major↔minor. Axis defaults to the inferred key's tonic
/// pitch class: the design source describes the axis as "midway between
/// tonic and dominant", but its own worked example
/// (<c>[C,F,G] → [Cm,Gm,Fm]</c> in C major) only reproduces with the axis at
/// the tonic itself (a true tonic/dominant midpoint is a non-integer axis
/// and sends C→G, not C→C) — implemented and documented as tonic-axis
/// accordingly. An explicit <c>axis</c> param makes the transform entirely
/// self-contained, so it does not require a key at all in that case.
/// Invertible: mirroring twice is the identity, same property
/// <see cref="InvertTransform"/> relies on.
/// </summary>
internal sealed class NegativeHarmonyTransform(ChordGrammar grammar) : KeyAwareTransform(grammar)
{
    public override string Id => "negative-harmony";
    public override string DisplayName => "Negative Harmony";
    public override bool IsInvertible => true;

    protected override bool RequiresKey(IReadOnlyDictionary<string, object> parameters) =>
        TransformParams.GetString(parameters, "axis") is null;

    protected override TransformResult ApplyWithKey(
        IReadOnlyList<ChordSymbol> chords,
        IReadOnlyDictionary<string, object> parameters,
        (int RootPc, bool IsMinor)? key,
        bool strict)
    {
        var axisText = TransformParams.GetString(parameters, "axis");
        var axisPc = axisText is not null ? Note.Parse(axisText).PitchClass.Value : key!.Value.RootPc;
        var positions = TransformParams.GetPositions(parameters, chords.Count);

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

            if (!ChordApproximation.CoreByQuality.TryGetValue(chord.Quality, out var core))
            {
                notes.Add(new TransformNote(i, "skipped", $"'{chord.ToSymbol()}' has no known triad core for negative-harmony."));
                results.Add(chord);
                continue;
            }

            var flipTarget = core.Triad switch { "" or "maj" => "m", "m" => "", _ => null };
            if (flipTarget is null)
            {
                notes.Add(new TransformNote(i, "skipped", $"'{chord.ToSymbol()}' has no defined major/minor flip under negative-harmony."));
                results.Add(chord);
                continue;
            }

            ChordSymbol result;
            if (core.Triad == chord.Quality)
            {
                result = chord with { Quality = flipTarget };
            }
            else if (strict)
            {
                notes.Add(new TransformNote(i, "skipped", $"'{chord.ToSymbol()}' is not a plain triad — negative-harmony in strict mode only handles plain triads."));
                results.Add(chord);
                continue;
            }
            else
            {
                notes.Add(new TransformNote(i, "approximated", $"'{chord.ToSymbol()}' triad-reduced before applying negative-harmony."));
                result = chord with { Quality = flipTarget };
            }

            if (chord.Bass is not null)
            {
                notes.Add(new TransformNote(i, "bass-dropped", $"Bass on '{chord.ToSymbol()}' was chosen for the original chord and is dropped under negative-harmony."));
            }

            var newRoot = InvertTransform.MirrorRoot(chord.Root, axisPc);
            results.Add(result with { Root = newRoot, Bass = null });
        }

        return new TransformResult(results.Select(c => c.ToSymbol()).ToArray(), notes, Invertible: true);
    }
}
