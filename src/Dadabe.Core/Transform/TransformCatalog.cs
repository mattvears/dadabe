using System.Collections;
using Dadabe.Core.Chord;

namespace Dadabe.Core.Transform;

/// <summary>
/// Registry of every <see cref="ITransformation"/>, keyed by <see cref="ITransformation.Id"/>.
/// Enumerable so "apply-all" (D51 §5) can run the whole catalogue.
/// </summary>
public sealed class TransformCatalog : IEnumerable<ITransformation>
{
    private readonly Dictionary<string, ITransformation> _byId;

    public TransformCatalog(IEnumerable<ITransformation> transformations)
    {
        _byId = transformations.ToDictionary(t => t.Id, StringComparer.Ordinal);
    }

    public static TransformCatalog CreateDefault(ChordGrammar grammar) => new(DefaultTransformations(grammar));

    public static IEnumerable<ITransformation> DefaultTransformations(ChordGrammar grammar) =>
    [
        new TransposeTransform(),
        new RetrogradeTransform(),
        new RotateTransform(),
        new InvertTransform(),
        new IntervalNegateTransform(),
        new IntervalReverseTransform(),
        new IntervalMultiplyTransform(),
        new QualityMapTransform(grammar),
        new ReduceTransform(),
        new TritoneSubTransform(grammar),
        new PlrTransform(),
    ];

    public int Count => _byId.Count;

    public ITransformation this[string id] => TryGet(id, out var t)
        ? t
        : throw new KeyNotFoundException($"No transform registered with type '{id}'.");

    public bool TryGet(string id, out ITransformation transformation) => _byId.TryGetValue(id, out transformation!);

    public IEnumerator<ITransformation> GetEnumerator() => _byId.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Applies a <see cref="TransformStep"/> chain left-to-right in a single
/// request: parse once at the front, emit once at the back (D50). Chords
/// re-parsed between steps stay <see cref="ChordSymbol"/>-typed internally.
/// </summary>
public static class TransformChain
{
    public static TransformResult Apply(
        TransformCatalog catalog,
        ChordParser parser,
        IReadOnlyList<string> chords,
        IReadOnlyList<TransformStep> steps)
    {
        var current = chords.Select(parser.Parse).ToArray();
        var notes = new List<TransformNote>();
        var invertible = true;

        foreach (var step in steps)
        {
            var transform = catalog[step.Type];
            var result = transform.Apply(current, step.Params);
            notes.AddRange(result.Notes);
            invertible &= result.Invertible;
            current = result.Chords.Select(parser.Parse).ToArray();
        }

        return new TransformResult(current.Select(c => c.ToSymbol()).ToArray(), notes, invertible);
    }
}
