using Dadabe.Core.Chord;
using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

public class PlayabilityScoreTests
{
    private static readonly Environment Env = Environment.Default();
    private static readonly ChordParser Parser = new(Env.Catalogs.ChordGrammar);
    private static readonly ChordExpander Expander = new(Env.Catalogs.ChordGrammar);

    private static PlayabilityScore Score(IReadOnlyList<string> chords, string tuningName = "STANDARD") =>
        PlayabilityRanking.Score(
            chords,
            Env.Catalogs.Tunings.Get(tuningName),
            Env.HandModel,
            SearchParams.Default,
            Env.Catalogs.VoicingCategories,
            Parser,
            Expander);

    [Fact]
    public void Playable_progression_reports_worst_comfort_distance_and_fret_span()
    {
        var score = Score(["C", "F", "G"]);

        score.Unplayable.Should().BeEmpty();
        score.WorstComfortPct.Should().BeInRange(0, 100);
        score.TotalDistance.Should().BeGreaterThanOrEqualTo(0);
        score.MinFret.Should().BeLessThanOrEqualTo(score.MaxFret);
    }

    [Fact]
    public void Chord_with_no_voicing_above_the_floor_lands_in_unplayable()
    {
        // An absurdly restrictive hand (no open strings, one fret of span) leaves
        // some chords with nothing above the search floor.
        var restrictive = SearchParams.Default with { AllowOpen = false, MaxSpan = 0, MaxFret = 1 };
        var score = PlayabilityRanking.Score(
            ["Cmaj13"], Env.Catalogs.Tunings.Get("STANDARD"), Env.HandModel, restrictive,
            Env.Catalogs.VoicingCategories, Parser, Expander);

        score.Unplayable.Should().Contain("Cmaj13");
    }

    [Fact]
    public void Unplayable_results_sort_last_under_playability_ranking()
    {
        var playable = Score(["C", "F", "G"]);
        var restrictive = SearchParams.Default with { AllowOpen = false, MaxSpan = 0, MaxFret = 1 };
        var unplayableScore = PlayabilityRanking.Score(
            ["Cmaj13"], Env.Catalogs.Tunings.Get("STANDARD"), Env.HandModel, restrictive,
            Env.Catalogs.VoicingCategories, Parser, Expander);

        var items = new[] { ("unplayable", unplayableScore), ("playable", playable) };
        var ranked = items.OrderByPlayability(i => i.Item2).ToList();

        ranked[0].Item1.Should().Be("playable");
        ranked[^1].Item1.Should().Be("unplayable");
    }
}
