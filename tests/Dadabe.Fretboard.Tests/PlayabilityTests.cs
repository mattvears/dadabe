using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

public class PlayabilityTests
{
    private static readonly HandModel Hand = HandModel.Default;

    [Fact]
    public void Within_span_and_string_count_is_admissible()
    {
        int[] candidate = { -1, 3, 2, 0, 1, 0 };
        Playability.Admissible(candidate, Hand).Should().BeTrue();
    }

    [Fact]
    public void Span_exceeded_is_rejected()
    {
        int[] candidate = { 0, 1, -1, -1, -1, 10 };
        Playability.Admissible(candidate, Hand).Should().BeFalse();
    }

    [Fact]
    public void Too_few_sounded_strings_is_rejected()
    {
        int[] candidate = { -1, -1, -1, -1, -1, 0 };
        Playability.Admissible(candidate, Hand).Should().BeFalse();
    }

    [Fact]
    public void All_open_passes()
    {
        int[] candidate = { 0, 0, 0, 0, 0, 0 };
        Playability.Admissible(candidate, Hand).Should().BeTrue();
    }
}
