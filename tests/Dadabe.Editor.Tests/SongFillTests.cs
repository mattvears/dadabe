using Dadabe.Core;
using Dadabe.Core.Chord;
using Dadabe.Editor.Routes;
using Dadabe.Editor.Services;
using Dadabe.Fretboard;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Dadabe.Editor.Tests;

/// <summary>
/// A section with two pinned and two unpinned slots fills only the unpinned
/// ones and leaves the pinned positions byte-identical (v0.5.2 §9).
/// </summary>
public class SongFillTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly SongService _songs;
    private readonly Catalogs _catalogs = Catalogs.Default();
    private readonly ChordParser _parser;
    private readonly ChordExpander _expander;

    public SongFillTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "dadabe-song-fill-test-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataRoot"] = _tempRoot })
            .Build();
        _songs = new SongService(new DataStore(config));
        _parser = new ChordParser(_catalogs.ChordGrammar);
        _expander = new ChordExpander(_catalogs.ChordGrammar);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) { Directory.Delete(_tempRoot, recursive: true); }
        GC.SuppressFinalize(this);
    }

    private PinnedVoicing PinShapeFor(string symbol, Tuning tuning)
    {
        var spec = _expander.Expand(_parser.Parse(symbol));
        var set = VoicingSearch.Search(spec, tuning, HandModel.Default, SearchParams.Default, _catalogs.VoicingCategories);
        var v = set.Voicings[0];
        return new PinnedVoicing(
            v.Positions.OrderBy(p => p.String).Select(p => new PinnedPosition(p.String, p.Fret, p.Muted, p.Open)).ToList(),
            (int)Math.Round(v.Comfort * 100),
            v.Structure);
    }

    [Fact]
    public void Fill_only_touches_unpinned_slots_and_preserves_pinned_positions()
    {
        _songs.Create("Fill Song", "STANDARD", null, null);
        var (_, _, section) = _songs.AddSection("fill-song", "A");
        var tuning = _catalogs.Tunings.Get("STANDARD");

        _songs.AppendSlots("fill-song", section!.Id, ["C", "Am", "F", "G"]);

        var cShape = PinShapeFor("C", tuning);
        var fShape = PinShapeFor("F", tuning);
        var provenance = new PinProvenance(1, "STANDARD", HandModel.Default.ContentHash.ToString());
        _songs.PinSlot("fill-song", section.Id, "C", 1, cShape, provenance, slotIndex: 0);
        _songs.PinSlot("fill-song", section.Id, "F", 1, fShape, provenance, slotIndex: 2);

        var error = SongRoutes.Fill("fill-song", section.Id, _songs, _catalogs, _parser, _expander, minComfort: 0.0, solutionsCount: 1);

        error.Should().BeNull();
        var slots = _songs.Get("fill-song")!.Sections.Single().Slots;

        // PinnedVoicing carries a List<T>, whose default equality inside a record
        // is reference-based — compare structurally instead.
        slots[0].Voicing.Should().BeEquivalentTo(cShape, "the pinned C shape must survive fill untouched");
        slots[2].Voicing.Should().BeEquivalentTo(fShape, "the pinned F shape must survive fill untouched");
        slots[1].Voicing.Should().NotBeNull("the unpinned Am slot should be filled");
        slots[3].Voicing.Should().NotBeNull("the unpinned G slot should be filled");
    }

    [Fact]
    public void Fill_fails_naming_the_chord_with_no_voicing_above_the_floor()
    {
        _songs.Create("Impossible", "STANDARD", null, null);
        var (_, _, section) = _songs.AddSection("impossible", "A");
        _songs.AppendSlots("impossible", section!.Id, ["Cmaj13#11"]);

        var error = SongRoutes.Fill("impossible", section.Id, _songs, _catalogs, _parser, _expander, minComfort: 0.999, solutionsCount: 1);

        error.Should().NotBeNull();
        error.Should().Contain("Cmaj13#11");
    }

    [Fact]
    public void Fill_reports_when_song_or_section_not_found()
    {
        var error = SongRoutes.Fill("missing", "missing", _songs, _catalogs, _parser, _expander, 0.0, 1);
        error.Should().Be("Song or section not found.");
    }
}
