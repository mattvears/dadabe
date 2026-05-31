using System.Text.Json.Serialization;

namespace Dadabe.Editor.Services;

public sealed record ProgressionModel(
    [property: JsonPropertyName("slug")]   string Slug,
    [property: JsonPropertyName("name")]   string Name,
    [property: JsonPropertyName("chords")] List<string> Chords,
    [property: JsonPropertyName("tempo")]  int? Tempo) : ISlugged;

public sealed class ProgressionService(DataStore store)
{
    public IReadOnlyList<ProgressionModel> ListAll() =>
        store.LoadAll<ProgressionModel>(store.ProgressionsDir);

    public ProgressionModel? Get(string slug) =>
        store.Load<ProgressionModel>(store.ProgressionsDir, slug);

    public (bool ok, string error) Create(string name, List<string> chords, int? tempo)
    {
        var slug = DataStore.ToSlug(name);
        if (string.IsNullOrEmpty(slug)) return (false, "Name is required.");
        if (store.Exists(store.ProgressionsDir, slug))
            return (false, $"A progression named '{name}' already exists.");
        store.Save(store.ProgressionsDir, slug, new ProgressionModel(slug, name, chords, tempo));
        return (true, string.Empty);
    }

    public (bool ok, string error) Update(string slug, string name, List<string> chords, int? tempo)
    {
        if (!store.Exists(store.ProgressionsDir, slug))
            return (false, "Not found.");
        store.Save(store.ProgressionsDir, slug, new ProgressionModel(slug, name, chords, tempo));
        return (true, string.Empty);
    }

    public bool Delete(string slug) => store.Delete(store.ProgressionsDir, slug);
}
