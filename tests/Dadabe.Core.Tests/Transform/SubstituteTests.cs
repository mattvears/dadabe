using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

public class SubstituteTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly TransformCatalog Catalog = TransformCatalog.CreateDefault(Grammar);

    private static TransformResult Apply(string[] chords, Dictionary<string, object> parameters) =>
        Catalog["substitute"].Apply(chords.Select(Parser.Parse).ToArray(), parameters);

    [Fact]
    public void Reproduces_the_design_docs_worked_example_in_C_major()
    {
        var result = Apply(["C", "F", "G"], new() { ["key"] = "C" });
        result.Chords.Should().Equal("Am", "Dm", "Bdim");
    }

    [Fact]
    public void Rule_generalises_to_a_different_key()
    {
        // Same functional-group rule in G major: I->vi, IV->ii, V->vii deg.
        var result = Apply(["G", "C", "D"], new() { ["key"] = "G" });
        result.Chords.Should().Equal("Em", "Am", "F#dim");
    }

    [Fact]
    public void Variant_selects_the_alternate_tonic_group_member()
    {
        var result = Apply(["C"], new() { ["key"] = "C", ["variant"] = 1 });
        result.Chords[0].Should().Be("Em");
    }

    [Fact]
    public void Every_functional_group_clears_two_common_tones_in_C_major()
    {
        var scale = new[] { 0, 2, 4, 5, 7, 9, 11 };
        int[][] groups = [[0, 2, 5], [1, 3], [4, 6]];
        foreach (var group in groups)
        {
            foreach (var degree in group)
            {
                var tones = ScaleTriads.TriadPitchClasses(0, scale, degree);
                var others = group.Where(d => d != degree).Select(d => ScaleTriads.TriadPitchClasses(0, scale, d));
                others.Should().Contain(candidate => candidate.Count(tones.Contains) >= 2,
                    $"degree {degree} should share >=2 tones with at least one other member of its group");
            }
        }
    }

    [Fact]
    public void Out_of_key_chord_falls_back_to_D46_default()
    {
        var result = Apply(["Db"], new() { ["key"] = "C" });
        result.Chords[0].Should().Be("Db");
        result.Notes.Should().ContainSingle(n => n.Kind == "out-of-key");
    }
}
