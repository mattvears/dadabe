using System.Collections.Immutable;
using Dadabe.Core.Memo;

namespace Dadabe.Core.Chord;

/// <summary>
/// Expands a <see cref="ChordSymbol"/> into a <see cref="ChordSpec"/>:
/// resolves the matched form's tones, applies each modifier, then spells
/// every tone via the stacked-thirds letter walk (D12).
/// </summary>
public sealed class ChordExpander
{
    private readonly ChordGrammar _grammar;
    private readonly Dictionary<string, GrammarForm> _formsByDisplayName;
    private readonly Dictionary<string, GrammarModifier> _modifiersByCanonical;

    public ChordExpander(ChordGrammar grammar)
    {
        ArgumentNullException.ThrowIfNull(grammar);
        _grammar = grammar;
        _formsByDisplayName = grammar.Forms.ToDictionary(f => f.DisplayName, StringComparer.Ordinal);
        _modifiersByCanonical = grammar.Modifiers.ToDictionary(m => m.Tokens[0], StringComparer.Ordinal);
    }

    public ChordSpec Expand(ChordSymbol symbol, IMemo<ChordSymbol, ChordSpec>? cache = null)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        if (cache is not null && cache.TryGet(symbol, out var cached))
        {
            return cached;
        }

        if (!_formsByDisplayName.TryGetValue(symbol.Quality, out var form))
        {
            throw new InvalidOperationException(
                $"No form with displayName '{symbol.Quality}' in grammar — was the symbol parsed with the same grammar?");
        }

        // Working state: function -> tone semitones. We retain insertion order
        // and rebuild as a list at the end.
        var tones = form.Tones.ToList();
        var required = new HashSet<string>(form.Required, StringComparer.Ordinal);

        // Alterations first (each removes its displaces target then adds its tones),
        // then additions. Order is irrelevant per D19 §10.2 since both operations
        // are independent.
        foreach (var altToken in symbol.Alterations)
        {
            if (!_modifiersByCanonical.TryGetValue(altToken, out var mod))
            {
                throw new InvalidOperationException($"Unknown alteration '{altToken}'.");
            }
            ApplyModifier(tones, required, mod);
        }
        foreach (var extToken in symbol.Extensions)
        {
            if (!_modifiersByCanonical.TryGetValue(extToken, out var mod))
            {
                throw new InvalidOperationException($"Unknown extension '{extToken}'.");
            }
            ApplyModifier(tones, required, mod);
        }

        var spelledTones = tones
            .Select(t => SpellTone(symbol.Root, t))
            .ToImmutableArray();

        if (symbol.Bass is { } bass)
        {
            spelledTones = WithBassTone(symbol.Root, spelledTones, bass);
        }

        var spec = new ChordSpec(
            symbol.Root,
            symbol.Quality,
            spelledTones,
            required.ToImmutableArray(),
            symbol.Bass);

        cache?.Put(symbol, spec);
        return spec;
    }

    /// <summary>
    /// Ensure a slash-chord bass is reachable on the fretboard.
    /// <para>
    /// An inversion (<c>C/G</c>, <c>Cmaj7/B</c>) names a pitch class the chord
    /// already contains, so nothing is added — the existing tone keeps its own
    /// function and only the bass constraint in the search distinguishes the
    /// inversion from root position.
    /// </para>
    /// <para>
    /// A foreign bass (<c>C/D</c>, <c>Dm7/G</c>) is appended as an extra tone.
    /// Without it <see cref="ChordSpec.PitchClasses"/> would not contain the
    /// bass at all and the fretboard search could never place it. The tone
    /// keeps the spelling the user typed (D12) rather than being re-derived by
    /// the stacked-thirds walk, and is labelled
    /// <see cref="ChordSpec.BassFunction"/> so it is never mistaken for a
    /// chord tone.
    /// </para>
    /// </summary>
    private static ImmutableArray<ChordTone> WithBassTone(
        Note root, ImmutableArray<ChordTone> tones, Note bass)
    {
        var bassPc = bass.PitchClass;
        if (tones.Any(t => t.PitchClass.Value == bassPc.Value))
        {
            return tones;
        }

        var semitones = (bassPc.Value - root.PitchClass.Value + 12) % 12;
        return tones.Add(new ChordTone(ChordSpec.BassFunction, semitones, bass, bassPc));
    }

    private static void ApplyModifier(List<GrammarTone> tones, HashSet<string> required, GrammarModifier mod)
    {
        if (mod.Kind == ModifierKind.Alteration && mod.Displaces is { } displaces)
        {
            for (var i = tones.Count - 1; i >= 0; i--)
            {
                if (string.Equals(tones[i].Function, displaces, StringComparison.Ordinal))
                {
                    tones.RemoveAt(i);
                }
            }
            required.Remove(displaces);
        }
        foreach (var add in mod.AddTones)
        {
            // A tone an octave apart from `add` (e.g. "11" and "4", or "13" and
            // "6") shares its pitch class. Two such tones can never coexist in a
            // ChordSpec (Reachability keys tones by pitch class), so whichever
            // extension is already present loses to the one being applied now —
            // this is what makes e.g. "13sus4" drop the stacked 11 in favor of
            // the sus 4 without the grammar needing to spell out every such
            // collision via "displaces".
            var addPc = ((add.Semitones % 12) + 12) % 12;
            for (var i = tones.Count - 1; i >= 0; i--)
            {
                var pc = ((tones[i].Semitones % 12) + 12) % 12;
                if (pc == addPc)
                {
                    required.Remove(tones[i].Function);
                    tones.RemoveAt(i);
                }
            }
            tones.Add(add);
        }
        foreach (var r in mod.Required)
        {
            required.Add(r);
        }
    }

    /// <summary>
    /// Stacked-thirds letter walk (D12): pick the letter from the function's
    /// scale degree, then adjust accidental so the spelled note's pitch
    /// class matches <c>(root + semitones) mod 12</c>.
    /// </summary>
    internal static ChordTone SpellTone(Note root, GrammarTone tone)
    {
        var degree = ScaleDegreeOf(tone.Function);
        var letterOffset = (degree - 1) % 7;
        var letter = Note.LetterPlus(root.Letter, letterOffset);

        var rootPc = root.PitchClass.Value;
        var targetPc = (rootPc + tone.Semitones) % 12;
        if (targetPc < 0) { targetPc += 12; }

        var naturalPc = Note.NaturalPitchClass(letter);
        var diff = (targetPc - naturalPc + 12) % 12;
        if (diff > 6) { diff -= 12; }
        if (diff is < Note.MinAccidental or > Note.MaxAccidental)
        {
            throw new InvalidOperationException(
                $"Stacked-thirds spelling for function '{tone.Function}' on root '{root}' " +
                $"requires accidental {diff}, outside the supported range " +
                $"{Note.MinAccidental}..{Note.MaxAccidental}.");
        }
        var spelled = new Note(letter, diff);
        return new ChordTone(tone.Function, tone.Semitones, spelled, PitchClass.FromInt(targetPc));
    }

    /// <summary>
    /// Map a function string (e.g. <c>1</c>, <c>b3</c>, <c>#11</c>, <c>bb7</c>)
    /// to its scale degree (1..13). The leading accidental glyphs are
    /// ignored — they affect semitones, not which letter we land on.
    /// </summary>
    internal static int ScaleDegreeOf(string function)
    {
        var i = 0;
        while (i < function.Length && function[i] is 'b' or '#') { i++; }
        if (!int.TryParse(function.AsSpan(i), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var degree))
        {
            throw new FormatException($"Cannot parse scale degree from function '{function}'.");
        }
        return degree;
    }
}
