using System.Collections.Immutable;
using Dadabe.Core;
using Dadabe.Core.Chord;

namespace Dadabe.Fretboard;

/// <summary>
/// Per-string list of fretted positions whose pitch class is a chord tone
/// of <see cref="ChordSpec"/>. Spelling is rebound to the chord-context
/// <see cref="ChordTone.Note"/> (D12), so the same fret can render as
/// <c>D#3</c> or <c>Eb3</c> depending on the chord.
/// </summary>
public sealed class Reachability
{
    private readonly ImmutableArray<ImmutableArray<FretPosition>> _byString;

    public Tuning Tuning { get; }
    public int MaxFret { get; }

    private Reachability(Tuning tuning, int maxFret, ImmutableArray<ImmutableArray<FretPosition>> byString)
    {
        Tuning = tuning;
        MaxFret = maxFret;
        _byString = byString;
    }

    public ImmutableArray<FretPosition> ForString(int s) => _byString[s];

    /// <summary>Compute reachable positions per string for a given chord spec.</summary>
    public static Reachability Compute(ChordSpec spec, Tuning tuning, int maxFret)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(tuning);

        var byPc = spec.Tones.ToDictionary(t => t.PitchClass.Value);

        var perString = new ImmutableArray<FretPosition>.Builder[tuning.Strings.Length];
        for (var i = 0; i < perString.Length; i++)
        {
            perString[i] = ImmutableArray.CreateBuilder<FretPosition>();
        }

        foreach (var bp in FretLayout.Positions(tuning, maxFret))
        {
            if (byPc.TryGetValue(bp.Pitch.PitchClass.Value, out var tone))
            {
                var soundingPitch = bp.Pitch.WithSpelling(tone.Note);
                perString[bp.String].Add(new FretPosition(
                    bp.String, bp.Fret, soundingPitch, tone.Note, tone.Function));
            }
        }

        return new Reachability(
            tuning,
            maxFret,
            perString.Select(b => b.ToImmutable()).ToImmutableArray());
    }
}
