using Dadabe.Core.Memo;
using FluentAssertions;

namespace Dadabe.Core.Tests.Memo;

public class InMemoryMemoTests
{
    private sealed record Bytes(string Namespace, byte[] Payload) : IContentHashable
    {
        public ContentHash ContentHash => Dadabe.Core.Memo.ContentHash.FromCanonical(Namespace, 1, Payload);
    }

    [Fact]
    public void Get_after_Put_returns_value()
    {
        var memo = new InMemoryMemo<Bytes, Bytes>();
        var key = new Bytes("k", new byte[] { 1 });
        var value = new Bytes("v", new byte[] { 9, 9 });

        memo.Put(key, value);

        memo.TryGet(key, out var got).Should().BeTrue();
        got.Should().Be(value);
    }

    [Fact]
    public void Get_with_no_Put_returns_false()
    {
        var memo = new InMemoryMemo<Bytes, Bytes>();
        var key = new Bytes("k", new byte[] { 1 });
        memo.TryGet(key, out _).Should().BeFalse();
    }

    [Fact]
    public void Equivalent_keys_share_an_entry()
    {
        var memo = new InMemoryMemo<Bytes, Bytes>();
        var key1 = new Bytes("k", new byte[] { 1, 2, 3 });
        var key2 = new Bytes("k", new byte[] { 1, 2, 3 });
        var value = new Bytes("v", new byte[] { 0xFF });

        memo.Put(key1, value);
        memo.TryGet(key2, out var got).Should().BeTrue();
        got.Should().Be(value);
        memo.Count.Should().Be(1);
    }

    [Fact]
    public void Concurrent_Put_on_same_key_is_thread_safe()
    {
        var memo = new InMemoryMemo<Bytes, Bytes>();
        var key = new Bytes("k", new byte[] { 1 });
        var values = Enumerable.Range(0, 100)
            .Select(i => new Bytes("v", new[] { (byte)i }))
            .ToArray();

        Parallel.ForEach(values, v => memo.Put(key, v));

        memo.TryGet(key, out var got).Should().BeTrue();
        values.Should().Contain(got!);
        memo.Count.Should().Be(1);
    }
}
