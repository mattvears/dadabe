namespace Dadabe.Core;

/// <summary>
/// One of the twelve enharmonic equivalence classes, <c>0..11</c> with C = 0.
/// Math only: <see cref="PitchClass"/> never appears in user-facing JSON
/// (per D12). Anything users see or type uses <see cref="Note"/> instead.
/// </summary>
public readonly record struct PitchClass
{
    public int Value { get; }

    public PitchClass(int value)
    {
        if (value is < 0 or > 11)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "PitchClass must be in 0..11.");
        }
        Value = value;
    }

    /// <summary>Wrap an arbitrary integer into the 0..11 range.</summary>
    public static PitchClass FromInt(int value)
    {
        var m = value % 12;
        if (m < 0) { m += 12; }
        return new PitchClass(m);
    }

    public static PitchClass operator +(PitchClass a, int semitones) => FromInt(a.Value + semitones);

    public static PitchClass operator -(PitchClass a, int semitones) => FromInt(a.Value - semitones);

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
