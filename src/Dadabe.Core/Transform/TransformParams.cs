using System.Globalization;

namespace Dadabe.Core.Transform;

/// <summary>
/// Reads typed values out of a transform's opaque parameter bag. Values may
/// arrive boxed as native CLR types (from code) or as strings (from CLI
/// <c>--chain</c> parsing) — both are accepted.
/// </summary>
internal static class TransformParams
{
    public static string? GetString(IReadOnlyDictionary<string, object> parameters, string key) =>
        parameters.TryGetValue(key, out var value) ? value?.ToString() : null;

    public static int? GetInt(IReadOnlyDictionary<string, object> parameters, string key)
    {
        var raw = GetString(parameters, key);
        return raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    public static int GetInt(IReadOnlyDictionary<string, object> parameters, string key, int fallback) =>
        GetInt(parameters, key) ?? fallback;

    /// <summary>
    /// Parses the "positions" param shared by every position-selectable
    /// transform: a comma-separated index list, or every index 0..count-1
    /// when absent/empty.
    /// </summary>
    public static HashSet<int> GetPositions(IReadOnlyDictionary<string, object> parameters, int count)
    {
        var raw = GetString(parameters, "positions");
        if (string.IsNullOrEmpty(raw))
        {
            return [.. Enumerable.Range(0, count)];
        }
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToHashSet();
    }
}
