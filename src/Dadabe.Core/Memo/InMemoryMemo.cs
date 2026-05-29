using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Dadabe.Core.Memo;

/// <summary>
/// In-process content-hash-keyed memo (D18). Thread-safe; the only memo
/// implementation in v0.1. Persistent backends are reserved for v0.2.
/// </summary>
public sealed class InMemoryMemo<TIn, TOut> : IMemo<TIn, TOut>
    where TIn : IContentHashable
    where TOut : IContentHashable
{
    private readonly ConcurrentDictionary<string, TOut> _store = new(StringComparer.Ordinal);

    public bool TryGet(TIn key, [NotNullWhen(true)] out TOut? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_store.TryGetValue(key.ContentHash.ToString(), out var stored))
        {
            value = stored!;
            return true;
        }
        value = default;
        return false;
    }

    public void Put(TIn key, TOut value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        _store[key.ContentHash.ToString()] = value;
    }

    public int Count => _store.Count;
}
