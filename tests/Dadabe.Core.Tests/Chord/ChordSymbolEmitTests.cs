using Dadabe.Core.Chord;
using FluentAssertions;

namespace Dadabe.Core.Tests.Chord;

public class ChordSymbolEmitTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);

    private static readonly Letter[] Letters =
        [Letter.C, Letter.D, Letter.E, Letter.F, Letter.G, Letter.A, Letter.B];

    private static readonly int[] Accidentals = [-1, 0, 1];

    public static IEnumerable<object[]> GrammarCrossProduct()
    {
        foreach (var letter in Letters)
        {
            foreach (var accidental in Accidentals)
            {
                var root = new Note(letter, accidental).ToString();
                foreach (var form in Grammar.Forms)
                {
                    // Use the canonical (DisplayName) token, not Tokens[0] — the
                    // loader re-sorts each form's Tokens longest-first (D19
                    // §10.2), so Tokens[0] is often an alias (e.g. "min7" for
                    // "m7"). Some alias + modifier combinations collide
                    // textually with an unrelated dedicated form (e.g. "min7"
                    // + "b5" both spell "...min7b5", which re-emits as
                    // "...m7b5" and reparses through the dedicated m7b5 form
                    // instead) — a pre-existing grammar quirk, not a symbol-
                    // emission bug. Generating from the canonical token avoids
                    // manufacturing that ambiguity.
                    yield return [root + form.DisplayName];

                    // Bare form plus each single modifier, since the parser
                    // does not enforce semantic compatibility (any modifier
                    // token may follow any form token).
                    foreach (var modifier in Grammar.Modifiers)
                    {
                        yield return [root + form.DisplayName + modifier.Tokens[0]];
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GrammarCrossProduct))]
    public void Round_trips_over_the_grammar_cross_product(string symbol)
    {
        var parsed = Parser.Parse(symbol);
        var emitted = parsed.ToSymbol();
        var reparsed = Parser.Parse(emitted);

        // ImmutableArray<T>'s default equality is reference-based, so compare
        // parsed values field-by-field rather than via record equality.
        reparsed.Root.Should().Be(parsed.Root);
        reparsed.Quality.Should().Be(parsed.Quality);
        reparsed.Extensions.Should().Equal(parsed.Extensions);
        reparsed.Alterations.Should().Equal(parsed.Alterations);
        reparsed.Bass.Should().Be(parsed.Bass);
    }

    [Theory]
    [InlineData("C-", "Cm")]
    [InlineData("CM7", "Cmaj7")]
    [InlineData("Cmin7", "Cm7")]
    public void Emission_normalises_aliases_to_the_canonical_token(string input, string expected)
    {
        Parser.Parse(input).ToSymbol().Should().Be(expected);
    }

    [Theory]
    [InlineData("C7#9b13", "C7#9b13")]
    [InlineData("C7b13#9", "C7#9b13")]
    public void Emission_order_is_canonical_regardless_of_input_order(string input, string expected)
    {
        Parser.Parse(input).ToSymbol().Should().Be(expected);
    }

    [Fact]
    public void Slash_chord_bass_is_emitted_after_the_chord()
    {
        Parser.Parse("Cmaj7/G").ToSymbol().Should().Be("Cmaj7/G");
    }
}
