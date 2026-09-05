using System.Collections.Immutable;
using Dadabe.Core;
using Dadabe.Core.Chord;
using Dadabe.Core.Memo;

namespace Dadabe.Fretboard;

/// <summary>
/// The (chord, tuning, hand, params) key for a voicing search, used as
/// the memo's lookup key (D18 §8.2).
/// </summary>
public sealed record VoicingSearchKey(
    ChordSpec Spec,
    Tuning Tuning,
    HandModel HandModel,
    SearchParams Params) : IContentHashable
{
    public ContentHash ContentHash
    {
        get
        {
            var c = new Canonical()
                .HashDigest(Spec.ContentHash)
                .HashDigest(Tuning.ContentHash)
                .HashDigest(HandModel.ContentHash)
                .HashDigest(Params.ContentHash);
            return ContentHash.FromCanonical(Namespaces.VoicingSearchKey, 1, c.AsSpan());
        }
    }
}

/// <summary>Memoizable result of <see cref="VoicingSearch.Search"/>.</summary>
public sealed record VoicingSet(ImmutableArray<Voicing> Voicings) : IContentHashable
{
    public ContentHash ContentHash
    {
        get
        {
            var c = new Canonical().U16BE((ushort)Voicings.Length);
            foreach (var v in Voicings) { c.HashDigest(v.ContentHash); }
            return ContentHash.FromCanonical(Namespaces.VoicingSearch, 1, c.AsSpan());
        }
    }
}

/// <summary>
/// Enumerates voicings in deterministic <see cref="D5"/> lex order: for
/// each string choose either a chord-tone fret (ascending) or muted
/// (sorted last). Steps map 1-to-1 with design.md §7.
/// </summary>
public static class VoicingSearch
{
    /// <summary>The muted token in our internal int[] candidate.</summary>
    public const int MutedToken = -1;

    public static VoicingSet Search(
        ChordSpec spec,
        Tuning tuning,
        HandModel hand,
        SearchParams p,
        VoicingCategoryCatalog categories,
        IMemo<VoicingSearchKey, VoicingSet>? cache = null,
        ModelVersion version = ModelVersion.V1)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(hand);
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(categories);

        var key = new VoicingSearchKey(spec, tuning, hand, p);
        if (cache is not null && cache.TryGet(key, out var cached))
        {
            return cached;
        }

        var effHand = ResolveEffectiveHand(hand, p);
        var reach = Reachability.Compute(spec, tuning, p.MaxFret);

        // Per-string choices: (fret, FretPosition?). FretPosition is null for muted.
        var perStringChoices = BuildChoices(reach, tuning.Strings.Length, p);

        var candidate = new int[tuning.Strings.Length];
        var voicings = ImmutableArray.CreateBuilder<Voicing>();
        var requiredFunctions = spec.Required.ToImmutableHashSet(StringComparer.Ordinal);
        // Opt-in: most forms deliberately omit the root from `required` (D4) — a
        // shell voicing's identity is carried by the 3rd/7th, and the root is
        // routinely dropped in real playing. RequireRoot lets a caller who wants
        // the root present anyway (e.g. teaching, or just wanting the note named
        // "Fm7" to contain an actual F) opt into that stricter floor per search.
        if (p.RequireRoot) { requiredFunctions = requiredFunctions.Add("1"); }

        Enumerate(
            perStringChoices,
            stringIdx: 0,
            candidate,
            spec,
            tuning,
            effHand,
            p,
            categories,
            reach,
            requiredFunctions,
            voicings,
            version);

