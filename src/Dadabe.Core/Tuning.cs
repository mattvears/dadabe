using System.Collections.Immutable;
using Dadabe.Core.Memo;

namespace Dadabe.Core;

/// <summary>
/// An ordered list of open-string spelled <see cref="Pitch"/>es, low to
/// high. Identity carries the *spelling*, not just sounding MIDI, so a
/// re-spelled tuning hashes distinctly (D17 / §8.1).
/// </summary>
public sealed record Tuning : IContentHashable
{
    public string Name { get; }

    /// <summary>Strings ordered low (index 0) to high.</summary>
    public ImmutableArray<Pitch> Strings { get; }

    public Tuning(string name, IEnumerable<Pitch> strings)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(strings);
        Name = name;
        Strings = strings.ToImmutableArray();
        if (Strings.Length == 0)
        {
            throw new ArgumentException("A tuning must have at least one string.", nameof(strings));
        }
    }

    /// <summary>
    /// Parse a comma-separated SPN spec like <c>"D2,A2,D3,A3,B3,E4"</c>.
    /// The <paramref name="name"/> is used as the display name only — it has
    /// no role in identity beyond being echoed back to consumers.
    /// </summary>
    public static Tuning ParseSpec(string spec, string name = "(spec)")
    {
        ArgumentNullException.ThrowIfNull(spec);
        var pitches = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Pitch.Parse)
            .ToArray();
        if (pitches.Length == 0)
        {
            throw new FormatException($"Tuning spec '{spec}' yielded no strings.");
        }
        return new Tuning(name, pitches);
    }

    public ContentHash ContentHash
    {
        get
        {
            var c = new Canonical().U16BE((ushort)Strings.Length);
            foreach (var p in Strings)
            {
                c.U8((byte)p.Note.Letter)
                 .I8((sbyte)p.Note.Accidental)
                 .I16BE((short)p.Octave);
            }
            return Memo.ContentHash.FromCanonical(Namespaces.Tuning, 1, c.AsSpan());
        }
    }
}
