using System.Diagnostics.CodeAnalysis;

namespace Dadabe.Core.Memo;

/// <summary>
/// A cache keyed on content hashes (D18). A memo is *sound* iff it never
/// returns a value that disagrees with the underlying function — every
/// memoized pipeline must produce byte-identical output with or without it.
/// </summary>
public interface IMemo<TIn, TOut>
    where TIn : IContentHashable
    where TOut : IContentHashable
{
    bool TryGet(TIn key, [NotNullWhen(true)] out TOut? value);

    void Put(TIn key, TOut value);
}
