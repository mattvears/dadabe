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

    private static VoicingSet SearchIn(string symbol, string tuningName = "STANDARD", SearchParams? p = null)
    {
        var spec = Expander.Expand(Parser.Parse(symbol));
        var tuning = Env.Catalogs.Tunings.Get(tuningName);
        return VoicingSearch.Search(spec, tuning, Env.HandModel, p ?? SearchParams.Default,
            Env.Catalogs.VoicingCategories);
    }

    private static int LowestPitchClass(Voicing v) => v.BassNote!.Value.PitchClass.Value;

    /// <summary>Fret shape as a comparable string, e.g. "x,3,2,0,1,0".</summary>
    private static string Shape(Voicing v) => string.Join(",", v.Positions
        .OrderBy(p => p.String)
        .Select(p => p.Fret?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "x"));

    [Theory]
    [InlineData("C/G", 7)]   // inversion — G is already the 5th
    [InlineData("C/E", 4)]   // inversion — E is already the 3rd
    [InlineData("C/D", 2)]   // foreign bass — D is appended to the spec
    [InlineData("Cmaj7/B", 11)]
    public void Slash_chord_voicings_all_put_the_requested_bass_lowest(string symbol, int expectedBassPc)
    {
        var set = SearchIn(symbol);
        set.Voicings.Should().NotBeEmpty();
        foreach (var v in set.Voicings)
        {
            LowestPitchClass(v).Should().Be(expectedBassPc);
        }
    }

    [Fact]
    public void Slash_chord_result_is_a_strict_subset_of_the_root_position_result()
    {
        var rootPosition = SearchIn("C").Voicings.Select(v => v.ContentHash.ToString()).ToHashSet();
        var overG = SearchIn("C/G").Voicings.Select(v => v.ContentHash.ToString()).ToList();

        overG.Should().NotBeEmpty();
        overG.Count.Should().BeLessThan(rootPosition.Count, "the bass constraint must actually filter");
        // Ids differ because ChordSpec.Bass participates in the hash, so compare
        // on the shape instead: every C/G voicing must be a playable C voicing.
        SearchIn("C/G").Voicings.Select(Shape)
            .Should().BeSubsetOf(SearchIn("C").Voicings.Select(Shape));
    }

    [Fact]
    public void Foreign_bass_voicings_carry_the_bass_function_label()
    {
        var set = SearchIn("C/D");
        set.Voicings.Should().NotBeEmpty();
        foreach (var v in set.Voicings)
        {
            var lowest = v.Sounded.OrderBy(p => p.SoundingPitch!.Value.Midi).First();
            lowest.Function.Should().Be(ChordSpec.BassFunction);
        }
    }

    [Fact]
    public void Unplayable_bass_yields_an_empty_set_rather_than_falling_back()
    {
        // Deliberately starved search: 3 frets, span 1, no open strings.
        var starved = SearchParams.Default with { MaxFret = 3, MaxSpan = 1, AllowOpen = false };
        SearchIn("C/D", p: starved).Voicings.Should().BeEmpty();
    }

    [Fact]
    public void Power_chord_search_yields_only_root_and_fifth_voicings()
    {
        var spec = Expander.Expand(Parser.Parse("C5"));
        var tuning = Env.Catalogs.Tunings.Get("STANDARD");
        var set = VoicingSearch.Search(spec, tuning, Env.HandModel, SearchParams.Default,
            Env.Catalogs.VoicingCategories);

        set.Voicings.Should().NotBeEmpty();
        foreach (var v in set.Voicings)
        {
            var functions = v.Sounded.Select(p => p.Function).ToHashSet();
            functions.Should().BeSubsetOf(["1", "5"], "a power chord has no third");
            functions.Should().Contain("1").And.Contain("5");
            v.Structure.Should().Be("power");
        }
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

    // ---- RequireRoot (D4 opt-in) ----

    private static bool HasRootFunction(Voicing v) =>
        v.Positions.Any(p => !p.Muted && p.Function == "1");

    [Fact]
    public void By_default_rootless_voicings_are_returned_for_m7()
    {
        // m7's `required` list is ["b3", "b7"] — the root is deliberately not required
        // (D4): a shell voicing's identity is carried by the 3rd/7th.
        var set = SearchIn("Fm7");
        set.Voicings.Should().Contain(v => !HasRootFunction(v), "the default floor omits the root for tertian sevenths");
    }

    [Fact]
    public void RequireRoot_excludes_every_rootless_voicing()
    {
        var withRoot = SearchIn("Fm7", p: SearchParams.Default with { RequireRoot = true });
        withRoot.Voicings.Should().NotBeEmpty();
        withRoot.Voicings.Should().OnlyContain(v => HasRootFunction(v));
    }

    [Fact]
    public void RequireRoot_is_a_no_op_for_the_power_chord_form_which_already_requires_it()
    {
        var normal = SearchIn("F5");
        var strict = SearchIn("F5", p: SearchParams.Default with { RequireRoot = true });
        strict.Voicings.Length.Should().Be(normal.Voicings.Length);
    }
}
