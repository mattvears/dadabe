using System.Collections.Immutable;
using Dadabe.Core;
using Dadabe.Core.Chord;
using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

/// <summary>
/// Validates Transition / VoiceMove content-hash invariants (D16/D17) and
/// the fret-distance metric used by VoiceLeadSolver (D29).
/// </summary>
public class TransitionTests
{
    private static readonly Environment Env = Environment.Default();
    private static readonly ChordParser Parser = new(Env.Catalogs.ChordGrammar);
    private static readonly ChordExpander Expander = new(Env.Catalogs.ChordGrammar);

    private static ImmutableArray<Voicing> GetVoicings(string symbol, int take = 2)
    {
        var spec = Expander.Expand(Parser.Parse(symbol));
        var tuning = Env.Catalogs.Tunings.Get("STANDARD");
        return VoicingSearch.Search(spec, tuning, Env.HandModel, SearchParams.Default, Env.Catalogs.VoicingCategories)
            .Voicings
            .Take(take)
            .ToImmutableArray();
    }

    [Fact]
    public void Transition_content_hash_is_deterministic()
    {
        var voicings = GetVoicings("Cmaj7");
        voicings.Length.Should().BeGreaterThanOrEqualTo(2);

        var moves = Array.Empty<VoiceMove>();
        var t1 = new Transition(voicings[0], voicings[1], moves, 0);
        var t2 = new Transition(voicings[0], voicings[1], moves, 0);

        t1.ContentHash.Should().Be(t2.ContentHash);
    }

    [Fact]
    public void Transition_hash_differs_when_direction_is_swapped()
    {
        var voicings = GetVoicings("Cmaj7");
        voicings.Length.Should().BeGreaterThanOrEqualTo(2);

        var moves = Array.Empty<VoiceMove>();
        var forward  = new Transition(voicings[0], voicings[1], moves, 0);
        var backward = new Transition(voicings[1], voicings[0], moves, 0);

        forward.ContentHash.Should().NotBe(backward.ContentHash);
    }

    [Fact]
    public void VoiceMove_carries_fret_displacement()
    {
        var move = new VoiceMove(StringIndex: 2, FromFret: 3, ToFret: 5, Distance: 2);

        move.StringIndex.Should().Be(2);
        move.FromFret.Should().Be(3);
        move.ToFret.Should().Be(5);
        move.Distance.Should().Be(2);
    }

    [Fact]
    public void VoiceMove_muted_string_uses_null_fret()
    {
        var move = new VoiceMove(StringIndex: 0, FromFret: null, ToFret: 3, Distance: 4);

        move.FromFret.Should().BeNull();
        move.Distance.Should().Be(4);
    }

    [Fact]
    public void TotalFretDistance_same_voicing_is_zero()
    {
        var voicings = GetVoicings("G");
        voicings.Length.Should().BeGreaterThanOrEqualTo(1);

        VoiceLeadSolver.TotalFretDistance(voicings[0], voicings[0]).Should().Be(0);
    }

    [Fact]
    public void TotalFretDistance_is_symmetric()
    {
        var voicings = GetVoicings("Cmaj7");
        voicings.Length.Should().BeGreaterThanOrEqualTo(2);

        int ab = VoiceLeadSolver.TotalFretDistance(voicings[0], voicings[1]);
        int ba = VoiceLeadSolver.TotalFretDistance(voicings[1], voicings[0]);
        ab.Should().Be(ba);
    }

    [Fact]
    public void TotalFretDistance_is_non_negative()
    {
        var voicings = GetVoicings("Dm7", take: 5);
        for (int i = 0; i < voicings.Length; i++)
        for (int j = 0; j < voicings.Length; j++)
            VoiceLeadSolver.TotalFretDistance(voicings[i], voicings[j]).Should().BeGreaterThanOrEqualTo(0);
    }
}
