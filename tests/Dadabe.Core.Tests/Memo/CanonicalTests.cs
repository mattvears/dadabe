using Dadabe.Core.Memo;
using FluentAssertions;

namespace Dadabe.Core.Tests.Memo;

public class CanonicalTests
{
    [Fact]
    public void U8_writes_one_byte()
    {
        var c = new Canonical().U8(0xAB);
        c.ToArray().Should().Equal(0xAB);
    }

    [Fact]
    public void U16BE_writes_big_endian()
    {
        var c = new Canonical().U16BE(0x1234);
        c.ToArray().Should().Equal(0x12, 0x34);
    }

    [Fact]
    public void I16BE_writes_big_endian_signed()
    {
        var c = new Canonical().I16BE(-1);
        c.ToArray().Should().Equal(0xFF, 0xFF);
    }

    [Fact]
    public void Utf8_writes_length_then_bytes()
    {
        var c = new Canonical().Utf8("Cmaj7");
        c.ToArray().Should().Equal(0x00, 0x05, (byte)'C', (byte)'m', (byte)'a', (byte)'j', (byte)'7');
    }

    [Fact]
    public void Sequential_writes_concatenate()
    {
        var c = new Canonical().U8(0x01).U16BE(0x0203).Utf8("A");
        c.ToArray().Should().Equal(0x01, 0x02, 0x03, 0x00, 0x01, (byte)'A');
    }

    [Fact]
    public void HashDigest_writes_16_raw_bytes()
    {
        var hash = ContentHash.FromCanonical("ns", 1, new byte[] { 1, 2, 3 });
        var c = new Canonical().HashDigest(hash);
        c.ToArray().Should().HaveCount(16);
    }
}
