using System.Collections.Immutable;
using Dadabe.Core;
using Dadabe.Core.Chord;
using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

/// <summary>
/// Validates Transition / VoiceMove content-hash invariants (D16/D17).
/// Transition is type-only in v0.1; these tests ensure the hash is stable
/// and that From/To order matters (so the hash distinguishes direction).
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

        var t1 = new Transition(voicings[0], voicings[1], ImmutableArray<VoiceMove>.Empty, 0);
        var t2 = new Transition(voicings[0], voicings[1], ImmutableArray<VoiceMove>.Empty, 0);

        t1.ContentHash.Should().Be(t2.ContentHash);
    }

    [Fact]
    public void Transition_hash_differs_when_direction_is_swapped()
    {
        var voicings = GetVoicings("Cmaj7");
        voicings.Length.Should().BeGreaterThanOrEqualTo(2);

        var forward = new Transition(voicings[0], voicings[1], ImmutableArray<VoiceMove>.Empty, 0);
        var backward = new Transition(voicings[1], voicings[0], ImmutableArray<VoiceMove>.Empty, 0);

        forward.ContentHash.Should().NotBe(backward.ContentHash);
    }

    [Fact]
    public void VoiceMove_carries_semitone_displacement()
    {
        var c4 = new Pitch(new Note(Letter.C, 0), 4);
        var e4 = new Pitch(new Note(Letter.E, 0), 4);
        var move = new VoiceMove(c4, e4, 4);

        move.Semitones.Should().Be(4);
        move.From.Should().Be(c4);
        move.To.Should().Be(e4);
    }
}
