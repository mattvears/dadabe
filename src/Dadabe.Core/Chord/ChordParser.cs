using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Dadabe.Core.Chord;

/// <summary>
/// Data-driven chord symbol parser (D19). Consumes a loaded
/// <see cref="ChordGrammar"/>. Match order is: root (regex) → form
/// (longest-token-first) → zero or more modifiers (longest-token-first, any
/// order). Fails on unparsed tail when the grammar's parse rules say so.
/// </summary>
public sealed class ChordParser
{
    private readonly ChordGrammar _grammar;
    private readonly Regex _rootRegex;
    private readonly (string Token, GrammarForm Form)[] _formTokensLongestFirst;
    private readonly (string Token, GrammarModifier Modifier)[] _modifierTokensLongestFirst;

    public ChordParser(ChordGrammar grammar)
    {
        ArgumentNullException.ThrowIfNull(grammar);
        _grammar = grammar;
        _rootRegex = new Regex(grammar.ParseRules.RootRegex, RegexOptions.CultureInvariant);

        _formTokensLongestFirst = grammar.Forms
            .SelectMany(f => f.Tokens.Select(tok => (Token: tok, Form: f)))
            .OrderByDescending(pair => pair.Token.Length)
            .ThenBy(pair => pair.Token, StringComparer.Ordinal)
            .ToArray();

        _modifierTokensLongestFirst = grammar.Modifiers
            .SelectMany(m => m.Tokens.Select(tok => (Token: tok, Modifier: m)))
            .OrderByDescending(pair => pair.Token.Length)
            .ThenBy(pair => pair.Token, StringComparer.Ordinal)
            .ToArray();
    }

    public ChordSymbol Parse(string symbol)
    {
        if (TryParse(symbol, out var chord, out var error))
        {
            return chord;
        }
        throw new FormatException(error);
    }

    public bool TryParse(string symbol, out ChordSymbol chord, out string error)
    {
        chord = null!;
        error = string.Empty;

        if (string.IsNullOrEmpty(symbol))
        {
            error = "Chord symbol is empty.";
            return false;
        }

        // TODO (future): support slash chords — parse "C/E" as root=C, bass=E.
        // Until then the grammar's rejectSlash flag keeps them rejected (D2).
        if (_grammar.ParseRules.RejectSlash && symbol.Contains('/', StringComparison.Ordinal))
        {
            error = "Slash chords are not supported in v0.1 (D2).";
            return false;
        }

        // TODO (future): support polychords — parse "C|G" or "C over G" notations.
        // Extension point: detect the polychord delimiter here and route to a
        // dedicated PolychordParser before falling through to the standard path.

        var rootMatch = _rootRegex.Match(symbol);
        if (!rootMatch.Success || rootMatch.Index != 0)
        {
            error = $"Cannot parse root from '{symbol}'.";
            return false;
        }

        var letterChar = rootMatch.Groups[1].Value[0];
        var accidentalText = rootMatch.Groups[2].Value;
        var letter = letterChar switch
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
        var accidental = accidentalText switch
        {
            "" => 0,
            "#" => 1,
            "b" => -1,
            "##" => 2,
            "bb" => -2,
            _ => throw new InvalidOperationException($"Unexpected accidental '{accidentalText}'."),
        };
        var root = new Note(letter, accidental);

        var cursor = symbol.AsSpan(rootMatch.Length);

        GrammarForm? matchedForm = null;
        foreach (var (token, form) in _formTokensLongestFirst)
        {
            if (token.Length == 0)
            {
                matchedForm = form;
                break;
            }
            if (cursor.StartsWith(token, StringComparison.Ordinal))
            {
                matchedForm = form;
                cursor = cursor[token.Length..];
                break;
            }
        }

        if (matchedForm is null)
        {
            error = $"No chord form matches '{symbol[rootMatch.Length..]}'.";
            return false;
        }

        var extensions = ImmutableArray.CreateBuilder<string>();
        var alterations = ImmutableArray.CreateBuilder<string>();

        while (cursor.Length > 0)
        {
            var matched = false;
            foreach (var (token, modifier) in _modifierTokensLongestFirst)
            {
                if (cursor.StartsWith(token, StringComparison.Ordinal))
                {
                    var canonical = modifier.Tokens[0];
                    if (modifier.Kind == ModifierKind.Addition)
                    {
                        extensions.Add(canonical);
                    }
                    else
                    {
                        alterations.Add(canonical);
                    }
                    cursor = cursor[token.Length..];
                    matched = true;
                    break;
                }
            }
            if (!matched) { break; }
        }

        if (_grammar.ParseRules.RejectUnparsedTail && cursor.Length > 0)
        {
            error = $"Unparsed tail '{cursor.ToString()}' after parsing root + form + modifiers in '{symbol}'.";
            return false;
        }

        chord = new ChordSymbol(
            root,
            matchedForm.DisplayName,
            extensions.ToImmutable(),
            alterations.ToImmutable(),
            Bass: null);
        return true;
    }
}
