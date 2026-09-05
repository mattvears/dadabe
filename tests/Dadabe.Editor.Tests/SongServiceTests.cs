using Dadabe.Editor.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Dadabe.Editor.Tests;

public class SongServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly DataStore _store;
    private readonly SongService _songs;

    public SongServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "dadabe-song-test-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataRoot"] = _tempRoot })
            .Build();
        _store = new DataStore(config);
        _songs = new SongService(_store);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) { Directory.Delete(_tempRoot, recursive: true); }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Create_then_get_round_trips()
    {
        var (ok, error) = _songs.Create("My Song", "DADABE", key: null, tempo: 120);
        ok.Should().BeTrue(error);

        var song = _songs.Get("my-song");
        song.Should().NotBeNull();
        song!.Name.Should().Be("My Song");
        song.Tuning.Should().Be("DADABE");
        song.Tempo.Should().Be(120);
        song.Sections.Should().BeEmpty();
    }

    [Fact]
    public void Create_rejects_duplicate_slug()
    {
        _songs.Create("Dup", null, null, null);
        var (ok, error) = _songs.Create("Dup", null, null, null);
        ok.Should().BeFalse();
        error.Should().Contain("already exists");
    }

    [Fact]
    public void Update_changes_fields()
    {
        _songs.Create("Original", "DADABE", null, null);
        var (ok, _) = _songs.Update("original", "Renamed", "STANDARD", "A minor", 90, null);
        ok.Should().BeTrue();

        var song = _songs.Get("original");
        song!.Name.Should().Be("Renamed");
        song.Tuning.Should().Be("STANDARD");
        song.Key.Should().Be("A minor");
        song.Tempo.Should().Be(90);
    }

    [Fact]
    public void Delete_removes_the_song()
    {
        _songs.Create("Gone", null, null, null);
        _songs.Delete("gone").Should().BeTrue();
        _songs.Get("gone").Should().BeNull();
    }

    [Fact]
    public void AddSection_then_AppendSlots_produces_unpinned_slots()
    {
        _songs.Create("Verse Song", "DADABE", null, null);
        var (ok, _, section) = _songs.AddSection("verse-song", "Verse");
        ok.Should().BeTrue();

        _songs.AppendSlots("verse-song", section!.Id, ["Am7", "D7", "Gmaj7"]);

        var song = _songs.Get("verse-song")!;
        var slots = song.Sections.Single().Slots;
        slots.Should().HaveCount(3);
        slots.Should().OnlyContain(s => s.Voicing == null);
    }

    /// <summary>A slot with a null voicing must survive a save/load round trip (v0.5.2 §8 "the slot is the load-bearing shape").</summary>
    [Fact]
    public void Unpinned_slot_survives_save_and_load_round_trip()
    {
        _songs.Create("RT", null, null, null);
        var (_, _, section) = _songs.AddSection("rt", "A");
        _songs.AppendSlots("rt", section!.Id, ["Cmaj7"]);

        var reloaded = _songs.Get("rt")!;
        var slot = reloaded.Sections.Single().Slots.Single();
        slot.Symbol.Should().Be("Cmaj7");
        slot.Voicing.Should().BeNull();
        slot.PinnedUnder.Should().BeNull();
    }

    [Fact]
    public void PinSlot_fills_the_matching_unpinned_slot_rather_than_appending()
    {
        _songs.Create("Pin", null, null, null);
        var (_, _, section) = _songs.AddSection("pin", "A");
        _songs.AppendSlots("pin", section!.Id, ["Cmaj7"]);

        var voicing = new PinnedVoicing([new PinnedPosition(0, 3, false, false)], 90, "triad");
        var provenance = new PinProvenance(1, "DADABE", "hash-1");
        var (ok, error) = _songs.PinSlot("pin", section.Id, "Cmaj7", 1, voicing, provenance);

        ok.Should().BeTrue(error);
        var slots = _songs.Get("pin")!.Sections.Single().Slots;
        slots.Should().HaveCount(1, "pinning should fill the existing unpinned slot, not append a duplicate");
        // PinnedVoicing carries a List<T>, whose default equality inside a record
        // is reference-based — compare structurally instead.
        slots[0].Voicing.Should().BeEquivalentTo(voicing);
        slots[0].PinnedUnder.Should().Be(provenance);
    }

    [Fact]
    public void PinSlot_appends_when_no_unpinned_slot_matches()
    {
        _songs.Create("Append", null, null, null);
        var (_, _, section) = _songs.AddSection("append", "A");

        var voicing = new PinnedVoicing([new PinnedPosition(0, 3, false, false)], 90, "triad");
        var provenance = new PinProvenance(1, "DADABE", "hash-1");
        _songs.PinSlot("append", section!.Id, "Cmaj7", 1, voicing, provenance);

        _songs.Get("append")!.Sections.Single().Slots.Should().HaveCount(1);
    }

    /// <summary>Provenance {modelVersion, tuning, handHash} is recorded on pin (D43).</summary>
    [Fact]
    public void Pin_records_provenance()
    {
        _songs.Create("Prov", "DADABE", null, null);
        var (_, _, section) = _songs.AddSection("prov", "A");
        var voicing = new PinnedVoicing([new PinnedPosition(0, 0, false, true)], 100, "triad");
        var provenance = new PinProvenance(1, "DADABE", "abc123");

        _songs.PinSlot("prov", section!.Id, "C", 2, voicing, provenance);

        var slot = _songs.Get("prov")!.Sections.Single().Slots.Single();
        slot.Bars.Should().Be(2);
        slot.PinnedUnder.Should().NotBeNull();
        slot.PinnedUnder!.ModelVersion.Should().Be(1);
        slot.PinnedUnder.Tuning.Should().Be("DADABE");
        slot.PinnedUnder.HandHash.Should().Be("abc123");
    }

    [Fact]
    public void RemoveSlot_and_DeleteSection_work()
    {
        _songs.Create("Remove", null, null, null);
        var (_, _, section) = _songs.AddSection("remove", "A");
        _songs.AppendSlots("remove", section!.Id, ["C", "F"]);

        _songs.RemoveSlot("remove", section.Id, 0).ok.Should().BeTrue();
        _songs.Get("remove")!.Sections.Single().Slots.Should().ContainSingle(s => s.Symbol == "F");

        _songs.DeleteSection("remove", section.Id).ok.Should().BeTrue();
        _songs.Get("remove")!.Sections.Should().BeEmpty();
    }
}
