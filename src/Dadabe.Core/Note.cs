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

    public override string ToString()
    {
        var sb = new StringBuilder(3);
        sb.Append(Letter switch
        {
            Letter.C => 'C',
            Letter.D => 'D',
            Letter.E => 'E',
            Letter.F => 'F',
            Letter.G => 'G',
            Letter.A => 'A',
            Letter.B => 'B',
            _ => throw new InvalidOperationException(),
        });
        if (Accidental > 0) { sb.Append('#', Accidental); }
        else if (Accidental < 0) { sb.Append('b', -Accidental); }
        return sb.ToString();
    }
}
