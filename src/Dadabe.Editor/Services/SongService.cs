using System.Text.Json.Serialization;

namespace Dadabe.Editor.Services;

/// <param name="Tuning">Catalog name or comma spec (D38). Null before the user has set one.</param>
/// <param name="Key">User override; key inference is display-only (D39).</param>
/// <param name="Hand">Song-level hand constraints (D42). Null fields fall back to <c>HandModel.Default</c>.</param>
public sealed record SongModel(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("tuning")] string? Tuning,
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("tempo")] int? Tempo,
    [property: JsonPropertyName("hand")] SongHand? Hand,
    [property: JsonPropertyName("sections")] List<SongSection> Sections) : ISlugged;

public sealed record SongSection(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slots")] List<SongSlot> Slots);

/// <param name="Voicing">Pinned shape, or null for a chord whose fingering is unchosen.</param>
public sealed record SongSlot(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("bars")] int Bars,
    [property: JsonPropertyName("voicing")] PinnedVoicing? Voicing,
    [property: JsonPropertyName("pinnedUnder")] PinProvenance? PinnedUnder);

/// <summary>
/// Positions, not a search-result index — result ordering is not stable
/// across model versions or search parameters.
/// </summary>
public sealed record PinnedVoicing(
    [property: JsonPropertyName("positions")] List<PinnedPosition> Positions,
    [property: JsonPropertyName("comfortPct")] int ComfortPct,
    [property: JsonPropertyName("structure")] string Structure);

public sealed record PinnedPosition(
    [property: JsonPropertyName("string")] int String,
    [property: JsonPropertyName("fret")] int? Fret,
    [property: JsonPropertyName("muted")] bool Muted,
    [property: JsonPropertyName("open")] bool Open);

public sealed record SongHand(
    [property: JsonPropertyName("frets")] int? Frets,
    [property: JsonPropertyName("span")] int? Span,
    [property: JsonPropertyName("minStrings")] int? MinStrings,
    [property: JsonPropertyName("maxStrings")] int? MaxStrings,
    [property: JsonPropertyName("allowOpen")] bool? AllowOpen,
    [property: JsonPropertyName("allowBarre")] bool? AllowBarre,
    [property: JsonPropertyName("allowThumb")] bool? AllowThumb,
    [property: JsonPropertyName("categories")] List<string>? Categories,
    [property: JsonPropertyName("requireRoot")] bool? RequireRoot = null);

/// <summary>
/// Records the conditions a pin was scored under (D43). Positions stay
/// playable forever, but the comfort *number* was computed under a
/// particular comfort model and hand — when provenance no longer matches
/// current, the slot is badged stale and offered a one-click re-score.
/// </summary>
public sealed record PinProvenance(
    [property: JsonPropertyName("modelVersion")] int ModelVersion,
    [property: JsonPropertyName("tuning")] string Tuning,
    [property: JsonPropertyName("handHash")] string HandHash);

public sealed class SongService(DataStore store)
{
    public IReadOnlyList<SongModel> ListAll() =>
        store.LoadAll<SongModel>(store.SongsDir);

    public SongModel? Get(string slug) =>
        store.Load<SongModel>(store.SongsDir, slug);

    public (bool ok, string error) Create(string name, string? tuning, string? key, int? tempo)
    {
        var slug = DataStore.ToSlug(name);
        if (string.IsNullOrEmpty(slug)) return (false, "Name is required.");
        if (store.Exists(store.SongsDir, slug))
            return (false, $"A song named '{name}' already exists.");
        store.Save(store.SongsDir, slug, new SongModel(slug, name, tuning, key, tempo, Hand: null, Sections: []));
        return (true, string.Empty);
    }

    public (bool ok, string error) Update(string slug, string name, string? tuning, string? key, int? tempo, SongHand? hand)
    {
        var existing = Get(slug);
        if (existing is null) return (false, "Not found.");
        store.Save(store.SongsDir, slug, existing with { Name = name, Tuning = tuning, Key = key, Tempo = tempo, Hand = hand });
        return (true, string.Empty);
    }

    public bool Delete(string slug) => store.Delete(store.SongsDir, slug);

    /// <summary>Writes an imported song as-is (§12 export/import) — overwrites on slug collision.</summary>
    public void SaveImported(SongModel song) => store.Save(store.SongsDir, song.Slug, song);

