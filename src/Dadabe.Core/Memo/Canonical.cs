using System.Buffers.Binary;
using System.Text;

namespace Dadabe.Core.Memo;

/// <summary>
/// Deterministic byte-serialization primitives for content hashing (D17).
/// Each domain type owns its layout; this class only provides the low-level
/// building blocks (big-endian fixed-width ints, length-prefixed strings/lists).
/// </summary>
public sealed class Canonical
{
    private readonly List<byte> _buffer = new();

    public int Length => _buffer.Count;

    public byte[] ToArray() => _buffer.ToArray();

    public ReadOnlySpan<byte> AsSpan() => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_buffer);

    public Canonical U8(byte value)
    {
        _buffer.Add(value);
        return this;
    }

    public Canonical I8(sbyte value)
    {
        _buffer.Add((byte)value);
        return this;
    }

    public Canonical U16BE(ushort value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buf, value);
        _buffer.AddRange(buf);
        return this;
    }

    public Canonical I16BE(short value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buf, value);
        _buffer.AddRange(buf);
        return this;
    }

    public Canonical I32BE(int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, value);
        _buffer.AddRange(buf);
        return this;
    }

    /// <summary>Length-prefixed (u16 BE) UTF-8 string.</summary>
    public Canonical Utf8(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new ArgumentException("String exceeds 65535 UTF-8 bytes.", nameof(value));
        }
        U16BE((ushort)bytes.Length);
        _buffer.AddRange(bytes);
        return this;
    }

    public Canonical Bytes(ReadOnlySpan<byte> value)
    {
        _buffer.AddRange(value);
        return this;
    }

    /// <summary>16-byte raw digest of another value's content hash, used as a child reference.</summary>
    public Canonical HashDigest(ContentHash hash)
    {
        if (hash.Digest.Length != 32)
        {
            throw new ArgumentException("Digest must be 32 hex chars.", nameof(hash));
        }
        Span<byte> bytes = stackalloc byte[16];
        for (var i = 0; i < 16; i++)
        {
            bytes[i] = (byte)((HexNibble(hash.Digest[i * 2]) << 4) | HexNibble(hash.Digest[(i * 2) + 1]));
        }
        _buffer.AddRange(bytes);
        return this;
    }

    private static int HexNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => throw new FormatException($"Not a hex digit: '{c}'."),
    };
}
