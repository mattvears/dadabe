using System.Collections.Immutable;
using Dadabe.Core;
using Dadabe.Core.Chord;

namespace Dadabe.Fretboard;

/// <param name="WorstComfortPct">The hardest single chord's comfort, as a 0-100 percentage.</param>
/// <param name="TotalDistance">Hand travel across the progression.</param>
/// <param name="MinFret">Lowest fretted position used anywhere in the progression.</param>
/// <param name="MaxFret">Highest fretted position used anywhere in the progression.</param>
/// <param name="Unplayable">Chords with no voicing above the comfort floor (or that failed to parse).</param>
public sealed record PlayabilityScore(
    int WorstComfortPct,
    int TotalDistance,
    int MinFret,
    int MaxFret,
    IReadOnlyList<string> Unplayable);

/// <summary>
/// Runs the exact pipeline <c>VoiceLeadRoutes</c> already uses — per-chord
/// <see cref="VoicingSearch"/> then <see cref="VoiceLeadSolver"/> across the
/// sequence — and reports playability (D51). A result with an unplayable
/// chord always sorts last (see <see cref="PlayabilityRanking"/>) and is
/// labelled, never hidden.
/// </summary>
public static class PlayabilityRanking
{
    public static PlayabilityScore Score(
        IReadOnlyList<string> chords,
        Tuning tuning,
        HandModel hand,
        SearchParams searchParams,
        VoicingCategoryCatalog categories,
        ChordParser parser,
        ChordExpander expander,
        double minComfort = 0.0)
    {
        var unplayable = new List<string>();
        var voicingsPerChord = new List<ImmutableArray<Voicing>>(chords.Count);

        foreach (var symbol in chords)
        {
            if (!parser.TryParse(symbol, out var chordSymbol, out _))
            {
                unplayable.Add(symbol);
                continue;
            }

            var spec = expander.Expand(chordSymbol);
            var set = VoicingSearch.Search(spec, tuning, hand, searchParams, categories);
            var voicings = set.Voicings;
            if (minComfort > 0.0)
            {
                voicings = voicings.Where(v => v.Comfort >= minComfort).ToImmutableArray();
            }

            if (voicings.Length == 0)
            {
                unplayable.Add(symbol);
                continue;
            }

            voicingsPerChord.Add(voicings);
        }

        if (unplayable.Count > 0 || voicingsPerChord.Count == 0)
        {
            return new PlayabilityScore(0, int.MaxValue, 0, 0, unplayable);
        }

        var solutions = VoiceLeadSolver.Solve(voicingsPerChord, maxSolutions: 1);
        var best = solutions[0];

        var worstComfort = best.Steps.Min(s => s.Voicing.Comfort);
        var frets = best.Steps
            .SelectMany(s => s.Voicing.Positions)
            .Where(p => p.Fret is > 0)
            .Select(p => p.Fret!.Value)
            .ToList();
        var minFret = frets.Count > 0 ? frets.Min() : 0;
        var maxFret = frets.Count > 0 ? frets.Max() : 0;

        return new PlayabilityScore(
            (int)Math.Round(worstComfort * 100),
            best.TotalDistance,
            minFret,
            maxFret,
            []);
    }

    /// <summary>
    /// Orders by <see cref="PlayabilityScore.Unplayable"/> count ascending, then
    /// worst comfort descending, then total distance ascending (D51).
    /// </summary>
    public static IOrderedEnumerable<T> OrderByPlayability<T>(
        this IEnumerable<T> items, Func<T, PlayabilityScore> scoreOf) =>
        items
            .OrderBy(i => scoreOf(i).Unplayable.Count)
            .ThenByDescending(i => scoreOf(i).WorstComfortPct)
            .ThenBy(i => scoreOf(i).TotalDistance);
}
