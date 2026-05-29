using System.Collections.Immutable;

namespace Dadabe.Fretboard;

/// <summary>
/// Maps a candidate position tuple to a valid <see cref="Fingering"/>, or
/// <c>null</c> when no assignment satisfies the eight rules in design.md §4.
/// </summary>
/// <remarks>
/// <para><strong>Deterministic preference order</strong> (load-bearing for
/// <c>Fingering.ContentHash</c> stability):</para>
/// <list type="number">
/// <item>Fretted positions are grouped by fret; groups iterate in
/// ascending-fret, then ascending lowest-string order.</item>
/// <item>If naïve assignment (one finger per fretted string) fits within
/// four fingers, no barre is used.</item>
/// <item>Otherwise, multi-string groups are converted to barres greedily —
/// largest group first, then by ascending fret — until the finger budget
/// or <see cref="HandModel.MaxBarres"/> is exhausted.</item>
/// <item>Fingers <c>Index</c>, <c>Middle</c>, <c>Ring</c>, <c>Pinky</c> are
/// allocated to groups (and to extra slots in non-barred multi-string
/// groups) in iteration order. Thumb is used iff still over budget and
/// the lowest-fret group is a singleton on string 0 with fret ≤
/// <see cref="ThumbPolicy.MaxFret"/>.</item>
/// <item>For each muted string, mute sources are tried in
/// <see cref="MuteSource"/> enum order; first legal source wins.</item>
/// </list>
/// <para><strong>v0.1 deviation from pseudocode §5 V8 (ThumbWrap):</strong>
/// the pseudocode rule <c>s = 0 ∧ ∃ (0, _, 5) ∈ A</c> is unsatisfiable
/// under V1 (a string can't be both fretted and muted). The design's
/// example output uses <c>thumb-wrap</c> mute on string 0 with no thumb in
/// the assignments. This solver follows the example: ThumbWrap mute on
/// string 0 is valid iff <see cref="ThumbPolicy.Allowed"/>.</para>
/// </remarks>
public static class FingeringSolver
{
    private static readonly MuteSource[] MuteSourcePreference =
    {
        MuteSource.AdjacentUnderside,
        MuteSource.BarreExtended,
        MuteSource.ThumbWrap,
        MuteSource.OuterHand,
        MuteSource.Unfretted,
    };

    private static readonly Finger[] FingerOrder = { Finger.Index, Finger.Middle, Finger.Ring, Finger.Pinky };

    public static Fingering? Solve(ReadOnlySpan<int> candidate, HandModel hand)
    {
        ArgumentNullException.ThrowIfNull(hand);
        if (candidate.Length == 0) { return null; }

        var stringCount = candidate.Length;

        // Build fret groups + collect mutes & open strings.
        var groupsByFret = new SortedDictionary<int, List<int>>();
        var mutedStrings = new List<int>();
        var openStrings = new List<int>();
        var anySounded = false;
        for (var s = 0; s < stringCount; s++)
        {
            var f = candidate[s];
            if (f < 0) { mutedStrings.Add(s); continue; }
            anySounded = true;
            if (f == 0) { openStrings.Add(s); continue; }
            if (!groupsByFret.TryGetValue(f, out var list))
            {
                list = new List<int>();
                groupsByFret[f] = list;
            }
            list.Add(s);
        }
        if (!anySounded) { return null; }
        foreach (var l in groupsByFret.Values) { l.Sort(); }

        var fretGroups = groupsByFret
            .Select(kvp => new FretGroup(kvp.Key, kvp.Value.ToImmutableArray()))
            .ToImmutableArray();

        // Decide barres + thumb so the assignment fits within four fingers.
        if (!ChooseBarresAndThumb(fretGroups, hand, out var barredGroupIndices, out var useThumbOnGroup))
        {
            return null;
        }

        var assignmentsBuilder = ImmutableArray.CreateBuilder<FingerAssignment>();
        var barresBuilder = ImmutableArray.CreateBuilder<BarreGroup>();
        var fingerFret = new Dictionary<Finger, int>();
        var nextNonThumbFinger = 0;

        for (var gi = 0; gi < fretGroups.Length; gi++)
        {
            var group = fretGroups[gi];
            var isBarre = barredGroupIndices.Contains(gi);
            var isThumb = useThumbOnGroup == gi;

            if (isThumb)
            {
                if (!hand.Thumb.Allowed || group.Strings.Length != 1
                    || group.Strings[0] != hand.Thumb.String || group.Fret > hand.Thumb.MaxFret)
                {
                    return null;
                }
                fingerFret[Finger.Thumb] = group.Fret;
                assignmentsBuilder.Add(new FingerAssignment(group.Strings[0], group.Fret, Finger.Thumb));
                continue;
            }

            if (isBarre)
            {
                if (nextNonThumbFinger >= FingerOrder.Length) { return null; }
                var finger = FingerOrder[nextNonThumbFinger++];
                fingerFret[finger] = group.Fret;
                barresBuilder.Add(new BarreGroup(
                    finger,
                    group.Fret,
                    LowStringInclusive: group.Strings[0],
                    HighStringInclusive: group.Strings[^1]));
                foreach (var s in group.Strings)
                {
                    assignmentsBuilder.Add(new FingerAssignment(s, group.Fret, finger));
                }
            }
            else
            {
                foreach (var s in group.Strings)
                {
                    if (nextNonThumbFinger >= FingerOrder.Length) { return null; }
                    var finger = FingerOrder[nextNonThumbFinger++];
                    // For multi-finger non-barred groups, all fingers sit on the same fret —
                    // we record each finger's fret independently for the reach check below.
                    fingerFret[finger] = group.Fret;
                    assignmentsBuilder.Add(new FingerAssignment(s, group.Fret, finger));
                }
            }
        }

        if (barresBuilder.Count > hand.MaxBarres) { return null; }

        // (V6) Inter-finger reach.
        foreach (var (f1, fret1) in fingerFret)
        {
            if (f1 == Finger.Thumb) { continue; }
            foreach (var (f2, fret2) in fingerFret)
            {
                if (f2 == Finger.Thumb || (int)f2 <= (int)f1) { continue; }
                var allowed = hand.StretchBetween(f1, f2);
                if (Math.Abs(fret1 - fret2) > allowed) { return null; }
            }
        }

        // Open strings.
        foreach (var s in openStrings)
        {
            assignmentsBuilder.Add(new FingerAssignment(s, 0, null));
        }

        assignmentsBuilder.Sort((a, b) => a.String.CompareTo(b.String));
        var assignments = assignmentsBuilder.ToImmutable();
        var barres = barresBuilder.ToImmutable();

        // Mute sources.
        var mutesBuilder = ImmutableArray.CreateBuilder<MuteAssignment>();
        foreach (var s in mutedStrings)
        {
            MuteSource? chosen = null;
            foreach (var source in MuteSourcePreference)
            {
                if (IsMuteLegal(source, s, stringCount, assignments, barres, hand, mutesBuilder))
                {
                    chosen = source;
                    break;
                }
            }
            if (chosen is null) { return null; }
            mutesBuilder.Add(new MuteAssignment(s, chosen.Value));
        }

        return new Fingering(assignments, barres, mutesBuilder.ToImmutable());
    }

