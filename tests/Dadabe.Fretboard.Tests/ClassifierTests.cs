using System.Collections.Immutable;
using Dadabe.Core;
using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

public class ClassifierTests
{
    private static readonly VoicingCategoryCatalog Catalog = VoicingCategoryCatalog.Load(null);

    private static FretPosition Sounded(int s, int fret, string note, string function)
    {
        var pitch = Pitch.Parse(note);
        return new FretPosition(s, fret, pitch, pitch.Note, function);
    }

    [Fact]
    public void Triad_three_chord_tones_classifies_as_triad()
    {
        var positions = ImmutableArray.Create(
            Sounded(0, 3, "C3", "1"),
            Sounded(1, 2, "E3", "3"),
            Sounded(2, 0, "G3", "5"));
        Classifier.Classify(positions, Catalog).Should().Be("triad");
    }

    [Fact]
    public void Shell_3_and_7_only_classifies_as_shell()
    {
        var positions = ImmutableArray.Create(
            Sounded(0, 3, "C3", "1"),
            Sounded(1, 2, "E3", "3"),
            Sounded(2, 3, "Bb3", "b7"));
        Classifier.Classify(positions, Catalog).Should().Be("shell");
    }

    [Fact]
    public void Spread_fallthrough_when_nothing_else_matches()
    {
        // Five sounded notes — outside triad/shell/drop-2/drop-3 templates.
        var positions = ImmutableArray.Create(
            Sounded(0, 3, "C3", "1"),
            Sounded(1, 2, "E3", "3"),
            Sounded(2, 0, "G3", "5"),
            Sounded(3, 0, "B3", "7"),
            Sounded(4, 1, "E4", "3"));
        Classifier.Classify(positions, Catalog).Should().Be("spread");
    }

    [Fact]
    public void Empty_positions_falls_through_to_spread()
    {
        Classifier.Classify(ImmutableArray<FretPosition>.Empty, Catalog).Should().Be("spread");
    }

    [Fact]
    public void Root_and_fifth_dyad_classifies_as_power()
    {
        var positions = ImmutableArray.Create(
            Sounded(0, 3, "C3", "1"),
            Sounded(1, 5, "G3", "5"));
        Classifier.Classify(positions, Catalog).Should().Be("power");
    }

    [Fact]
    public void Octave_doubled_power_shape_is_power_not_triad()
    {
        // The standard guitar power chord: root, fifth, octave root. Three
        // sounded notes all drawn from {1,5}, so 'triad' would swallow it if
        // 'power' were not evaluated first.
        var positions = ImmutableArray.Create(
            Sounded(0, 3, "C3", "1"),
            Sounded(1, 5, "G3", "5"),
            Sounded(2, 5, "C4", "1"));
        Classifier.Classify(positions, Catalog).Should().Be("power");
    }

    [Fact]
    public void Power_does_not_capture_voicings_containing_a_third()
    {
        var positions = ImmutableArray.Create(
            Sounded(0, 3, "C3", "1"),
            Sounded(1, 5, "G3", "5"),
            Sounded(2, 2, "E4", "3"));
        Classifier.Classify(positions, Catalog).Should().Be("triad");
    }
}
