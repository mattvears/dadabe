using System.Text.Json.Nodes;
using Dadabe.Fretboard;
using FluentAssertions;

namespace Dadabe.Cli.Tests;

/// <summary>
/// Direct coverage of <see cref="Commands.VoicingsCommand.Run"/> behavior that
/// sits outside the search itself: the post-search <c>minComfort</c> filter and
/// the slash-bass-unreachable warning. Both are precedents v0.6.1 extends
/// (<c>minResonance</c> follows the same post-filter shape; the inversion
/// warning follows the same "warn, don't silently drop" shape), so a
/// regression here would be easy to miss without an isolated test.
/// </summary>
public class VoicingsCommandTests
{
    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();

    private static double[] Comforts(string json) => Parse(json)["data"]!["voicings"]!.AsArray()
        .Select(v => v!["comfort"]!.GetValue<double>())
        .ToArray();

    private static List<string> Warnings(string json) => Parse(json)["warnings"]!.AsArray()
        .Select(w => w!.GetValue<string>())
        .ToList();

    [Fact]
    public void MinComfort_zero_returns_every_voicing_unfiltered()
    {
        var unfiltered = TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 100);
        var explicitZero = TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 100, minComfort: 0.0);

        Comforts(explicitZero).Should().Equal(Comforts(unfiltered));
    }

    [Fact]
    public void MinComfort_excludes_voicings_below_the_threshold()
    {
        var all = Comforts(TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 100));
        all.Should().Contain(c => c < 0.9, "the fixture needs at least one voicing below the threshold to be meaningful");

        var filtered = Comforts(TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 100, minComfort: 0.9));

        filtered.Should().OnlyContain(c => c >= 0.9);
        filtered.Length.Should().BeLessThan(all.Length);
    }

    [Fact]
    public void MinComfort_above_every_voicing_yields_an_empty_list_not_an_error()
    {
        var filtered = Parse(TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 100, minComfort: 1.1))
            ["data"]!["voicings"]!.AsArray();

        filtered.Should().BeEmpty();
    }

    [Fact]
    public void Unreachable_slash_bass_warns_instead_of_silently_returning_nothing()
    {
        // Same starved search VoicingSearchTests uses to force an empty set for
        // a foreign slash bass: 3 frets, span 1, no open strings.
        var starved = SearchParams.Default with { MaxFret = 3, MaxSpan = 1, AllowOpen = false };
        var json = TestHelpers.RunVoicings("C/D", "STANDARD", limit: 100, searchParams: starved);

        Parse(json)["data"]!["voicings"]!.AsArray().Should().BeEmpty();
        Warnings(json).Should().ContainSingle(w => w.Contains("in the bass", StringComparison.Ordinal));
    }

    [Fact]
    public void Reachable_slash_bass_carries_no_warning()
    {
        var json = TestHelpers.RunVoicings("C/G", "STANDARD", limit: 5);

        Parse(json)["data"]!["voicings"]!.AsArray().Should().NotBeEmpty();
        Warnings(json).Should().BeEmpty();
    }

    [Fact]
    public void Root_position_chord_never_warns_even_when_a_search_would_be_empty()
    {
        // No slash bass at all, just an impossibly starved search — the warning
        // is gated on spec.Bass being set, not merely on an empty result.
        var starved = SearchParams.Default with { MaxFret = 1, MaxSpan = 0, AllowOpen = false, AllowBarre = false };
        var json = TestHelpers.RunVoicings("Cmaj7", "STANDARD", limit: 100, searchParams: starved);

        Warnings(json).Should().BeEmpty();
    }
}
