namespace Dadabe.Core;

/// <summary>
/// A semitone-valued interval with an optional quality label (e.g.
/// <c>P5</c>, <c>m3</c>, <c>b9</c>, <c>#11</c>). Quality is spelling-aware:
/// <c>b5</c> and <c>#11</c> are different labels even when their integer
/// semitone counts collide modulo 12.
/// </summary>
public readonly record struct Interval(int Semitones, string Quality)
{
    public static Interval Semi(int semitones) => new(semitones, DefaultQuality(semitones));

    /// <summary>
    /// Heuristic interval label from a raw semitone count. Used by
    /// <c>Pitch - Pitch</c>; chord-context spellings (b5, #11) should come
    /// from the grammar (D19), not from this fallback.
    /// </summary>
    public static string DefaultQuality(int semitones) => semitones switch
    {
        0 => "P1",
        1 => "m2",
        2 => "M2",
        3 => "m3",
        4 => "M3",
        5 => "P4",
        6 => "A4",
        7 => "P5",
        8 => "m6",
        9 => "M6",
        10 => "m7",
        11 => "M7",
        12 => "P8",
        13 => "m9",
        14 => "M9",
        15 => "A9",
        17 => "P11",
        18 => "A11",
        20 => "m13",
        21 => "M13",
        _ => semitones.ToString(System.Globalization.CultureInfo.InvariantCulture) + "st",
    };

    /// <summary>
    /// Letter-steps this interval moves: the digits of <see cref="Quality"/>,
    /// minus 1. <c>P5</c> → 4, <c>m3</c> → 2, <c>A4</c> → 3. Drives
    /// spelling-correct transposition (D45).
    /// </summary>
    public int DiatonicSteps
    {
        get
        {
            var digits = new string(Quality.Where(char.IsAsciiDigit).ToArray());
            if (digits.Length == 0)
            {
                throw new FormatException(
                    $"Interval quality '{Quality}' has no diatonic number to derive letter-steps from.");
            }
            return int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) - 1;
        }
    }

    public override string ToString() => Quality;
}
