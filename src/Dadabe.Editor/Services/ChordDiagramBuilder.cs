using Dadabe.Core;
using Dadabe.Editor.Slices;
using Dadabe.Fretboard;

namespace Dadabe.Editor.Services;

/// <summary>
/// Builds the <see cref="ChordDiagram"/> view model from either a live
/// <see cref="Voicing"/> (voicings/voice-lead results) or a stored
/// <see cref="PinnedVoicing"/> (song slots) — the same shape either way, so
/// one Razor partial renders both.
/// </summary>
public static class ChordDiagramBuilder
{
    public static ChordDiagram FromVoicing(Voicing voicing)
    {
        var positions = voicing.Positions.OrderBy(p => p.String).ToList();
        var startFret = StartFret(positions.Where(p => p.Fret is > 0).Select(p => p.Fret!.Value));

        var strings = positions.Select(p =>
        {
            var name = voicing.Tuning.Strings[p.String].ToString();
            if (p.Muted) { return new ChordDiagramString(name, true, false, null); }
            if (p.Open) { return new ChordDiagramString(name, false, true, null); }
            return new ChordDiagramString(name, false, false, p.Fret!.Value);
        }).ToList();

        return new ChordDiagram(strings, startFret, NumFrets: 4);
    }

    public static ChordDiagram FromPinned(IReadOnlyList<PinnedPosition> positions, Tuning tuning)
    {
        var ordered = positions.OrderBy(p => p.String).ToList();
        var startFret = StartFret(ordered.Where(p => p.Fret is > 0).Select(p => p.Fret!.Value));

        var strings = ordered.Select(p =>
        {
            var name = tuning.Strings[p.String].ToString();
            if (p.Muted) { return new ChordDiagramString(name, true, false, null); }
            if (p.Open) { return new ChordDiagramString(name, false, true, null); }
            return new ChordDiagramString(name, false, false, p.Fret);
        }).ToList();

        return new ChordDiagram(strings, startFret, NumFrets: 4);
    }

    private static int StartFret(IEnumerable<int> frettedAboveOpen)
    {
        var list = frettedAboveOpen as ICollection<int> ?? frettedAboveOpen.ToList();
        return list.Count > 0 ? list.Min() : 1;
    }
}
