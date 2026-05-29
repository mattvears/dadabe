using System.Diagnostics.CodeAnalysis;
using Dadabe.Core.Memo;
using FluentAssertions;

namespace Dadabe.Core.Tests.Memo;

/// <summary>
/// Asserts the IMemo contract invariants against a minimal mock persistent
/// backend (v0.3 scaffolding, D18).  Any future filesystem/SQLite backend
/// must satisfy all facts below.
/// </summary>
public class MemoBackendTests
{
    // Minimal hashable pair used as key/value throughout.
    private sealed record Payload(string Ns, byte[] Data) : IContentHashable
    {
        public ContentHash ContentHash =>
            Dadabe.Core.Memo.ContentHash.FromCanonical(Ns, 1, Data);
    }

    /// <summary>
    /// Minimal in-process persistent backend: stores entries in a dictionary
    /// that survives across TryGet/Put calls just like a real file store would.
    /// Replace with a real filesystem/SQLite implementation later.
    /// </summary>
    private sealed class PersistentMockMemo<TIn, TOut> : IMemo<TIn, TOut>
        where TIn : IContentHashable
        where TOut : IContentHashable
    {
        private readonly Dictionary<string, TOut> _store = new(StringComparer.Ordinal);

        public bool TryGet(TIn key, [NotNullWhen(true)] out TOut? value)
            => _store.TryGetValue(key.ContentHash.ToString(), out value);

        public void Put(TIn key, TOut value)
            => _store[key.ContentHash.ToString()] = value;

        public int Count => _store.Count;
    }

    [Fact]
    public void Mock_persistent_backend_satisfies_get_after_put()
    {
        IMemo<Payload, Payload> memo = new PersistentMockMemo<Payload, Payload>();
        var key = new Payload("key", new byte[] { 1, 2 });
        var val = new Payload("val", new byte[] { 9 });

        memo.Put(key, val);
        memo.TryGet(key, out var got).Should().BeTrue();
        got.Should().Be(val);
    }

    [Fact]
    public void Mock_persistent_backend_miss_returns_false()
    {
        IMemo<Payload, Payload> memo = new PersistentMockMemo<Payload, Payload>();
        var key = new Payload("key", new byte[] { 7 });
        memo.TryGet(key, out _).Should().BeFalse();
    }

    [Fact]
    public void InMemoryMemo_and_mock_persistent_backend_honor_same_contract()
    {
        // Both backends must behave identically for the same operations.
        IMemo<Payload, Payload> inMem = new InMemoryMemo<Payload, Payload>();
        IMemo<Payload, Payload> persistent = new PersistentMockMemo<Payload, Payload>();

        var key = new Payload("k", new byte[] { 0xAA });
        var val = new Payload("v", new byte[] { 0xBB });

        foreach (var memo in new[] { inMem, persistent })
        {
            memo.TryGet(key, out _).Should().BeFalse("miss before put");
            memo.Put(key, val);
            memo.TryGet(key, out var got).Should().BeTrue("hit after put");
            got!.ContentHash.Should().Be(val.ContentHash, "returned value identity preserved");
        }
    }

    [Fact]
    public void Content_hash_string_is_stable_memo_key()
    {
        // The hash string produced by the same Payload must be identical every
        // time — this is the invariant backends rely on for key lookup.
        var p = new Payload("ns", new byte[] { 1, 2, 3 });
        p.ContentHash.ToString().Should().Be(p.ContentHash.ToString());
    }
}
