using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

public class TransformCatalogTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly TransformCatalog Catalog = TransformCatalog.CreateDefault(Grammar);

    private static ChordSymbol[] ParseAll(params string[] symbols) => symbols.Select(Parser.Parse).ToArray();

    [Fact]
    public void Catalog_contains_all_eleven_key_blind_transforms_plus_the_key_aware_family()
    {
        var ids = Catalog.Select(t => t.Id).ToArray();
        ids.Should().BeEquivalentTo([
            "transpose", "retrograde", "rotate", "invert",
            "interval-negate", "interval-reverse", "interval-multiply",
            "quality-map", "reduce", "tritone-sub", "plr",
            "diatonic-transpose", "parallel-mode", "substitute", "negative-harmony",
        ]);
        Catalog.Count.Should().Be(15);
    }

    [Fact]
    public void Every_registered_transform_round_trips_output_through_ToSymbol()
    {
        var chords = ParseAll("C", "F", "G", "Am7");
        foreach (var transform in Catalog)
        {
            var parameters = DefaultParamsFor(transform.Id);
            var result = transform.Apply(chords, parameters);
            foreach (var symbol in result.Chords)
            {
                var act = () => Parser.Parse(symbol);
                act.Should().NotThrow($"'{symbol}' emitted by '{transform.Id}' should be parseable");
            }
        }
    }

    [Theory]
    [InlineData("retrograde")]
    [InlineData("invert")]
    [InlineData("interval-negate")]
    [InlineData("interval-reverse")]
    public void Invertible_transforms_with_no_parameters_return_to_the_original_when_applied_twice(string id)
    {
        var chords = ParseAll("C", "F", "G", "Em");
        var transform = Catalog[id];
        var parameters = DefaultParamsFor(id);

        var once = transform.Apply(chords, parameters);
        var onceChords = once.Chords.Select(Parser.Parse).ToArray();
        var twice = transform.Apply(onceChords, parameters);

        twice.Chords.Should().Equal(chords.Select(c => c.ToSymbol()));
    }

    [Fact]
    public void Transpose_inverts_by_negating_the_interval()
    {
        var chords = ParseAll("C", "F", "G");
        var transform = Catalog["transpose"];

        var up = transform.Apply(chords, new Dictionary<string, object> { ["interval"] = "M2" });
        var upChords = up.Chords.Select(Parser.Parse).ToArray();

        // The inverse of +M2 is -M2: same quality label, negated semitone count.
        var down = transform.Apply(upChords, new Dictionary<string, object> { ["semitones"] = -2 });
        down.Chords.Should().Equal(chords.Select(c => c.ToSymbol()));
    }

    [Fact]
    public void Rotate_inverts_by_negating_by()
    {
        var chords = ParseAll("C", "F", "G", "Am");
        var transform = Catalog["rotate"];

        var rotated = transform.Apply(chords, new Dictionary<string, object> { ["by"] = 1 });
        var rotatedChords = rotated.Chords.Select(Parser.Parse).ToArray();
        var back = transform.Apply(rotatedChords, new Dictionary<string, object> { ["by"] = -1 });

        back.Chords.Should().Equal(chords.Select(c => c.ToSymbol()));
    }

    private static Dictionary<string, object> DefaultParamsFor(string id) => id switch
    {
        "transpose" => new() { ["interval"] = "M2" },
        "rotate" => new() { ["by"] = 1 },
        "invert" => new() { ["axis"] = "C" },
        "quality-map" => new() { ["to"] = "m7" },
        "reduce" => new() { ["level"] = "triad" },
        "tritone-sub" => new(),
        "plr" => new() { ["op"] = "P" },
        "diatonic-transpose" => new() { ["by"] = 1 },
        "parallel-mode" => new() { ["mode"] = "mixolydian" },
        "negative-harmony" => new() { ["axis"] = "C" },
        _ => [],
    };
}
