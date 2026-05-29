using System.Collections.Immutable;

namespace Dadabe.Core.Chord;

public enum ModifierKind
{
    Addition,
    Alteration,
}

/// <summary>One chord-tone entry inside a form or modifier.</summary>
public sealed record GrammarTone(string Function, int Semitones);

/// <summary>An atomic chord pattern (e.g. <c>maj7</c>, <c>m7b5</c>).</summary>
public sealed record GrammarForm(
    ImmutableArray<string> Tokens,
    string DisplayName,
    ImmutableArray<GrammarTone> Tones,
    ImmutableArray<string> Required);

/// <summary>A trailing extension/alteration applied to a matched form.</summary>
public sealed record GrammarModifier(
    ImmutableArray<string> Tokens,
    ModifierKind Kind,
    string? Displaces,
    ImmutableArray<GrammarTone> AddTones,
    ImmutableArray<string> Required);

public sealed record GrammarParseRules(
    string RootRegex,
    bool LongestTokenFirst,
    string ModifierOrder,
    bool ModifiersAllowedAfterEmptyForm,
    bool RejectUnparsedTail,
    bool RejectSlash);

/// <summary>
/// Resolved chord grammar (D19). Forms and modifiers carry their tokens
/// sorted longest-first per the parse-rules invariant; the loader re-sorts
/// after any overlay merge so the longest-match property survives user
/// extensions.
/// </summary>
public sealed record ChordGrammar(
    ImmutableArray<GrammarForm> Forms,
    ImmutableArray<GrammarModifier> Modifiers,
    GrammarParseRules ParseRules);
