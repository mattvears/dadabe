using Dadabe.Core.Chord;
using Dadabe.Core.Transform;

namespace Dadabe.Core;

/// <summary>
/// A progression transformation (D50): root-motion or grammar-mechanical
/// rewrites of a chord sequence. Parsing happens once at the chain's front
/// and emission once at its back — implementations work over
/// <see cref="ChordSymbol"/>, never re-parsing text mid-chain.
/// </summary>
public interface ITransformation
{
    /// <summary>Short machine-readable identifier, e.g. "transpose".</summary>
    string Id { get; }

    /// <summary>Human-readable display name shown in frontends.</summary>
    string DisplayName { get; }

    /// <summary>Whether <see cref="TransformResult.Invertible"/> is expected to be true for this transform's output.</summary>
    bool IsInvertible { get; }

    /// <summary>
    /// Applies the transform to a chord sequence. Chord-shape guards (D53)
    /// skip individual chords that do not qualify and report a
    /// <see cref="TransformNote"/> rather than throwing.
    /// </summary>
    TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters);
}
