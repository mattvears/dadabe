using System.Text.Json;
using Dadabe.Core.Chord;
using Dadabe.Editor.Services;
using FluentAssertions;

namespace Dadabe.Editor.Tests;

/// <summary>
/// Guards the hand-authored seed data under <c>src/Dadabe.Editor/data</c> so that
/// the progression and prediction fixtures stay structurally valid and musically
/// parseable as they grow. See <c>docs/whitepaper-progressions.md</c> for the
/// intent behind the fixture set.
/// </summary>
public class DataFixtureTests
{
    private static readonly ChordParser Parser = new(ChordGrammarLoader.Load(null));

    /// <summary>
    /// Fixtures that deliberately carry an unparseable chord symbol to exercise the
    /// lenient silent-skip parsing path (whitepaper §3 / <c>PredictCommand.cs:59-61</c>).
    /// They must still contain at least one *valid* chord so the context stays usable.
    /// </summary>
    private static readonly HashSet<string> IntentionallyMalformed =
        new(StringComparer.Ordinal) { "mostly-valid-snippet", "malformed-symbol" };

    // ---- progressions -----------------------------------------------------

    public static IEnumerable<object[]> ProgressionFiles() => Files(ProgressionsDir);

    [Theory]
    [MemberData(nameof(ProgressionFiles))]
    public void Progression_deserializes_with_required_fields(string path)
    {
        var model = Load<ProgressionModel>(path);

        model.Slug.Should().NotBeNullOrWhiteSpace();
        model.Name.Should().NotBeNullOrWhiteSpace();
        model.Chords.Should().NotBeNull().And.NotBeEmpty();
        model.Slug.Should().Be(SlugOf(path), "the slug field must match the file name");
    }

    [Theory]
    [MemberData(nameof(ProgressionFiles))]
    public void Progression_chords_parse_or_exercise_silent_skip(string path)
    {
        var model = Load<ProgressionModel>(path);
        var parsed = model.Chords.Select(c => (chord: c, ok: Parser.TryParse(c, out _, out _))).ToList();

        if (IntentionallyMalformed.Contains(model.Slug))
        {
            parsed.Should().Contain(p => p.ok, "an intentionally-malformed fixture must still have a usable chord");
            parsed.Should().Contain(p => !p.ok, "this fixture is registered as exercising the silent-skip path");
        }
        else
        {
            var bad = parsed.Where(p => !p.ok).Select(p => p.chord);
            bad.Should().BeEmpty("every chord in a non-malformed fixture must parse via the real grammar");
        }
    }

    [Theory]
    [MemberData(nameof(ProgressionFiles))]
    public void Progression_tempo_respects_schema_minimum(string path)
    {
        var model = Load<ProgressionModel>(path);
        if (model.Tempo is { } tempo)
            tempo.Should().BeGreaterThanOrEqualTo(1, "progression.schema.json sets minimum: 1 on tempo");
    }

    [Fact]
    public void Progression_slugs_are_unique()
    {
        var slugs = Directory.GetFiles(ProgressionsDir, "*.json")
            .Select(f => Load<ProgressionModel>(f).Slug)
            .ToList();
        slugs.Should().OnlyHaveUniqueItems();
    }

    // ---- predictions ------------------------------------------------------

    public static IEnumerable<object[]> PredictionFiles() => Files(PredictionsDir);

    [Theory]
    [MemberData(nameof(PredictionFiles))]
    public void Prediction_deserializes_with_required_fields(string path)
    {
        var model = Load<PredictionModel>(path);

        model.Slug.Should().NotBeNullOrWhiteSpace();
        model.Name.Should().NotBeNullOrWhiteSpace();
        model.Chord.Should().NotBeNullOrWhiteSpace();
        model.Filters.Should().NotBeNull();
        model.Slug.Should().Be(SlugOf(path), "the slug field must match the file name");
    }

    [Theory]
    [MemberData(nameof(PredictionFiles))]
    public void Prediction_seed_chord_parses(string path)
    {
        var model = Load<PredictionModel>(path);
        Parser.TryParse(model.Chord, out _, out var error)
            .Should().BeTrue($"seed chord '{model.Chord}' must parse: {error}");
    }

    [Theory]
    [MemberData(nameof(PredictionFiles))]
    public void Prediction_context_resolves_to_a_progression(string path)
    {
        var model = Load<PredictionModel>(path);
        if (model.ContextSlug is null) return;

        File.Exists(Path.Combine(ProgressionsDir, $"{model.ContextSlug}.json"))
            .Should().BeTrue($"context '{model.ContextSlug}' must reference an existing progression");
    }

    [Theory]
    [MemberData(nameof(PredictionFiles))]
    public void Prediction_bounds_match_schema(string path)
    {
        var model = Load<PredictionModel>(path);

        if (model.MaxResults is { } mr)
            mr.Should().BeInRange(1, 50, "prediction.schema.json bounds maxResults to [1, 50]");
        if (model.Entropy is { } en)
            en.Should().BeInRange(0.01, 10.0, "prediction.schema.json bounds entropy to [0.01, 10.0]");
    }

    [Fact]
    public void Prediction_slugs_are_unique()
    {
        var slugs = Directory.GetFiles(PredictionsDir, "*.json")
            .Select(f => Load<PredictionModel>(f).Slug)
            .ToList();
        slugs.Should().OnlyHaveUniqueItems();
    }

    // ---- helpers ----------------------------------------------------------

    private static string ProgressionsDir => Path.Combine(DataRoot, "progressions");
    private static string PredictionsDir => Path.Combine(DataRoot, "predictions");
    private static string DataRoot => Path.Combine(RepoRoot(), "src", "Dadabe.Editor", "data");

    private static IEnumerable<object[]> Files(string dir) =>
        Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new object[] { f });

    private static string SlugOf(string path) => Path.GetFileNameWithoutExtension(path);

    private static T Load<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Deserialized null from {path}");

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "schemas"))
                && Directory.Exists(Path.Combine(dir, "src")))
            {
                return dir;
            }
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) { break; }
            dir = parent;
        }
        throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
    }
}
