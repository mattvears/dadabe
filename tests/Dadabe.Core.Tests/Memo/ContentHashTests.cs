using Dadabe.Core.Memo;
using FluentAssertions;

namespace Dadabe.Core.Tests.Memo;

public class ContentHashTests
{
    [Fact]
    public void Parse_round_trips_ToString()
    {
        var original = ContentHash.FromCanonical("voicing", 1, new byte[] { 1, 2, 3, 4 });
        var s = original.ToString();
        var parsed = ContentHash.Parse(s);
        parsed.Should().Be(original);
    }

    [Fact]
    public void ToString_format_is_namespace_version_digest()
    {
        var hash = ContentHash.FromCanonical("tuning", 1, ReadOnlySpan<byte>.Empty);
        hash.ToString().Should().MatchRegex("^tuning:1:[0-9a-f]{32}$");
    }

    [Fact]
    public void Same_bytes_produce_same_digest()
    {
        var bytes = new byte[] { 0x42, 0x13, 0x37 };
        var a = ContentHash.FromCanonical("voicing", 1, bytes);
        var b = ContentHash.FromCanonical("voicing", 1, bytes);
        a.Should().Be(b);
    }

    [Fact]
    public void Different_bytes_produce_different_digest()
    {
        var a = ContentHash.FromCanonical("voicing", 1, new byte[] { 1 });
        var b = ContentHash.FromCanonical("voicing", 1, new byte[] { 2 });
        a.Digest.Should().NotBe(b.Digest);
    }

    [Fact]
    public void Namespace_separates_identical_digests()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var voicing = ContentHash.FromCanonical("voicing", 1, bytes);
        var fingering = ContentHash.FromCanonical("fingering", 1, bytes);
        voicing.Digest.Should().Be(fingering.Digest);
        voicing.Should().NotBe(fingering);
        voicing.ToString().Should().NotBe(fingering.ToString());
    }

    [Fact]
    public void Version_separates_identical_digests()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var v1 = ContentHash.FromCanonical("voicing", 1, bytes);
        var v2 = ContentHash.FromCanonical("voicing", 2, bytes);
        v1.Should().NotBe(v2);
    }

    [Theory]
    [InlineData("badformat")]
    [InlineData("ns:notanumber:0123456789abcdef0123456789abcdef")]
    [InlineData("ns:1:tooshort")]
    public void Parse_rejects_malformed(string input)
    {
        var act = () => ContentHash.Parse(input);
        act.Should().Throw<FormatException>();
    }
}
