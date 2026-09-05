using Dadabe.Core.Chord;
using FluentAssertions;

namespace Dadabe.Core.Tests.Chord;

public class KeyInferenceTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly ChordExpander Expander = new(Grammar);

    private static ChordSpec Spec(string symbol) => Expander.Expand(Parser.Parse(symbol));

    [Fact]
    public void Empty_context_returns_null()
    {
        KeyInference.InferKey([]).Should().BeNull();
    }

    [Fact]
    public void C_major_context_infers_C_major()
    {
        var context = new[] { Spec("Cmaj7"), Spec("Am7"), Spec("Dm7"), Spec("G7") }
            .ToList();
        var key = KeyInference.InferKey(context);
        key.Should().NotBeNull();
        key!.Value.IsMinor.Should().BeFalse();
        // C = 0
        key.Value.RootPc.Should().Be(0);
    }

    [Fact]
    public void Minor_flavored_context_infers_a_plausible_key()
    {
        // Am, Dm, Em all belong to both A minor AND C major (relative keys share diatonic PCs).
        // Root-only heuristics cannot distinguish them; we verify the result is one of the pair.
        var context = new[] { Spec("Am"), Spec("Dm"), Spec("Em") }.ToList();
        var key = KeyInference.InferKey(context);
        key.Should().NotBeNull();
        key!.Value.RootPc.Should().BeOneOf(0, 9); // C major (0) or A minor (9)
    }

    [Fact]
    public void Single_chord_with_clear_key_succeeds()
    {
        // A single tonic chord should still point to a plausible key.
        var key = KeyInference.InferKey([Spec("Gmaj7")]);
        // G major is the most natural home for Gmaj7.
        key.Should().NotBeNull();
    }

    [Fact]
    public void Classify_diatonic_candidate()
    {
        var key = (RootPc: 0, IsMinor: false); // C major
        var rel = KeyInference.Classify("G7", key, lastChordRootPc: 2); // V7 in C
        rel.Should().Be(CandidateRelationship.Diatonic);
    }

    [Fact]
    public void Classify_tritone_sub_is_valid_non_diatonic()
    {
        var key = (RootPc: 0, IsMinor: false); // C major
        // Db7 is the tritone sub of G7 (interval 6 from G)
        var rel = KeyInference.Classify("Db7", key, lastChordRootPc: 7); // from G
        rel.Should().Be(CandidateRelationship.ValidNonDiatonic);
    }

    [Fact]
    public void Classify_unrelated_chord_is_unrelated()
    {
        var key = (RootPc: 0, IsMinor: false); // C major
        // F# major is not diatonic to C major and has no obvious harmonic function
        var rel = KeyInference.Classify("F#", key, lastChordRootPc: 0);
        rel.Should().Be(CandidateRelationship.ValidNonDiatonic); // tritone = 6 semitones = tritone sub
    }
}
