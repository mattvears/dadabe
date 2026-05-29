using Dadabe.Core;
using Dadabe.Core.Chord;
using Dadabe.Core.Memo;
using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

public class VoicingSearchTests
{
    private static readonly Environment Env = Environment.Default();
    private static readonly ChordParser Parser = new(Env.Catalogs.ChordGrammar);
    private static readonly ChordExpander Expander = new(Env.Catalogs.ChordGrammar);

    private static VoicingSet SearchCmaj7(SearchParams? p = null, IMemo<VoicingSearchKey, VoicingSet>? cache = null)
    {
        var spec = Expander.Expand(Parser.Parse("Cmaj7"));
        var tuning = Env.Catalogs.Tunings.Get("STANDARD");
        return VoicingSearch.Search(spec, tuning, Env.HandModel, p ?? SearchParams.Default, Env.Catalogs.VoicingCategories, cache);
    }

    [Fact]
    public void Returns_some_voicings_for_Cmaj7_in_standard()
    {
        var set = SearchCmaj7();
        set.Voicings.Should().NotBeEmpty();
    }

    [Fact]
    public void Every_voicing_only_contains_chord_pitch_classes()
    {
        var spec = Expander.Expand(Parser.Parse("Cmaj7"));
        var chordPcs = spec.PitchClasses.Select(pc => pc.Value).ToHashSet();
        var set = SearchCmaj7();
        foreach (var v in set.Voicings)
        {
            foreach (var pos in v.Sounded)
            {
                pos.SoundingPitch!.Value.PitchClass.Value.Should().BeOneOf(chordPcs);
            }
        }
    }

    [Fact]
    public void Every_voicing_includes_required_chord_tones()
    {
        var spec = Expander.Expand(Parser.Parse("Cmaj7"));
        var set = SearchCmaj7();
        foreach (var v in set.Voicings)
        {
            var present = v.Sounded.Select(p => p.Function).ToHashSet();
            foreach (var req in spec.Required)
            {
                present.Should().Contain(req, $"voicing {v.ContentHash} must include required tone {req}");
            }
        }
    }

    [Fact]
    public void Every_voicing_has_a_valid_fingering()
    {
        var set = SearchCmaj7();
        foreach (var v in set.Voicings)
        {
            v.Fingering.Should().NotBeNull();
        }
    }

    [Fact]
    public void Every_voicing_carries_a_category()
    {
        var set = SearchCmaj7();
        foreach (var v in set.Voicings)
        {
            v.Structure.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void Comfort_is_in_zero_one()
    {
        var set = SearchCmaj7();
        foreach (var v in set.Voicings)
        {
            v.Comfort.Should().BeInRange(0.0, 1.0);
        }
    }

    [Fact]
    public void Cache_returns_identical_set()
    {
        var cache = new InMemoryMemo<VoicingSearchKey, VoicingSet>();
        var first = SearchCmaj7(cache: cache);
        var second = SearchCmaj7(cache: cache);

        second.Should().BeSameAs(first);
        second.ContentHash.Should().Be(first.ContentHash);
    }

    [Fact]
    public void Search_is_deterministic_across_runs_without_cache()
    {
        var a = SearchCmaj7();
        var b = SearchCmaj7();
        a.Voicings.Length.Should().Be(b.Voicings.Length);
        for (var i = 0; i < a.Voicings.Length; i++)
        {
            a.Voicings[i].ContentHash.Should().Be(b.Voicings[i].ContentHash);
        }
    }

    [Fact]
    public void Search_with_and_without_cache_produces_byte_identical_ids()
    {
        // D18 §8.4: memoization never changes the result, only the timing.
        var cache = new InMemoryMemo<VoicingSearchKey, VoicingSet>();
        var noCache = SearchCmaj7();
        var withCache = SearchCmaj7(cache: cache);

        noCache.Voicings.Length.Should().Be(withCache.Voicings.Length);
        for (var i = 0; i < noCache.Voicings.Length; i++)
        {
            noCache.Voicings[i].ContentHash.Should().Be(withCache.Voicings[i].ContentHash);
        }
    }

    [Fact]
    public void Disallowing_open_strings_drops_voicings_with_open_strings()
    {
        var noOpen = SearchParams.Default with { AllowOpen = false };
        var set = SearchCmaj7(noOpen);
        foreach (var v in set.Voicings)
        {
            v.OpenStrings.Should().Be(0);
        }
    }

    [Fact]
    public void Disallowing_barre_drops_voicings_with_barres()
    {
        var noBarre = SearchParams.Default with { AllowBarre = false };
        var set = SearchCmaj7(noBarre);
        foreach (var v in set.Voicings)
        {
            v.Fingering.Barres.Should().BeEmpty();
        }
    }

    [Fact]
    public void Lexicographic_order_low_strings_then_low_frets_first()
    {
        // The first emitted voicing should not have a higher-fret choice on string 0
        // than a later voicing that's a chord-tone position on string 0.
        var set = SearchCmaj7();
        if (set.Voicings.Length < 2) { return; }
        var first = set.Voicings[0];
        var second = set.Voicings[1];
        // Compare by per-string position tuples (muted = int.MaxValue).
        int Encode(int? f) => f ?? int.MaxValue;
        for (var s = 0; s < first.Positions.Length; s++)
        {
            var a = Encode(first.Positions[s].Fret);
            var b = Encode(second.Positions[s].Fret);
            if (a != b) { a.Should().BeLessThan(b); break; }
        }
    }
}
