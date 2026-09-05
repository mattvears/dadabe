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
        new DiatonicTransposeTransform(grammar),
        new ParallelModeTransform(grammar),
        new SubstituteTransform(grammar),
        new NegativeHarmonyTransform(grammar),
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
        IReadOnlyList<TransformStep> steps,
        TransformRequestOptions? options = null)
    {
        var opts = options ?? TransformRequestOptions.Default;
        var current = chords.Select(parser.Parse).ToArray();
        var notes = new List<TransformNote>();
        var invertible = true;

        foreach (var step in steps)
        {
            var transform = catalog[step.Type];
            var result = transform.Apply(current, MergeRequestOptions(step.Params, opts));
            notes.AddRange(result.Notes);
            invertible &= result.Invertible;
            current = result.Chords.Select(parser.Parse).ToArray();
        }

        return new TransformResult(current.Select(c => c.ToSymbol()).ToArray(), notes, invertible);
    }

    /// <summary>
    /// Request-level "strictness"/"key" always win over anything already in
    /// step.Params for those two reserved keys — D54 rejected a per-step
    /// policy specifically so it can't be overridden per step. Key-blind
    /// transforms never read either key and are unaffected.
    /// </summary>
    private static Dictionary<string, object> MergeRequestOptions(
        IReadOnlyDictionary<string, object> stepParams, TransformRequestOptions opts)
    {
        var merged = new Dictionary<string, object>(stepParams, StringComparer.Ordinal)
        {
            ["strictness"] = opts.Strictness == Strictness.Strict ? "strict" : "loose",
        };
        if (opts.KeyOverride is { } key)
        {
            merged["key"] = KeyAwareTransform.FormatKey(key.RootPc, key.IsMinor);
        }
        return merged;
    }
}
