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

        var spec = new ChordSpec(
            symbol.Root,
            symbol.Quality,
            spelledTones,
            required.ToImmutableArray());

        cache?.Put(symbol, spec);
        return spec;
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
