using Dadabe.Core.Chord;
using Dadabe.Core.Transform;
using FluentAssertions;

namespace Dadabe.Core.Tests.Transform;

public class TransformChainTests
{
    private static readonly ChordGrammar Grammar = ChordGrammarLoader.Load(workingDirectory: null);
    private static readonly ChordParser Parser = new(Grammar);
    private static readonly TransformCatalog Catalog = TransformCatalog.CreateDefault(Grammar);

    [Fact]
    public void Retrograde_then_transpose_composes_left_to_right()
    {
        var steps = new List<TransformStep>
        {
            new("retrograde", new Dictionary<string, object>()),
            new("transpose", new Dictionary<string, object> { ["interval"] = "M2" }),
        };

        var result = TransformChain.Apply(Catalog, Parser, ["C", "F", "G"], steps);

        result.Chords.Should().Equal("A", "G", "D");
    }
}