        var set = new VoicingSet(voicings.ToImmutable());
        cache?.Put(key, set);
        return set;
    }

    private static HandModel ResolveEffectiveHand(HandModel hand, SearchParams p)
    {
        var thumb = p.AllowThumb ? hand.Thumb : hand.Thumb with { Allowed = false };
        var maxBarres = p.AllowBarre ? hand.MaxBarres : 0;
        return new HandModel(
            name: hand.Name,
            maxFret: Math.Min(hand.MaxFret, p.MaxFret),
            maxSpan: Math.Min(hand.MaxSpan, p.MaxSpan),
            minStrings: Math.Max(hand.MinStrings, p.MinStrings),
            maxStrings: Math.Min(hand.MaxStrings, p.MaxStrings),
            stretch: hand.Stretch,
            thumb: thumb,
            maxBarres: maxBarres);
    }

    private static ImmutableArray<ImmutableArray<Choice>> BuildChoices(
        Reachability reach, int stringCount, SearchParams p)
    {
        var perString = new ImmutableArray<Choice>.Builder[stringCount];
        for (var s = 0; s < stringCount; s++)
        {
            var builder = ImmutableArray.CreateBuilder<Choice>();
            foreach (var pos in reach.ForString(s).OrderBy(fp => fp.Fret ?? int.MaxValue))
            {
                if (pos.Fret is 0 && !p.AllowOpen) { continue; }
                builder.Add(new Choice(pos.Fret!.Value, pos));
            }
            builder.Add(new Choice(MutedToken, FretPosition.Mute(s)));
            perString[s] = builder;
        }
        return perString.Select(b => b.ToImmutable()).ToImmutableArray();
    }

    private static void Enumerate(
        ImmutableArray<ImmutableArray<Choice>> choices,
        int stringIdx,
        int[] candidate,
        ChordSpec spec,
        Tuning tuning,
        HandModel hand,
        SearchParams p,
        VoicingCategoryCatalog categories,
        Reachability reach,
        ImmutableHashSet<string> requiredFunctions,
        ImmutableArray<Voicing>.Builder output,
        ModelVersion version)
    {
        if (stringIdx == candidate.Length)
        {
            TryEmit(candidate, choices, spec, tuning, hand, p, categories, reach, requiredFunctions, output, version);
            return;
        }

        foreach (var choice in choices[stringIdx])
        {
            candidate[stringIdx] = choice.Fret;
            // Cheap incremental prune: if the partial candidate already exceeds the
            // hand's span among fretted positions so far, skip the rest of this
            // branch — futures only add more fretted positions, never shrink span.
            if (ExceedsSpanSoFar(candidate, stringIdx, hand)) { continue; }
            Enumerate(choices, stringIdx + 1, candidate, spec, tuning, hand, p,
                categories, reach, requiredFunctions, output, version);
        }
    }

    private static bool ExceedsSpanSoFar(int[] candidate, int upToInclusive, HandModel hand)
    {
        var min = int.MaxValue;
        var max = int.MinValue;
        for (var i = 0; i <= upToInclusive; i++)
        {
            var f = candidate[i];
            if (f <= 0) { continue; }
            if (f < min) { min = f; }
            if (f > max) { max = f; }
        }
        return min != int.MaxValue && max - min > hand.MaxSpan;
    }

    private static void TryEmit(
        int[] candidate,
        ImmutableArray<ImmutableArray<Choice>> choices,
        ChordSpec spec,
        Tuning tuning,
        HandModel hand,
        SearchParams p,
        VoicingCategoryCatalog categories,
        Reachability reach,
        ImmutableHashSet<string> requiredFunctions,
        ImmutableArray<Voicing>.Builder output,
        ModelVersion version)
    {
        if (!Playability.Admissible(candidate, hand)) { return; }

        var fingering = FingeringSolver.Solve(candidate, hand);
        if (fingering is null) { return; }

        // Build per-string positions using Reach for sounded slots.
        var positionsBuilder = ImmutableArray.CreateBuilder<FretPosition>(candidate.Length);
        var presentFunctions = new HashSet<string>(StringComparer.Ordinal);
        for (var s = 0; s < candidate.Length; s++)
        {
            var f = candidate[s];
            if (f == MutedToken) { positionsBuilder.Add(FretPosition.Mute(s)); continue; }
            var pos = reach.ForString(s).First(fp => fp.Fret == f);
            positionsBuilder.Add(pos);
            if (pos.Function is not null) { presentFunctions.Add(pos.Function); }
        }

        // Required-tone check (D4 floor).
        foreach (var req in requiredFunctions)
        {
            if (!presentFunctions.Contains(req)) { return; }
        }

        var positions = positionsBuilder.ToImmutable();

        // Slash-chord bass constraint: the lowest sounding pitch must be the
        // requested bass. Compared by pitch class, so any octave of the bass
        // note counts. Strings are not assumed to ascend in pitch, so this
        // works on re-entrant tunings too.
        if (spec.Bass is { } bass && !LowestSoundingPitchClassIs(positions, bass.PitchClass.Value))
        {
            return;
        }

        var structure = Classifier.Classify(positions, categories);
        if (!p.Categories.IsDefaultOrEmpty && !p.Categories.Contains(structure, StringComparer.Ordinal))
        {
            return;
        }

        // E8/D24: v2 will invoke a function classifier here gated on
        // version >= ModelVersion.V2; v1 leaves functions empty.
        IReadOnlyList<string> functions = Array.Empty<string>();

        var span = ComputeSpan(positions);
        // Mutes running off either end of the neck are simply not struck, so
        // they cost nothing; only interior mutes need deadening.
        var interiorMutedStrings = positions.Count(pp => pp.Muted) - Voicing.CountEdgeMutes(positions);
        var isolatedMutedStrings = Voicing.CountIsolatedMutes(positions);
        var lowestFret = positions
            .Where(pp => pp.Fret is > 0)
            .Select(pp => pp.Fret!.Value)
            .DefaultIfEmpty(0)
            .Min();
        var comfort = Voicing.ComputeComfort(
            span,
            interiorMutedStrings,
            isolatedMutedStrings,
            fingering.Barres,
            lowestFret,
            hand.MaxSpan,
            hand.MaxFret,
            fingering.Assignments,
            positions);

        output.Add(new Voicing(spec, tuning, hand, positions, fingering, structure, comfort, functions));
    }

    /// <summary>
    /// True when the lowest-pitched sounded position in <paramref name="positions"/>
    /// has pitch class <paramref name="pitchClass"/>. False for an all-muted
    /// candidate, which has no bass to speak of.
    /// </summary>
    private static bool LowestSoundingPitchClassIs(ImmutableArray<FretPosition> positions, int pitchClass)
    {
        var lowestMidi = int.MaxValue;
        var lowestPc = -1;
        foreach (var p in positions)
        {
            if (p.Muted || p.SoundingPitch is not { } pitch) { continue; }
            if (pitch.Midi < lowestMidi)
            {
                lowestMidi = pitch.Midi;
                lowestPc = pitch.PitchClass.Value;
            }
        }
        return lowestPc == pitchClass;
    }

    private static int ComputeSpan(ImmutableArray<FretPosition> positions)
    {
        var min = int.MaxValue;
        var max = int.MinValue;
        foreach (var p in positions)
        {
            if (p.Fret is not { } f || f <= 0) { continue; }
            if (f < min) { min = f; }
            if (f > max) { max = f; }
        }
        return min == int.MaxValue ? 0 : max - min;
    }

    private readonly record struct Choice(int Fret, FretPosition Position);
}
