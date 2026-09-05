using Dadabe.Core.Chord;
using Dadabe.Core.Memo;
using FluentAssertions;

namespace Dadabe.Core.Tests.Chord;

public class ChordExpanderTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly ChordExpander Expander = new(Grammar);

    private static ChordSpec Expand(string symbol) => Expander.Expand(Parser.Parse(symbol));

    private static string[] Notes(ChordSpec s) => s.Tones.Select(t => t.Note.ToString()).ToArray();
    private static string[] Functions(ChordSpec s) => s.Tones.Select(t => t.Function).ToArray();

    [Fact]
    public void Cmaj7_spells_C_E_G_B()
    {
        var spec = Expand("Cmaj7");
        Notes(spec).Should().Equal("C", "E", "G", "B");
        Functions(spec).Should().Equal("1", "3", "5", "7");
    }

    [Fact]
    public void FSharpMaj7_spells_F_sharp_A_sharp_C_sharp_E_sharp()
    {
        var spec = Expand("F#maj7");
        Notes(spec).Should().Equal("F#", "A#", "C#", "E#");
    }

    [Fact]
    public void DbMaj7_spells_D_flat_F_A_flat_C()
    {
        var spec = Expand("Dbmaj7");
        Notes(spec).Should().Equal("Db", "F", "Ab", "C");
    }

    [Fact]
    public void Inversion_carries_bass_without_adding_a_tone()
    {
        var spec = Expand("C/G");
        spec.Bass.Should().Be(new Note(Letter.G, 0));
        // G is already the 5th, so the tone set is untouched and keeps its function.
        Notes(spec).Should().Equal("C", "E", "G");
        Functions(spec).Should().Equal("1", "3", "5");
    }

    [Fact]
    public void Foreign_bass_is_appended_as_a_bass_function_tone()
    {
        var spec = Expand("C/D");
        spec.Bass.Should().Be(new Note(Letter.D, 0));
        Notes(spec).Should().Equal("C", "E", "G", "D");
        Functions(spec).Should().Equal("1", "3", "5", ChordSpec.BassFunction);
    }

    [Fact]
    public void Foreign_bass_keeps_the_spelling_the_user_typed()
    {
        // F# must not be re-spelled Gb by a stacked-thirds walk (D12).
        Notes(Expand("C/F#")).Should().Equal("C", "E", "G", "F#");
        Notes(Expand("C/Gb")).Should().Equal("C", "E", "G", "Gb");
    }

    [Fact]
    public void Foreign_bass_does_not_enter_the_required_set()
    {
        // The bass is enforced by the search's lowest-pitch check, not by the
        // required-tone floor — keeping one source of truth for the constraint.
        Expand("C/D").Required.Should().Equal(Expand("C").Required);
    }

    [Fact]
    public void Slash_chord_hashes_differently_from_its_root_position_form()
    {
        Expand("C/G").ContentHash.Should().NotBe(Expand("C").ContentHash);
        Expand("C/G").ContentHash.Should().NotBe(Expand("C/E").ContentHash);
    }

    [Fact]
    public void Root_position_hash_is_unaffected_by_the_bass_field()
    {
        // Bass bytes are appended only when present, so specs that predate slash
        // chords keep their canonical form and published voicing ids do not churn.
        var withNullBass = new ChordSpec(
            Expand("C").Root, Expand("C").Quality, Expand("C").Tones, Expand("C").Required);
        Expand("C").ContentHash.Should().Be(withNullBass.ContentHash);
    }

    [Fact]
    public void BDim_spells_B_D_F()
    {
        var spec = Expand("Bdim");
        Notes(spec).Should().Equal("B", "D", "F");
    }

    [Fact]
    public void BSharpDim7_uses_double_flat_for_bb7()
    {
        var spec = Expand("B#dim7");
        // B# (pc 0) dim7: B# D# F# A
        Notes(spec).Should().Equal("B#", "D#", "F#", "A");
    }

    [Fact]
    public void Cmaj_spells_C_E_G()
    {
        var spec = Expand("Cmaj");
        Notes(spec).Should().Equal("C", "E", "G");
    }

    [Fact]
    public void Cm_spells_C_Eb_G()
    {
        var spec = Expand("Cm");
        Notes(spec).Should().Equal("C", "Eb", "G");
    }

    [Fact]
    public void G7b9_includes_b9_in_alterations()
    {
        var spec = Expand("G7b9");
        Functions(spec).Should().Contain("b9");
        spec.Required.Should().Contain("b9");
    }

    [Fact]
    public void Cmaj7_required_is_3_and_7()
    {
        var expected = new[] { "3", "7" };
        Expand("Cmaj7").Required.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Bare_root_C_spells_C_E_G()
    {
        var spec = Expand("C");
        Notes(spec).Should().Equal("C", "E", "G");
    }

    [Fact]
    public void Enharmonic_chords_share_pitch_classes_but_differ_in_spelling()
    {
        var fSharp = Expand("F#maj7");
        var gFlat = Expand("Gbmaj7");

        fSharp.PitchClasses.Select(pc => pc.Value)
            .Should().BeEquivalentTo(gFlat.PitchClasses.Select(pc => pc.Value));
        Notes(fSharp).Should().NotEqual(Notes(gFlat));
        fSharp.ContentHash.Should().NotBe(gFlat.ContentHash);
    }

    [Fact]
    public void Alteration_displaces_natural_5_for_b5()
    {
        var c7b5 = Expand("C7b5");
        Functions(c7b5).Should().Contain("b5");
        Functions(c7b5).Should().NotContain("5");
    }

    [Fact]
    public void Memo_cache_returns_identical_spec()
    {
        var cache = new InMemoryMemo<ChordSymbol, ChordSpec>();
        var sym = Parser.Parse("Cmaj7");

        var first = Expander.Expand(sym, cache);
        var second = Expander.Expand(sym, cache);

        second.Should().BeSameAs(first);
        first.ContentHash.Should().Be(second.ContentHash);
    }
}
