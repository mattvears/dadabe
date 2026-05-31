using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dadabe.Editor.Services;

public sealed record CadenceModel(
    [property: JsonPropertyName("slug")]               string Slug,
    [property: JsonPropertyName("type")]               string Type,
    [property: JsonPropertyName("resolution")]         string? Resolution,
    [property: JsonPropertyName("exampleProgression")] List<string> ExampleProgression,
    [property: JsonPropertyName("description")]        string? Description) : ISlugged;

public sealed record ModeModel(
    [property: JsonPropertyName("slug")]        string Slug,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("intervals")]   List<int> Intervals,
    [property: JsonPropertyName("parentScale")] string? ParentScale,
    [property: JsonPropertyName("degreeIndex")] int? DegreeIndex,
    [property: JsonPropertyName("noteNames")]   List<string>? NoteNames,
    [property: JsonPropertyName("description")] string? Description) : ISlugged;

public sealed record ScaleModel(
    [property: JsonPropertyName("slug")]        string Slug,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("notes")]       List<string> Notes,
    [property: JsonPropertyName("intervals")]   List<int> Intervals,
    [property: JsonPropertyName("modeOf")]      string? ModeOf,
    [property: JsonPropertyName("description")] string? Description) : ISlugged;

public sealed class ReferenceService(DataStore store)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Cadences
    public IReadOnlyList<CadenceModel> ListCadences() =>
        store.LoadAll<CadenceModel>(store.CadencesDir);
    public CadenceModel? GetCadence(string slug) =>
        store.Load<CadenceModel>(store.CadencesDir, slug);
    public (bool ok, string error) SaveCadence(CadenceModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Slug))
            return (false, "Slug is required.");
        store.Save(store.CadencesDir, model.Slug, model);
        return (true, string.Empty);
    }
    public bool DeleteCadence(string slug) => store.Delete(store.CadencesDir, slug);

    // Modes
    public IReadOnlyList<ModeModel> ListModes() =>
        store.LoadAll<ModeModel>(store.ModesDir);
    public ModeModel? GetMode(string slug) =>
        store.Load<ModeModel>(store.ModesDir, slug);
    public (bool ok, string error) SaveMode(ModeModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Slug))
            return (false, "Slug is required.");
        store.Save(store.ModesDir, model.Slug, model);
        return (true, string.Empty);
    }
    public bool DeleteMode(string slug) => store.Delete(store.ModesDir, slug);

    // Scales
    public IReadOnlyList<ScaleModel> ListScales() =>
        store.LoadAll<ScaleModel>(store.ScalesDir);
    public ScaleModel? GetScale(string slug) =>
        store.Load<ScaleModel>(store.ScalesDir, slug);
    public (bool ok, string error) SaveScale(ScaleModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Slug))
            return (false, "Slug is required.");
        store.Save(store.ScalesDir, model.Slug, model);
        return (true, string.Empty);
    }
    public bool DeleteScale(string slug) => store.Delete(store.ScalesDir, slug);

    // Voicing category overlay (raw JSON)
    public string? GetVoicingCategories()
    {
        var path = store.VoicingCategoriesFile;
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
    public (bool ok, string error) SaveVoicingCategories(string json)
    {
        try
        {
            JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return (false, $"Invalid JSON: {ex.Message}");
        }
        File.WriteAllText(store.VoicingCategoriesFile, json);
        return (true, string.Empty);
    }
}