    /// <summary>
    /// Decide which groups to barre and whether the thumb takes the lowest
    /// group, so the total finger demand fits in four fingers (plus the
    /// optional thumb). Returns false if no admissible plan exists.
    /// </summary>
    private static bool ChooseBarresAndThumb(
        ImmutableArray<FretGroup> groups,
        HandModel hand,
        out HashSet<int> barredGroupIndices,
        out int useThumbOnGroup)
    {
        var barred = new HashSet<int>();
        var thumbGroup = -1;

        int FingersNeeded()
        {
            var n = 0;
            for (var i = 0; i < groups.Length; i++)
            {
                n += barred.Contains(i) ? 1 : groups[i].Strings.Length;
            }
            if (thumbGroup >= 0) { n -= 1; }
            return n;
        }

        var multiStringGroups = Enumerable.Range(0, groups.Length)
            .Where(i => groups[i].Strings.Length > 1)
            .OrderByDescending(i => groups[i].Strings.Length)
            .ThenBy(i => groups[i].Fret)
            .ToList();

        while (FingersNeeded() > FingerOrder.Length && barred.Count < hand.MaxBarres)
        {
            var next = multiStringGroups.FirstOrDefault(i => !barred.Contains(i), -1);
            if (next < 0) { break; }
            barred.Add(next);
        }

        if (FingersNeeded() > FingerOrder.Length)
        {
            if (!hand.Thumb.Allowed || groups.Length == 0)
            {
                barredGroupIndices = barred;
                useThumbOnGroup = thumbGroup;
                return false;
            }
            var lowest = groups[0];
            if (lowest.Strings.Length == 1
                && lowest.Strings[0] == hand.Thumb.String
                && lowest.Fret <= hand.Thumb.MaxFret)
            {
                thumbGroup = 0;
            }
            else
            {
                barredGroupIndices = barred;
                useThumbOnGroup = thumbGroup;
                return false;
            }
        }

        barredGroupIndices = barred;
        useThumbOnGroup = thumbGroup;
        return FingersNeeded() <= FingerOrder.Length;
    }

    private static bool IsMuteLegal(
        MuteSource source,
        int s,
        int stringCount,
        ImmutableArray<FingerAssignment> assignments,
        ImmutableArray<BarreGroup> barres,
        HandModel hand,
        ImmutableArray<MuteAssignment>.Builder mutesSoFar)
    {
        switch (source)
        {
            case MuteSource.AdjacentUnderside:
                return assignments.Any(a => a.Fret > 0 && Math.Abs(a.String - s) == 1);

            case MuteSource.BarreExtended:
                return barres.Any(b => s >= b.LowStringInclusive && s <= b.HighStringInclusive);

            case MuteSource.ThumbWrap:
                return hand.Thumb.Allowed && s == hand.Thumb.String;

            case MuteSource.OuterHand:
                {
                    var alreadyMuted = mutesSoFar.Select(m => m.String).ToHashSet();
                    var remaining = Enumerable.Range(0, stringCount)
                        .Where(x => x != s && !alreadyMuted.Contains(x))
                        .ToArray();
                    if (remaining.Length == 0) { return false; }
                    return s < remaining[0] || s > remaining[^1];
                }

            case MuteSource.Unfretted:
                {
                    if (assignments.Any(a => a.Fret > 0 && Math.Abs(a.String - s) <= 1)) { return false; }
                    if (barres.Any(b => s >= b.LowStringInclusive - 1 && s <= b.HighStringInclusive + 1)) { return false; }
                    return true;
                }

            default:
                return false;
        }
    }

    private sealed record FretGroup(int Fret, ImmutableArray<int> Strings);
}
