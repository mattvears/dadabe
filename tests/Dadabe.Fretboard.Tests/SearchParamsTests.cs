using System.Collections.Immutable;
using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

/// <summary>
/// <see cref="SearchParams.ContentHash"/> had no direct coverage before v0.6.1
/// (D58) adds a new field (<c>RequireInversion</c>) to it — these tests pin
/// down which fields currently participate, so a mistake wiring the new field
/// into <see cref="Canonical"/> shows up against a known-good baseline instead
/// of only against downstream voicing-id churn.
/// </summary>
public class SearchParamsTests
{
    private static SearchParams Default => SearchParams.Default;

    [Fact]
    public void Two_separately_constructed_equal_instances_hash_identically()
    {
        var a = new SearchParams(15, 4, 3, 6, true, true, false, ImmutableArray<string>.Empty, false);
        var b = new SearchParams(15, 4, 3, 6, true, true, false, ImmutableArray<string>.Empty, false);

        a.ContentHash.Should().Be(b.ContentHash);
    }

    [Theory]
    [InlineData(nameof(SearchParams.MaxFret))]
    [InlineData(nameof(SearchParams.MaxSpan))]
    [InlineData(nameof(SearchParams.MinStrings))]
    [InlineData(nameof(SearchParams.MaxStrings))]
    [InlineData(nameof(SearchParams.AllowOpen))]
    [InlineData(nameof(SearchParams.AllowBarre))]
    [InlineData(nameof(SearchParams.AllowThumb))]
    [InlineData(nameof(SearchParams.RequireRoot))]
    public void Changing_each_scalar_field_changes_the_hash(string field)
    {
        var baseline = Default;
        var changed = field switch
        {
            nameof(SearchParams.MaxFret) => baseline with { MaxFret = baseline.MaxFret + 1 },
            nameof(SearchParams.MaxSpan) => baseline with { MaxSpan = baseline.MaxSpan + 1 },
            nameof(SearchParams.MinStrings) => baseline with { MinStrings = baseline.MinStrings + 1 },
            nameof(SearchParams.MaxStrings) => baseline with { MaxStrings = baseline.MaxStrings + 1 },
            nameof(SearchParams.AllowOpen) => baseline with { AllowOpen = !baseline.AllowOpen },
            nameof(SearchParams.AllowBarre) => baseline with { AllowBarre = !baseline.AllowBarre },
            nameof(SearchParams.AllowThumb) => baseline with { AllowThumb = !baseline.AllowThumb },
            nameof(SearchParams.RequireRoot) => baseline with { RequireRoot = !baseline.RequireRoot },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        changed.ContentHash.Should().NotBe(baseline.ContentHash, $"{field} must participate in the hash");
    }

    [Fact]
    public void Categories_are_order_independent()
    {
        var forward = Default with { Categories = ["drop-2", "shell", "triad"] };
        var reversed = Default with { Categories = ["triad", "shell", "drop-2"] };

        forward.ContentHash.Should().Be(reversed.ContentHash, "Categories are sorted before hashing");
    }

    [Fact]
    public void Different_categories_hash_differently()
    {
        var a = Default with { Categories = ["drop-2"] };
        var b = Default with { Categories = ["shell"] };

        a.ContentHash.Should().NotBe(b.ContentHash);
    }

    [Fact]
    public void Empty_categories_differs_from_a_non_empty_set()
    {
        var empty = Default with { Categories = ImmutableArray<string>.Empty };
        var nonEmpty = Default with { Categories = ["shell"] };

        empty.ContentHash.Should().NotBe(nonEmpty.ContentHash);
    }
}
