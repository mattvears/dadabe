using System.Globalization;

namespace Dadabe.Core;

/// <summary>
/// A spelled pitch — <see cref="Note"/> at a given octave. Octave follows
/// the letter, not the sound: <c>Cb4</c> sounds like B3 but is labelled
/// octave 4 (D12).
/// </summary>
public readonly record struct Pitch
{
    public Note Note { get; }
    public int Octave { get; }

    public Pitch(Note note, int octave)
    {
        Note = note;
        Octave = octave;
    }

    /// <summary>
    /// MIDI number under 12-TET with A4 = 69. <c>Cb4</c> and <c>B3</c> share
    /// MIDI 59 even though their <see cref="Octave"/> labels differ.
    /// </summary>
    public int Midi => (12 * (Octave + 1)) + Note.NaturalPitchClass(Note.Letter) + Note.Accidental;

    public PitchClass PitchClass => Note.PitchClass;

    /// <summary>
    /// Return a <see cref="Pitch"/> with the same sounding MIDI as this one
    /// but spelled per <paramref name="newNote"/>. The octave is recomputed
    /// from MIDI so that <c>C4</c> rebound to <c>B#</c> yields <c>B#3</c>
    /// (both MIDI 60), not <c>B#4</c>.
    /// </summary>
    public Pitch WithSpelling(Note newNote)
    {
        var midi = Midi;
        var natural = Note.NaturalPitchClass(newNote.Letter);
        var newOctave = ((midi - natural - newNote.Accidental) / 12) - 1;
        return new Pitch(newNote, newOctave);
    }

    public static Pitch Parse(string s)
    {
        if (TryParse(s, out var pitch))
        {
            return pitch;
        }
        throw new FormatException($"'{s}' is not a valid scientific pitch notation string.");
    }

    public static bool TryParse(string s, out Pitch pitch)
    {
        pitch = default;
        if (string.IsNullOrEmpty(s)) { return false; }
        // Split note part and octave part: the octave starts at the first digit/'-'.
        var split = -1;
        for (var i = 1; i < s.Length; i++)
        {
            if (s[i] is (>= '0' and <= '9') or '-')
            {
                split = i;
                break;
            }
        }
        if (split <= 0) { return false; }
        var notePart = s[..split];
        var octPart = s[split..];
        if (!Note.TryParse(notePart, out var note)) { return false; }
        if (!int.TryParse(octPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var octave)) { return false; }
        pitch = new Pitch(note, octave);
        return true;
    }

    public override string ToString() => Note.ToString() + Octave.ToString(CultureInfo.InvariantCulture);

    public static Interval operator -(Pitch high, Pitch low) => Interval.Semi(high.Midi - low.Midi);

    public static Pitch operator +(Pitch pitch, Interval interval) => pitch.Transpose(interval.Semitones);

    public Pitch Transpose(int semitones)
    {
        // Keep the same letter; let the accidental absorb the shift if it fits,
        // otherwise just shift MIDI. We re-derive an octave whose letter matches
        // the new pitch class. Used only for fret arithmetic that doesn't care
        // about chord-context spelling — the chord context rebinds the spelling
        // via Reach() later.
        var newMidi = Midi + semitones;
        // Default to natural-letter spelling at the new pitch class.
        var newPc = ((newMidi % 12) + 12) % 12;
        Letter newLetter = newPc switch
        {
            0 => Core.Letter.C,
            1 => Core.Letter.C,
            2 => Core.Letter.D,
            3 => Core.Letter.D,
            4 => Core.Letter.E,
            5 => Core.Letter.F,
            6 => Core.Letter.F,
            7 => Core.Letter.G,
            8 => Core.Letter.G,
            9 => Core.Letter.A,
            10 => Core.Letter.A,
            11 => Core.Letter.B,
            _ => throw new InvalidOperationException(),
        };
        var accidental = newPc - Note.NaturalPitchClass(newLetter);
        var newOctave = (newMidi / 12) - 1;
        return new Pitch(new Note(newLetter, accidental), newOctave);
    }
}
