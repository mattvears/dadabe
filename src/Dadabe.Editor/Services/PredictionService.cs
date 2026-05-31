using System.Text.Json.Serialization;

namespace Dadabe.Editor.Services;

public sealed record PredictionFilterModel(
    [property: JsonPropertyName("type")]   string Type,
    [property: JsonPropertyName("params")] Dictionary<string, string> Params);

public sealed record PredictionModel(
    [property: JsonPropertyName("slug")]       string Slug,
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("chord")]      string Chord,
    [property: JsonPropertyName("context")]    string? ContextSlug,
    [property: JsonPropertyName("filters")]    List<PredictionFilterModel> Filters,
    [property: JsonPropertyName("maxResults")] int? MaxResults,
    [property: JsonPropertyName("entropy")]    double? Entropy) : ISlugged;

public sealed class PredictionService(DataStore store)
{
    public IReadOnlyList<PredictionModel> ListAll() =>
        store.LoadAll<PredictionModel>(store.PredictionsDir);

    public PredictionModel? Get(string slug) =>
        store.Load<PredictionModel>(store.PredictionsDir, slug);

    public (bool ok, string error) Create(PredictionModel model)
    {
        var slug = DataStore.ToSlug(model.Name);
        if (string.IsNullOrEmpty(slug)) return (false, "Name is required.");
        if (store.Exists(store.PredictionsDir, slug))
            return (false, $"A prediction request named '{model.Name}' already exists.");
        store.Save(store.PredictionsDir, slug, model with { Slug = slug });
        return (true, string.Empty);
    }

    public (bool ok, string error) Update(string slug, PredictionModel model)
    {
        if (!store.Exists(store.PredictionsDir, slug))
            return (false, "Not found.");
        store.Save(store.PredictionsDir, slug, model with { Slug = slug });
        return (true, string.Empty);
    }

    public bool Delete(string slug) => store.Delete(store.PredictionsDir, slug);
}
