using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dadabe.Editor.Services;

public sealed class DataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Root { get; }

    public DataStore(IConfiguration config)
    {
        Root = config["DataRoot"] ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        EnsureDirectories();
    }

    private void EnsureDirectories()
    {
        foreach (var dir in new[]
        {
            ProgressionsDir, TuningsDir, PredictionsDir,
            CadencesDir, ModesDir, ScalesDir,
            Path.Combine(Root, "reference"),
        })
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string ProgressionsDir => Path.Combine(Root, "progressions");
    public string TuningsDir      => Path.Combine(Root, "tunings");
    public string PredictionsDir  => Path.Combine(Root, "predictions");
    public string CadencesDir     => Path.Combine(Root, "reference", "cadences");
    public string ModesDir        => Path.Combine(Root, "reference", "modes");
    public string ScalesDir       => Path.Combine(Root, "reference", "scales");
    public string VoicingCategoriesFile => Path.Combine(Root, "reference", "voicing-categories.json");

    public IReadOnlyList<T> LoadAll<T>(string dir) where T : ISlugged
    {
        if (!Directory.Exists(dir))
            return [];

        return Directory.GetFiles(dir, "*.json")
            .Select(f => JsonSerializer.Deserialize<T>(File.ReadAllText(f), JsonOptions)!)
            .OrderBy(x => x.Slug)
            .ToList();
    }

    public T? Load<T>(string dir, string slug)
    {
        var path = Path.Combine(dir, $"{slug}.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
            : default;
    }

    public void Save<T>(string dir, string slug, T item)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{slug}.json"),
            JsonSerializer.Serialize(item, JsonOptions));
    }

    public bool Delete(string dir, string slug)
    {
        var path = Path.Combine(dir, $"{slug}.json");
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public bool Exists(string dir, string slug) =>
        File.Exists(Path.Combine(dir, $"{slug}.json"));

    public static string ToSlug(string name) =>
        System.Text.RegularExpressions.Regex.Replace(
            name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
}

public interface ISlugged
{
    string Slug { get; }
}
