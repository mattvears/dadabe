using System.Globalization;
using System.Security.Cryptography;

namespace Dadabe.Core.Memo;

/// <summary>
/// Stable identity for a content-addressable domain value (D17).
/// Format: <c>namespace:version:digest</c> where digest is the first 128 bits
/// of SHA-256 over the value's canonical bytes, lowercase hex (32 chars).
/// </summary>
public readonly record struct ContentHash(string Namespace, int Version, string Digest)
{
    private const int DigestHexLength = 32;

    public override string ToString() => $"{Namespace}:{Version}:{Digest}";

    /// <summary>Round-trip parse of <see cref="ToString"/>.</summary>
    public static ContentHash Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var parts = s.Split(':');
        if (parts.Length != 3)
        {
            throw new FormatException($"Expected 'namespace:version:digest', got '{s}'.");
        }
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version))
        {
            throw new FormatException($"Version '{parts[1]}' is not an integer.");
        }
        if (parts[2].Length != DigestHexLength)
        {
            throw new FormatException($"Digest must be {DigestHexLength} hex chars, got '{parts[2]}'.");
        }
        return new ContentHash(parts[0], version, parts[2]);
    }

    /// <summary>
    /// Build a hash by SHA-256'ing <paramref name="bytes"/> and truncating to
    /// 128 bits, rendered as lowercase hex.
    /// </summary>
    public static ContentHash FromCanonical(string @namespace, int version, ReadOnlySpan<byte> bytes)
    {
        Span<byte> full = stackalloc byte[32];
        SHA256.HashData(bytes, full);
        return new ContentHash(@namespace, version, ToHexLowercase(full[..16]));
    }

    private static string ToHexLowercase(ReadOnlySpan<byte> bytes)
    {
        const string HexChars = "0123456789abcdef";
        Span<char> chars = stackalloc char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[(i * 2) + 0] = HexChars[bytes[i] >> 4];
            chars[(i * 2) + 1] = HexChars[bytes[i] & 0xF];
        }
        return new string(chars);
    }
}
