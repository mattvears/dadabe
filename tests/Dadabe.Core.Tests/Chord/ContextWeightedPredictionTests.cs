using Dadabe.Core.Chord;
using FluentAssertions;

namespace Dadabe.Core.Tests.Chord;

public class ContextWeightedPredictionTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly ChordExpander Expander = new(Grammar);

    private static ChordSpec Spec(string symbol) => Expander.Expand(Parser.Parse(symbol));

    [Fact]
    public void Without_context_predict_returns_candidates()
    {
        var spec = Spec("Dm7");
        var results = NextChordPredictor.Predict(spec, topN: 5, entropy: 0.5);
        results.Should().HaveCount(5);
        results.All(c => c.Probability > 0).Should().BeTrue();
    }

    [Fact]
    public void With_context_scores_sum_to_approximately_one()
    {
        var spec = Spec("Dm7");
        var context = new[] { Spec("Cmaj7"), Spec("Am7") }.ToList();
        var results = NextChordPredictor.Predict(spec, 10, 0.5, context);
        results.Sum(c => c.Probability).Should().BeApproximately(1.0, precision: 0.01);
    }

    [Fact]
    public void C_major_context_boosts_diatonic_candidates_over_non_diatonic()
    {
        // For Cmaj7, the M7 prediction table includes G7 (V7, diatonic in C major)
        // and Bbmaj7 (bVIImaj7, NOT diatonic in C major).
        // With C major context and low entropy, G7 should outscore Bbmaj7.
        var spec = Spec("Cmaj7");
        var context = new[] { Spec("Fmaj7"), Spec("Dm7"), Spec("Cmaj7") }.ToList();
        var results = NextChordPredictor.Predict(spec, 10, entropy: 0.2, context);

        var g7 = results.FirstOrDefault(c => c.Symbol == "G7");
        var bbm7 = results.FirstOrDefault(c => c.Symbol == "Bbmaj7");

        g7.Should().NotBeNull("G7 is V7 in C major and should appear in predictions for Cmaj7");
        bbm7.Should().NotBeNull("Bbmaj7 is bVIImaj7 and should appear in predictions for Cmaj7");

        // With C major context, G7 (diatonic) should score higher than Bbmaj7 (non-diatonic).
        g7!.Probability.Should().BeGreaterThan(bbm7!.Probability);
    }

    [Fact]
    public void High_entropy_context_produces_flatter_distribution_than_low_entropy()
    {
        var spec = Spec("G7");
        var context = new[] { Spec("Cmaj7") }.ToList();

        var lowEntropy = NextChordPredictor.Predict(spec, 5, entropy: 0.1, context);
        var highEntropy = NextChordPredictor.Predict(spec, 5, entropy: 5.0, context);

        double lowSpread = lowEntropy.Max(c => c.Probability) - lowEntropy.Min(c => c.Probability);
        double highSpread = highEntropy.Max(c => c.Probability) - highEntropy.Min(c => c.Probability);

        lowSpread.Should().BeGreaterThan(highSpread);
    }
}
