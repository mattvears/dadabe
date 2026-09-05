using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

public class NegativeHarmonyTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly TransformCatalog Catalog = TransformCatalog.CreateDefault(Grammar);

    private static TransformResult Apply(string[] chords, Dictionary<string, object> parameters) =>
        Catalog["negative-harmony"].Apply(chords.Select(Parser.Parse).ToArray(), parameters);

    [Fact]
    public void Reproduces_the_design_docs_worked_example()
    {
        var result = Apply(["C", "F", "G"], new() { ["key"] = "C" });
        result.Chords.Should().Equal("Cm", "Gm", "Fm");
    }

    [Fact]
    public void Explicit_axis_bypasses_key_inference_entirely()
    {
        // No "key" param and an empty context (which would otherwise trigger needs-key) — axis alone is enough.
        var result = Apply(["C", "F", "G"], new() { ["axis"] = "C" });
        result.Chords.Should().Equal("Cm", "Gm", "Fm");
        result.Notes.Should().NotContain(n => n.Kind == "needs-key");
    }

    [Fact]
    public void Missing_key_and_axis_produces_the_uniform_needs_key_note()
    {
        var result = Apply([], new());
        result.Notes.Should().ContainSingle(n => n.Kind == "needs-key");
    }

    [Fact]
    public void Applying_twice_returns_to_the_original()
    {
        var once = Apply(["C", "F", "G"], new() { ["axis"] = "C" });
        var onceChords = once.Chords.Select(Parser.Parse).ToArray();
        var twice = Catalog["negative-harmony"].Apply(onceChords, new Dictionary<string, object> { ["axis"] = "C" });
        twice.Chords.Should().Equal("C", "F", "G");
    }

    [Fact]
    public void Dim_chord_has_no_defined_flip_even_in_loose_mode()
    {
        var result = Apply(["Cdim"], new() { ["axis"] = "C", ["strictness"] = "loose" });
        result.Chords[0].Should().Be("Cdim");
        result.Notes.Should().ContainSingle(n => n.Kind == "skipped");
    }

    [Fact]
    public void Bass_is_dropped_and_noted()
    {
        var result = Apply(["C/E"], new() { ["axis"] = "C" });
        result.Notes.Should().ContainSingle(n => n.Kind == "bass-dropped");
    }
}
