using FluentAssertions;

namespace Dadabe.Fretboard.Tests;

/// <summary>
/// Verifies the CWD-overlay behaviour of VoicingCategoryCatalog (D20/v0.3).
/// </summary>
public class VoicingCategoryCatalogTests
{
    [Fact]
    public void Embedded_catalog_loads_without_overlay()
    {
        var cat = VoicingCategoryCatalog.Load(workingDirectory: null);
        cat.Categories.Should().NotBeEmpty();
    }

    [Fact]
    public void Embedded_catalog_contains_core_5_categories()
    {
        var cat = VoicingCategoryCatalog.Load(workingDirectory: null);
        var names = cat.Categories.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        names.Should().Contain("power");
        names.Should().Contain("triad");
        names.Should().Contain("shell");
        names.Should().Contain("drop-2");
        names.Should().Contain("drop-3");
        names.Should().Contain("spread");
    }

    [Fact]
    public void Power_is_ordered_before_triad()
    {
        // 'triad' allows all-{1,5} function sets at noteCount 3, so it would
        // absorb the octave-doubled power shape if it were evaluated first.
        var cat = VoicingCategoryCatalog.Load(workingDirectory: null);
        var names = cat.Categories.Select(c => c.Name).ToList();
        names.IndexOf("power").Should().BeLessThan(names.IndexOf("triad"));
    }

    [Fact]
    public void Last_category_is_the_matchAny_fallthrough()
    {
        var cat = VoicingCategoryCatalog.Load(workingDirectory: null);
        var last = cat.Categories.Last();
        last.Matches.Any(m => m.MatchAny).Should().BeTrue("spread/fallthrough must be last");
    }

    [Fact]
    public void Overlay_from_cwd_adds_new_category()
    {
        var dir = WriteOverlay("""
            {
              "categories": [
                {
                  "name": "custom-test",
                  "description": "overlay test category",
                  "matches": [ { "matchAny": true } ]
                }
              ]
            }
            """);
        try
        {
            var cat = VoicingCategoryCatalog.Load(dir);
            cat.Categories.Should().Contain(c => c.Name == "custom-test");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Overlay_replacing_category_by_name_is_deterministic()
    {
        // Write an overlay that redefines 'triad' with a custom description.
        var dir = WriteOverlay("""
            {
              "categories": [
                {
                  "name": "triad",
                  "description": "overridden in test",
                  "matches": [ { "noteCount": 3 } ]
                }
              ]
            }
            """);
        try
        {
            var cat1 = VoicingCategoryCatalog.Load(dir);
            var cat2 = VoicingCategoryCatalog.Load(dir);

            var triad1 = cat1.Categories.Single(c => c.Name == "triad");
            var triad2 = cat2.Categories.Single(c => c.Name == "triad");
            triad1.Description.Should().Be("overridden in test");
            triad2.Description.Should().Be(triad1.Description);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Overlay_priority_list_fully_replaces_embedded_priority()
    {
        var dir = WriteOverlay("""
            {
              "priority": ["custom-a", "custom-b"],
              "categories": [
                { "name": "custom-a", "matches": [ { "matchAny": false } ] },
                { "name": "custom-b", "matches": [ { "matchAny": true  } ] }
              ]
            }
            """);
        try
        {
            var cat = VoicingCategoryCatalog.Load(dir);
            cat.Priority.Should().StartWith(["custom-a", "custom-b"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void No_overlay_file_produces_identical_catalog_on_repeated_calls()
    {
        var cat1 = VoicingCategoryCatalog.Load(workingDirectory: null);
        var cat2 = VoicingCategoryCatalog.Load(workingDirectory: null);
        cat1.Categories.Select(c => c.Name).Should()
            .Equal(cat2.Categories.Select(c => c.Name));
    }

    private static string WriteOverlay(string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "dadabe-cattest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, VoicingCategoryCatalog.FileName), json);
        return dir;
    }
}
