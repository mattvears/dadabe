using System.Text.Json.Serialization;

namespace Dadabe.Editor.Services;

public sealed record TuningModel(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("strings")] List<string> Strings) : ISlugged;

public sealed class TuningService(DataStore store)
{
    public IReadOnlyList<TuningModel> ListAll() =>
        store.LoadAll<TuningModel>(store.TuningsDir);

    public TuningModel? Get(string slug) =>
        store.Load<TuningModel>(store.TuningsDir, slug);

    public (bool ok, string error) Create(string name, List<string> strings)
    {
        var slug = DataStore.ToSlug(name);
        if (string.IsNullOrEmpty(slug)) return (false, "Name is required.");
        if (store.Exists(store.TuningsDir, slug))
            return (false, $"A tuning named '{name}' already exists.");
        store.Save(store.TuningsDir, slug, new TuningModel(slug, name, strings));
        return (true, string.Empty);
    }

    public (bool ok, string error) Update(string slug, string name, List<string> strings)
    {
        if (!store.Exists(store.TuningsDir, slug))
            return (false, "Not found.");
        store.Save(store.TuningsDir, slug, new TuningModel(slug, name, strings));
        return (true, string.Empty);
    }

    public bool Delete(string slug) => store.Delete(store.TuningsDir, slug);
}