    public (bool ok, string error, SongSection? section) AddSection(string slug, string name)
    {
        var song = Get(slug);
        if (song is null) return (false, "Song not found.", null);
        if (string.IsNullOrWhiteSpace(name)) return (false, "Section name is required.", null);

        var section = new SongSection(Guid.NewGuid().ToString("N")[..8], name.Trim(), []);
        song.Sections.Add(section);
        store.Save(store.SongsDir, slug, song);
        return (true, string.Empty, section);
    }

    public (bool ok, string error) DeleteSection(string slug, string sectionId)
    {
        var song = Get(slug);
        if (song is null) return (false, "Song not found.");
        var removed = song.Sections.RemoveAll(s => s.Id == sectionId) > 0;
        if (!removed) return (false, "Section not found.");
        store.Save(store.SongsDir, slug, song);
        return (true, string.Empty);
    }

    /// <summary>
    /// Appends unpinned slots parsed from a pasted chart (D57 §12 "paste a
    /// chart into a section") — same whitespace-split path the voice-lead
    /// form uses.
    /// </summary>
    public (bool ok, string error) AppendSlots(string slug, string sectionId, IEnumerable<string> symbols, int barsPerSlot = 1)
    {
        var (song, section) = FindSection(slug, sectionId);
        if (song is null || section is null) return (false, "Song or section not found.");
        foreach (var symbol in symbols)
        {
            section.Slots.Add(new SongSlot(symbol, barsPerSlot, Voicing: null, PinnedUnder: null));
        }
        store.Save(store.SongsDir, slug, song);
        return (true, string.Empty);
    }

    /// <summary>
    /// Pins a voicing to a chord in a section (D37 §5.2). If the section
    /// already has an unpinned slot for this symbol, that slot is filled;
    /// otherwise a new slot is appended. When <paramref name="slotIndex"/>
    /// is given, that slot's voicing is replaced outright — this is how a
    /// user swaps a shape they have changed their mind about.
    /// </summary>
    public (bool ok, string error) PinSlot(
        string slug, string sectionId, string symbol, int bars,
        PinnedVoicing voicing, PinProvenance provenance, int? slotIndex = null)
    {
        var (song, section) = FindSection(slug, sectionId);
        if (song is null || section is null) return (false, "Song or section not found.");

        if (slotIndex is { } idx)
        {
            if (idx < 0 || idx >= section.Slots.Count) return (false, "Slot index out of range.");
            section.Slots[idx] = section.Slots[idx] with { Voicing = voicing, PinnedUnder = provenance };
        }
        else
        {
            var unpinnedIdx = section.Slots.FindIndex(s => s.Symbol == symbol && s.Voicing is null);
            if (unpinnedIdx >= 0)
            {
                section.Slots[unpinnedIdx] = section.Slots[unpinnedIdx] with { Voicing = voicing, PinnedUnder = provenance };
            }
            else
            {
                section.Slots.Add(new SongSlot(symbol, bars, voicing, provenance));
            }
        }

        store.Save(store.SongsDir, slug, song);
        return (true, string.Empty);
    }

    public (bool ok, string error) RemoveSlot(string slug, string sectionId, int slotIndex)
    {
        var (song, section) = FindSection(slug, sectionId);
        if (song is null || section is null) return (false, "Song or section not found.");
        if (slotIndex < 0 || slotIndex >= section.Slots.Count) return (false, "Slot index out of range.");
        section.Slots.RemoveAt(slotIndex);
        store.Save(store.SongsDir, slug, song);
        return (true, string.Empty);
    }

    /// <summary>Replaces a section's slots wholesale — used by fill (§9) and by promote/instantiate (§12).</summary>
    public (bool ok, string error) ReplaceSlots(string slug, string sectionId, List<SongSlot> slots)
    {
        var (song, section) = FindSection(slug, sectionId);
        if (song is null || section is null) return (false, "Song or section not found.");
        section.Slots.Clear();
        section.Slots.AddRange(slots);
        store.Save(store.SongsDir, slug, song);
        return (true, string.Empty);
    }

    public (SongModel? Song, SongSection? Section) FindSection(string slug, string sectionId)
    {
        var song = Get(slug);
        var section = song?.Sections.FirstOrDefault(s => s.Id == sectionId);
        return (song, section);
    }
}
