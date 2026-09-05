using System.Text;

namespace Dadabe.Core;

/// <summary>
/// The seven natural letters in stacked-thirds order indexing convention:
/// C=0, D=1, E=2, F=3, G=4, A=5, B=6. So <c>(Letter + 2) mod 7</c> walks a
/// third up by letter (D12 stacked-thirds spelling).
/// </summary>
public enum Letter
{
    C = 0,
    D = 1,
    E = 2,
    F = 3,
    G = 4,
    A = 5,
    B = 6,
}

/// <summary>
/// A user-facing pitch name: letter plus accidental in <c>−2..+2</c>
/// (double-flat through double-sharp). Two notes with the same
/// <see cref="PitchClass"/> can still differ as <see cref="Note"/>s — the
/// hook that lets <c>F♯maj7</c> and <c>G♭maj7</c> stay distinct (D12).
/// </summary>
public readonly record struct Note
{
    public const int MinAccidental = -2;
    public const int MaxAccidental = +2;

    public Letter Letter { get; }
    public int Accidental { get; }

    public Note(Letter letter, int accidental)
    {
        if (accidental is < MinAccidental or > MaxAccidental)
        {
            throw new ArgumentOutOfRangeException(
                nameof(accidental),
                accidental,
                $"Accidental must be in {MinAccidental}..{MaxAccidental}.");
        }
        Letter = letter;
        Accidental = accidental;
    }

    /// <summary>Pitch class of the natural (no-accidental) letter.</summary>
    public static int NaturalPitchClass(Letter letter) => letter switch
    {
        Letter.C => 0,
        Letter.D => 2,
        Letter.E => 4,
        Letter.F => 5,
        Letter.G => 7,
        Letter.A => 9,
        Letter.B => 11,
        _ => throw new ArgumentOutOfRangeException(nameof(letter), letter, null),
    };

    /// <summary>Derived pitch class, with accidental wrapped into 0..11.</summary>
    public PitchClass PitchClass => Core.PitchClass.FromInt(NaturalPitchClass(Letter) + Accidental);

    /// <summary>Letter walk: a third up by letter is <c>LetterPlus(2)</c>.</summary>
    public static Letter LetterPlus(Letter letter, int steps)
    {
        var v = ((int)letter + steps) % 7;
        if (v < 0) { v += 7; }
        return (Letter)v;
    }

    /// <summary>
    /// Transposes by letter-walk then accidental correction, so the result is
    /// spelled rather than guessed (D45). Throws nothing: if the diatonically
    /// correct spelling would fall outside ±2 accidentals, the result is
    /// respelled enharmonically and <paramref name="spellingNote"/> carries a
    /// human-readable explanation; otherwise it is <c>null</c>.
    /// </summary>
    public Note Transpose(Interval interval, out string? spellingNote)
    {
        var targetPc = Mod12(NaturalPitchClass(Letter) + Accidental + interval.Semitones);
        var steps = interval.DiatonicSteps;
        if (interval.Semitones < 0) { steps = -steps; }
        var letter = LetterPlus(Letter, steps);
        var accidental = NormalizeAccidental(targetPc - NaturalPitchClass(letter));

        if (accidental is >= MinAccidental and <= MaxAccidental)
        {
            spellingNote = null;
            return new Note(letter, accidental);
        }

        var attempted = FormatUnchecked(letter, accidental);
        var (respelledLetter, respelledAccidental) = SpellPitchClass(targetPc, letter);
        var result = new Note(respelledLetter, respelledAccidental);
        spellingNote =
            $"{attempted} respelled as {result} — this key is past what standard notation handles comfortably.";
        return result;
    }

    /// <summary>Transposes by named interval. See the <c>out spellingNote</c> overload for overflow handling.</summary>
    public Note Transpose(Interval interval) => Transpose(interval, out _);

    private static int Mod12(int v)
    {
        var m = v % 12;
        return m < 0 ? m + 12 : m;
    }

    /// <summary>Normalizes an accidental delta to its representative in (-6, 6].</summary>
    private static int NormalizeAccidental(int accidental)
    {
        while (accidental > 6) { accidental -= 12; }
        while (accidental <= -6) { accidental += 12; }
        return accidental;
    }

    /// <summary>
    /// Spells a raw pitch class (0..11) as a <see cref="Note"/>, preferring
    /// the simplest accidental (ties broken by circular distance to
    /// <paramref name="preferredLetter"/>). Used by transforms that compute a
    /// target pitch class directly rather than via a named interval (e.g.
    /// inversion about an axis, D47) — unlike the diatonic-overflow respell
    /// path, there is no "correct" letter to stay near here, and preferring
    /// the plainest spelling is what makes double application return to the
    /// original spelling (mirror-about-C on F and back, not F## and back).
    /// </summary>
    public static Note Spell(int pitchClass, Letter preferredLetter)
    {
        var targetPc = Mod12(pitchClass);
        var bestLetter = preferredLetter;
        var bestAccidental = 0;
        var bestAbsAccidental = int.MaxValue;
        var bestDistance = int.MaxValue;

        foreach (Letter letter in Enum.GetValues<Letter>())
        {
            var accidental = NormalizeAccidental(targetPc - NaturalPitchClass(letter));
            if (accidental is < MinAccidental or > MaxAccidental) { continue; }

            var absAccidental = Math.Abs(accidental);
            var diff = Math.Abs((int)letter - (int)preferredLetter);
            var distance = Math.Min(diff, 7 - diff);
            if (absAccidental < bestAbsAccidental || (absAccidental == bestAbsAccidental && distance < bestDistance))
            {
                bestLetter = letter;
                bestAccidental = accidental;
                bestAbsAccidental = absAccidental;
                bestDistance = distance;
            }
        }

        return new Note(bestLetter, bestAccidental);
    }

    /// <summary>
    /// Finds the letter closest to <paramref name="preferredLetter"/> (by
    /// circular letter distance) whose accidental for <paramref name="targetPc"/>
    /// falls within ±2. Natural pitch classes are at most 2 semitones apart, so
    /// a valid candidate always exists.
    /// </summary>
    private static (Letter Letter, int Accidental) SpellPitchClass(int targetPc, Letter preferredLetter)
    {
        var bestLetter = preferredLetter;
        var bestAccidental = 0;
        var bestDistance = int.MaxValue;
        var bestAbsAccidental = int.MaxValue;

        foreach (Letter letter in Enum.GetValues<Letter>())
        {
            var accidental = NormalizeAccidental(targetPc - NaturalPitchClass(letter));
            if (accidental is < MinAccidental or > MaxAccidental) { continue; }

            var diff = Math.Abs((int)letter - (int)preferredLetter);
            var distance = Math.Min(diff, 7 - diff);
            var absAccidental = Math.Abs(accidental);
            if (distance < bestDistance || (distance == bestDistance && absAccidental < bestAbsAccidental))
            {
                bestLetter = letter;
                bestAccidental = accidental;
                bestDistance = distance;
                bestAbsAccidental = absAccidental;
            }
        }

        return (bestLetter, bestAccidental);
    }

    private static string FormatUnchecked(Letter letter, int accidental)
    {
        var sb = new StringBuilder();
        sb.Append(LetterChar(letter));
        if (accidental > 0) { sb.Append('#', accidental); }
        else if (accidental < 0) { sb.Append('b', -accidental); }
        return sb.ToString();
    }

    private static char LetterChar(Letter letter) => letter switch
    {
        Letter.C => 'C',
        Letter.D => 'D',
        Letter.E => 'E',
        Letter.F => 'F',
        Letter.G => 'G',
        Letter.A => 'A',
        Letter.B => 'B',
        _ => throw new InvalidOperationException(),
    };

    /// <summary>
    /// Parse a note name like <c>C</c>, <c>C#</c>, <c>Bb</c>, <c>Bbb</c>,
    /// <c>E#</c>. Accepts both ASCII <c>#</c>/<c>b</c> and Unicode <c>♯</c>/<c>♭</c>
    /// accidentals. Round-trips with <see cref="ToString"/> when the input
    /// uses ASCII accidentals.
    /// </summary>
    public static Note Parse(string s)
    {
        if (TryParse(s, out var note))
        {
            return note;
        }
        throw new FormatException($"'{s}' is not a valid note name.");
    }

    public static bool TryParse(string s, out Note note)
    {
        note = default;
        if (string.IsNullOrEmpty(s) || s.Length > 3) { return false; }
        var head = char.ToUpperInvariant(s[0]);
        if (head is < 'A' or > 'G') { return false; }
        Letter letter = head switch
        {
            'C' => Letter.C,
            'D' => Letter.D,
            'E' => Letter.E,
            'F' => Letter.F,
            'G' => Letter.G,
            'A' => Letter.A,
            'B' => Letter.B,
            _ => throw new InvalidOperationException(),
        };
        var accidental = 0;
        for (var i = 1; i < s.Length; i++)
        {
            var c = s[i];
            if (c is '#' or '♯') { accidental++; }
            else if (c is 'b' or 'B' or '♭') { accidental--; }
            else if (c is '⨯') { accidental += 2; }
            else { return false; }
        }
        if (accidental is < MinAccidental or > MaxAccidental) { return false; }
        note = new Note(letter, accidental);
        return true;
    }

    public override string ToString() => FormatUnchecked(Letter, Accidental);
}
