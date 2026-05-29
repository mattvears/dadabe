using System.Collections.Immutable;

namespace Dadabe.Fretboard;

/// <summary>
/// Data-driven voicing classifier (D20). Walks the catalog's priority
/// list and returns the first category whose <c>matches</c> has any entry
/// where every rule holds for the voicing's sounded positions (in
/// ascending MIDI order).
/// </summary>
public static class Classifier
{
    public static string Classify(IReadOnlyList<FretPosition> soundedPositions, VoicingCategoryCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(soundedPositions);
        ArgumentNullException.ThrowIfNull(catalog);

        var sorted = soundedPositions
            .Where(p => !p.Muted && p.SoundingPitch is not null)
            .OrderBy(p => p.SoundingPitch!.Value.Midi)
            .ToImmutableArray();

        foreach (var category in catalog.Categories)
        {
            foreach (var match in category.Matches)
            {
                if (MatchesRule(match, sorted))
                {
                    return category.Name;
                }
            }
        }
        return string.Empty;
    }

    private static bool MatchesRule(CategoryMatch m, ImmutableArray<FretPosition> sorted)
    {
        if (m.MatchAny) { return true; }

        var n = sorted.Length;
        var functions = sorted.Select(p => p.Function ?? string.Empty).ToImmutableArray();

        if (m.NoteCount is { } nc && nc != n) { return false; }
        if (m.NoteCountRange is { } range && (n < range.Min || n > range.Max)) { return false; }

        if (m.AllFunctionsIn is { } allowed && functions.Any(f => !allowed.Contains(f)))
        {
            return false;
        }

        foreach (var req in m.RequireFunctions)
        {
            if (!functions.Any(req.Contains)) { return false; }
        }

        foreach (var forbid in m.ForbidFunctions)
        {
            if (functions.Any(forbid.Contains)) { return false; }
        }

        if (m.FunctionSequenceLowToHigh.Length > 0)
        {
            if (m.FunctionSequenceLowToHigh.Length != n) { return false; }
            for (var i = 0; i < n; i++)
            {
                if (!m.FunctionSequenceLowToHigh[i].Contains(functions[i])) { return false; }
            }
        }

        if (m.AdjacentIntervalMinSemitones is { } min)
        {
            for (var i = 1; i < n; i++)
            {
                var gap = sorted[i].SoundingPitch!.Value.Midi - sorted[i - 1].SoundingPitch!.Value.Midi;
                if (gap < min) { return false; }
            }
        }

        return true;
    }
}
