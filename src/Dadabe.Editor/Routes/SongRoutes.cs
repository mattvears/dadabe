using System.Collections.Immutable;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dadabe.Core;
using Dadabe.Core.Chord;
using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;
using Dadabe.Fretboard;
using Microsoft.AspNetCore.Mvc;

namespace Dadabe.Editor.Routes;

public static class SongRoutes
{
    private static readonly char[] ChordSeparators = [' ', '\n', '\r', '\t'];

    public static void MapSongRoutes(this WebApplication app)
    {
        // ── top-level song CRUD (mirrors ProgressionRoutes) ──
        app.MapGet("/songs", ([FromServices] SongService svc) =>
            Results.RazorSlice<SongsIndex, IReadOnlyList<SongModel>>(svc.ListAll()));

        app.MapGet("/api/songs/options", ([FromServices] SongService svc) =>
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("""<wa-option value="">— none —</wa-option>""");
            foreach (var s in svc.ListAll())
            {
                var slug = WebUtility.HtmlEncode(s.Slug);
                var name = WebUtility.HtmlEncode(s.Name);
                var tuning = WebUtility.HtmlEncode(s.Tuning ?? "");
                sb.Append(System.Globalization.CultureInfo.InvariantCulture,
                    $"""<wa-option value="{slug}" data-name="{name}" data-tuning="{tuning}">{name}</wa-option>""");
            }
            return Results.Content(sb.ToString(), "text/html");
        });

        app.MapGet("/api/songs/{slug}/sections/options", (string slug, [FromServices] SongService svc) =>
        {
            var song = svc.Get(slug);
            var sb = new System.Text.StringBuilder();
            if (song is not null)
            {
                foreach (var s in song.Sections)
                {
                    var id = WebUtility.HtmlEncode(s.Id);
                    var name = WebUtility.HtmlEncode(s.Name);
                    sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"""<wa-option value="{id}">{name}</wa-option>""");
                }
            }
            return Results.Content(sb.ToString(), "text/html");
        });

        app.MapGet("/api/songs/new", () =>
            Results.RazorSlice<SongsForm, SongsFormModel>(FormModel(null)));

        app.MapGet("/api/songs/{slug}/edit", (string slug, [FromServices] SongService svc) =>
        {
            var model = svc.Get(slug);
            return model is null
                ? Results.NotFound()
                : Results.RazorSlice<SongsForm, SongsFormModel>(FormModel(model));
        });

        app.MapPost("/api/songs", async (HttpRequest req, [FromServices] SongService svc) =>
        {
            var form = await req.ReadFormAsync();
            var name = form["name"].ToString();
            var tuning = form["tuning"].ToString().NullIfEmpty();
            var key = form["key"].ToString().NullIfEmpty();
            var tempo = int.TryParse(form["tempo"], out var t) ? t : (int?)null;

            var (ok, error) = svc.Create(name, tuning, key, tempo);
            if (!ok) { return Results.BadRequest(error); }
            return Results.RazorSlice<SongsList, IReadOnlyList<SongModel>>(svc.ListAll());
        });

        app.MapPut("/api/songs/{slug}", async (string slug, HttpRequest req, [FromServices] SongService svc) =>
        {
            var form = await req.ReadFormAsync();
            var name = form["name"].ToString();
            var tuning = form["tuning"].ToString().NullIfEmpty();
            var key = form["key"].ToString().NullIfEmpty();
            var tempo = int.TryParse(form["tempo"], out var t) ? t : (int?)null;
            var existing = svc.Get(slug);

            var (ok, error) = svc.Update(slug, name, tuning, key, tempo, existing?.Hand);
            if (!ok) { return Results.BadRequest(error); }
            return Results.RazorSlice<SongsList, IReadOnlyList<SongModel>>(svc.ListAll());
        });

        app.MapDelete("/api/songs/{slug}", (string slug, [FromServices] SongService svc) =>
        {
            svc.Delete(slug);
            return Results.Ok();
        });

        // ── song detail page ──
        app.MapGet("/songs/{slug}", (
            string slug,
            [FromServices] SongService songs,
            [FromServices] ProgressionService progressions,
            [FromServices] Catalogs catalogs,
            [FromServices] ChordParser parser,
            [FromServices] ChordExpander expander) =>
        {
            var model = BuildDetailModel(slug, songs, progressions, catalogs, parser, expander);
            return model is null
                ? Results.NotFound()
                : Results.RazorSlice<SongDetail, SongDetailModel>(model);
        });

        app.MapGet("/songs/{slug}/chart", (
            string slug,
            [FromServices] SongService songs,
            [FromServices] ProgressionService progressions,
            [FromServices] Catalogs catalogs,
            [FromServices] ChordParser parser,
            [FromServices] ChordExpander expander) =>
        {
            var model = BuildDetailModel(slug, songs, progressions, catalogs, parser, expander);
            return model is null
                ? Results.NotFound()
                : Results.RazorSlice<SongChart, SongChartModel>(new SongChartModel(model.Song, model.KeyDisplay, model.Sections));
        });

        app.MapGet("/api/songs/{slug}/export", (string slug, [FromServices] SongService songs) =>
        {
            var song = songs.Get(slug);
            if (song is null) { return Results.NotFound(); }
            var json = JsonSerializer.Serialize(song, ExportOptions);
            return Results.Text(json, "application/json");
        });

        app.MapPost("/api/songs/import", async (HttpRequest req, [FromServices] SongService svc) =>
        {
            var form = await req.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0) { return Results.BadRequest("A song JSON file is required."); }

            using var reader = new StreamReader(file.OpenReadStream());
            var json = await reader.ReadToEndAsync();
            SongModel? imported;
            try { imported = JsonSerializer.Deserialize<SongModel>(json, ExportOptions); }
            catch (JsonException ex) { return Results.BadRequest($"Invalid song JSON: {ex.Message}"); }
            if (imported is null || string.IsNullOrWhiteSpace(imported.Slug))
            {
                return Results.BadRequest("Invalid song JSON: missing slug.");
            }

            svc.SaveImported(imported);
            return Results.RazorSlice<SongsList, IReadOnlyList<SongModel>>(svc.ListAll());
        });

        // ── sections ──
        app.MapPost("/api/songs/{slug}/sections", async (string slug, HttpRequest req, [FromServices] SongService songs,
            [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser, [FromServices] ChordExpander expander) =>
        {
            var form = await req.ReadFormAsync();
            var name = form["name"].ToString();
            var (ok, error, _) = songs.AddSection(slug, name);
            return DetailFragment(slug, songs, progressions, catalogs, parser, expander, ok ? null : error);
        });

        app.MapDelete("/api/songs/{slug}/sections/{id}", (string slug, string id, [FromServices] SongService songs,
            [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser, [FromServices] ChordExpander expander) =>
        {
            var (ok, error) = songs.DeleteSection(slug, id);
            return DetailFragment(slug, songs, progressions, catalogs, parser, expander, ok ? null : error);
        });

        // ── slots ──
        app.MapPost("/api/songs/{slug}/sections/{id}/slots", async (string slug, string id, HttpRequest req,
            [FromServices] SongService songs, [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser, [FromServices] ChordExpander expander) =>
        {
            var form = await req.ReadFormAsync();
            var symbol = form["symbol"].ToString().Trim();
            var bars = int.TryParse(form["bars"], out var b) ? Math.Max(1, b) : 1;

            if (string.IsNullOrWhiteSpace(symbol))
            {
                return DetailFragment(slug, songs, progressions, catalogs, parser, expander, "A chord symbol is required.");
            }
            if (!parser.TryParse(symbol, out _, out var parseError))
            {
                return DetailFragment(slug, songs, progressions, catalogs, parser, expander, $"Invalid chord '{symbol}': {parseError}");
            }

            var positionsRaw = form["positions"].ToString();
            string? error = null;
            if (string.IsNullOrEmpty(positionsRaw))
            {
                var (ok, err) = songs.AppendSlots(slug, id, [symbol], bars);
                error = ok ? null : err;
            }
            else
            {
                var (ok, err, provenance) = BuildPinArgs(slug, songs, catalogs, form, positionsRaw);
                if (!ok || provenance is null) { error = err; }
                else
                {
                    var (pinOk, pinErr) = songs.PinSlot(slug, id, symbol, bars, provenance.Value.Voicing, provenance.Value.Provenance);
                    error = pinOk ? null : pinErr;
                }
            }

            return DetailFragment(slug, songs, progressions, catalogs, parser, expander, error);
        });

        app.MapDelete("/api/songs/{slug}/sections/{id}/slots/{index:int}", (string slug, string id, int index,
            [FromServices] SongService songs, [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser, [FromServices] ChordExpander expander) =>
        {
            var (ok, error) = songs.RemoveSlot(slug, id, index);
            return DetailFragment(slug, songs, progressions, catalogs, parser, expander, ok ? null : error);
        });

        // Dynamic pin target for the voicings tab, where the active song/section
        // are chosen client-side (header selects) rather than baked into the URL —
        // slug/sectionId travel in the form body instead of the route (D37 §5.2).
        app.MapPost("/api/songs/pin", async (HttpRequest req, [FromServices] SongService songs, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser) =>
        {
            var form = await req.ReadFormAsync();
            var slug = form["slug"].ToString();
            var sectionId = form["sectionId"].ToString();
            var symbol = form["symbol"].ToString();

            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(sectionId))
            {
                return Results.Content(
                    """<p class="form-hint">Select an active song and section (top right) before pinning.</p>""",
                    "text/html");
            }
            if (!parser.TryParse(symbol, out _, out var parseError))
            {
                return Results.Content($"""<p style="color:var(--wa-color-danger-600)">{WebUtility.HtmlEncode(parseError)}</p>""", "text/html");
            }

            var positionsRaw = form["positions"].ToString();
            var (ok, error, args) = BuildPinArgs(slug, songs, catalogs, form, positionsRaw);
            if (!ok || args is null)
            {
                return Results.Content($"""<p style="color:var(--wa-color-danger-600)">{WebUtility.HtmlEncode(error)}</p>""", "text/html");
            }

            var bars = int.TryParse(form["bars"], out var b) ? Math.Max(1, b) : 1;
            var (pinOk, pinErr) = songs.PinSlot(slug, sectionId, symbol, bars, args.Value.Voicing, args.Value.Provenance);
            return Results.Content(
                pinOk
                    ? $"""<p style="color:var(--wa-color-success-600)">Pinned {WebUtility.HtmlEncode(symbol)}.</p>"""
                    : $"""<p style="color:var(--wa-color-danger-600)">{WebUtility.HtmlEncode(pinErr)}</p>""",
                "text/html");
        });

        // ── paste / instantiate / promote (§12 progression interop) ──
        app.MapPost("/api/songs/{slug}/sections/{id}/paste", async (string slug, string id, HttpRequest req,
            [FromServices] SongService songs, [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser, [FromServices] ChordExpander expander) =>
        {
            var form = await req.ReadFormAsync();
            var chart = form["chart"].ToString();
            var symbols = chart.Split(ChordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            string? error = null;
            foreach (var symbol in symbols)
            {
                if (!parser.TryParse(symbol, out _, out var parseError)) { error = $"Invalid chord '{symbol}': {parseError}"; break; }
            }
            if (error is null && symbols.Length > 0)
            {
                var (ok, err) = songs.AppendSlots(slug, id, symbols);
                error = ok ? null : err;
            }

            return DetailFragment(slug, songs, progressions, catalogs, parser, expander, error);
        });

        app.MapPost("/api/songs/{slug}/sections/{id}/instantiate", async (string slug, string id, HttpRequest req,
            [FromServices] SongService songs, [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser, [FromServices] ChordExpander expander) =>
        {
            var form = await req.ReadFormAsync();
            var progressionSlug = form["progression"].ToString();
            var progression = progressions.Get(progressionSlug);
            string? error = progression is null ? "Progression not found." : null;
            if (progression is not null)
            {
                var (ok, err) = songs.AppendSlots(slug, id, progression.Chords);
                error = ok ? null : err;
            }
            return DetailFragment(slug, songs, progressions, catalogs, parser, expander, error);
        });

        app.MapPost("/api/songs/{slug}/sections/{id}/promote", async (string slug, string id, HttpRequest req,
            [FromServices] SongService songs, [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser, [FromServices] ChordExpander expander) =>
        {
            var form = await req.ReadFormAsync();
            var name = form["name"].ToString();
            var (song, section) = songs.FindSection(slug, id);
            string? error;
            if (song is null || section is null) { error = "Section not found."; }
            else
            {
                var chords = section.Slots.Select(s => s.Symbol).ToList();
                var (ok, err) = progressions.Create(name, chords, song.Tempo);
                error = ok ? null : err;
            }
            return DetailFragment(slug, songs, progressions, catalogs, parser, expander, error);
        });

        // ── fill unpinned slots by voice leading (§9) ──
        app.MapPost("/api/songs/{slug}/sections/{id}/fill", async (string slug, string id, HttpRequest req,
            [FromServices] SongService songs, [FromServices] ProgressionService progressions, [FromServices] Catalogs catalogs, [FromServices] ChordParser parser, [FromServices] ChordExpander expander) =>
        {
            var form = await req.ReadFormAsync();
            var minComfortPct = int.TryParse(form["minComfort"], out var mc) ? Math.Clamp(mc, 0, 100) : 0;
            var solutionsCount = int.TryParse(form["solutions"], out var s) ? Math.Clamp(s, 1, 5) : 1;

            var error = Fill(slug, id, songs, catalogs, parser, expander, minComfortPct / 100.0, solutionsCount);
            return DetailFragment(slug, songs, progressions, catalogs, parser, expander, error);
        });
    }

    // ── helpers ──

    private static readonly JsonSerializerOptions ExportOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static SongsFormModel FormModel(SongModel? song) => new(
        song,
        Catalogs.Default().Tunings.All.OrderBy(t => t.Name, StringComparer.Ordinal).Select(t => t.Name).ToList(),
        []);

    /// <summary>Parses the "positions" JSON (§9 pin payload) and assembles a <see cref="PinnedVoicing"/> + <see cref="PinProvenance"/>.</summary>
    private static (bool Ok, string? Error, (PinnedVoicing Voicing, PinProvenance Provenance)? Args) BuildPinArgs(
        string slug, SongService songs, Catalogs catalogs, IFormCollection form, string positionsRaw)
    {
        var song = songs.Get(slug);
        if (song is null) { return (false, "Song not found.", null); }

        List<PinnedPositionDto>? positions;
        try { positions = JsonSerializer.Deserialize<List<PinnedPositionDto>>(positionsRaw, ExportOptions); }
        catch (JsonException) { return (false, "Malformed voicing positions.", null); }
        if (positions is null) { return (false, "Malformed voicing positions.", null); }

        var comfortPct = int.TryParse(form["comfortPct"], out var cp) ? cp : 0;
        var structure = form["structure"].ToString();
        var tuningName = song.Tuning ?? "DADABE";

        Dadabe.Core.Tuning tuning;
        try { tuning = VoicingRoutes.ResolveTuningPublic(catalogs, tuningName, tuningName); }
        catch (FormatException ex) { return (false, ex.Message, null); }

        var hand = SongHandResolver.ResolveHandModel(song.Hand);
        var voicing = new PinnedVoicing(
            positions.Select(p => new PinnedPosition(p.String, p.Fret, p.Muted, p.Open)).ToList(),
            comfortPct,
            structure);
        var provenance = new PinProvenance((int)ModelVersion.V1, tuning.Name, hand.ContentHash.ToString());
        return (true, null, (voicing, provenance));
    }

    private sealed record PinnedPositionDto(
        [property: JsonPropertyName("string")] int String,
        [property: JsonPropertyName("fret")] int? Fret,
        [property: JsonPropertyName("muted")] bool Muted,
        [property: JsonPropertyName("open")] bool Open);

    /// <summary>
    /// Fills every unpinned slot in a section via <see cref="VoiceLeadSolver"/>,
    /// treating pinned slots as single-candidate layers so the solver routes
    /// around them (§9). Returns an error message, or null on success.
    /// </summary>
    internal static string? Fill(
        string slug, string sectionId, SongService songs, Catalogs catalogs,
        ChordParser parser, ChordExpander expander, double minComfort, int solutionsCount)
    {
        var (song, section) = songs.FindSection(slug, sectionId);
        if (song is null || section is null) { return "Song or section not found."; }
        if (section.Slots.Count == 0) { return "Section has no slots to fill."; }

        var tuningName = song.Tuning ?? "DADABE";
        Dadabe.Core.Tuning tuning;
        try { tuning = VoicingRoutes.ResolveTuningPublic(catalogs, tuningName, tuningName); }
        catch (FormatException ex) { return ex.Message; }

        var hand = SongHandResolver.ResolveHandModel(song.Hand);
        var searchParams = SongHandResolver.ResolveSearchParams(song.Hand);

        var voicingsPerSlot = new List<ImmutableArray<Voicing>>(section.Slots.Count);
        foreach (var slot in section.Slots)
        {
            if (!parser.TryParse(slot.Symbol, out var symbol, out var parseError))
            {
                return $"Invalid chord '{slot.Symbol}': {parseError}";
            }
            var spec = expander.Expand(symbol);
            var set = VoicingSearch.Search(spec, tuning, hand, searchParams, catalogs.VoicingCategories);

            if (slot.Voicing is { } pinned)
            {
                var match = set.Voicings.FirstOrDefault(v => PositionsMatch(v, pinned));
                if (match is null)
                {
                    return $"'{slot.Symbol}' was pinned under a different tuning or hand model — re-pin it before filling.";
                }
                voicingsPerSlot.Add([match]);
            }
            else
            {
                var candidates = set.Voicings;
                if (minComfort > 0.0) { candidates = candidates.Where(v => v.Comfort >= minComfort).ToImmutableArray(); }
                if (candidates.Length == 0)
                {
                    return $"Chord '{slot.Symbol}' has no playable voicings above min-comfort {(int)Math.Round(minComfort * 100)}%.";
                }
                voicingsPerSlot.Add(candidates);
            }
        }

        var solved = VoiceLeadSolver.Solve(voicingsPerSlot, solutionsCount);
        if (solved.Count == 0) { return "No voice-leading solution found."; }

        var best = solved[0];
        var newSlots = new List<SongSlot>(section.Slots.Count);
        for (var i = 0; i < section.Slots.Count; i++)
        {
            var v = best.Steps[i].Voicing;
            var voicing = new PinnedVoicing(
                v.Positions.OrderBy(p => p.String).Select(p => new PinnedPosition(p.String, p.Fret, p.Muted, p.Open)).ToList(),
                (int)Math.Round(v.Comfort * 100),
                v.Structure);
            var provenance = new PinProvenance((int)ModelVersion.V1, tuning.Name, hand.ContentHash.ToString());
            newSlots.Add(section.Slots[i] with { Voicing = voicing, PinnedUnder = provenance });
        }

        songs.ReplaceSlots(slug, sectionId, newSlots);
        return null;
    }

    private static bool PositionsMatch(Voicing v, PinnedVoicing pinned)
    {
        var byString = v.Positions.ToDictionary(p => p.String);
        if (byString.Count != pinned.Positions.Count) { return false; }
        foreach (var pp in pinned.Positions)
        {
            if (!byString.TryGetValue(pp.String, out var actual)) { return false; }
            if (actual.Muted != pp.Muted || actual.Open != pp.Open) { return false; }
            if (!pp.Muted && !pp.Open && actual.Fret != pp.Fret) { return false; }
        }
        return true;
    }

    private static IResult DetailFragment(
        string slug, SongService songs, ProgressionService progressions, Catalogs catalogs,
        ChordParser parser, ChordExpander expander, string? error)
    {
        var model = BuildDetailModel(slug, songs, progressions, catalogs, parser, expander, error);
        return model is null
            ? Results.NotFound()
            : Results.RazorSlice<SongDetailBody, SongDetailModel>(model);
    }

    internal static SongDetailModel? BuildDetailModel(
        string slug, SongService songs, ProgressionService progressions, Catalogs catalogs,
        ChordParser parser, ChordExpander expander, string? error = null)
    {
        var song = songs.Get(slug);
        if (song is null) { return null; }

        var hand = SongHandResolver.ResolveHandModel(song.Hand);
        var tuningName = song.Tuning ?? "DADABE";
        Dadabe.Core.Tuning? tuning = null;
        try { tuning = VoicingRoutes.ResolveTuningPublic(catalogs, tuningName, tuningName); }
        catch (FormatException) { /* tuning unresolved — diagrams degrade to ASCII-less display below */ }

        var allSpecs = new List<ChordSpec>();
        var sectionViews = new List<SectionViewModel>();
        foreach (var section in song.Sections)
        {
            var slotViews = new List<SlotViewModel>();
            for (var i = 0; i < section.Slots.Count; i++)
            {
                var slot = section.Slots[i];
                if (parser.TryParse(slot.Symbol, out var symbol, out _))
                {
                    allSpecs.Add(expander.Expand(symbol));
                }

                ChordDiagram? diagram = slot.Voicing is not null && tuning is not null
                    ? ChordDiagramBuilder.FromPinned(slot.Voicing.Positions, tuning)
                    : null;
                var stale = IsStale(slot, song, hand);
                slotViews.Add(new SlotViewModel(i, slot, diagram, stale));
            }

            sectionViews.Add(new SectionViewModel(
                section, slotViews, BuildPositionRange(section), BuildHardestChordLabel(section),
                section.Slots.Count(s => s.Voicing is not null)));
        }

        var keyDisplay = BuildKeyDisplay(song, allSpecs);
        return new SongDetailModel(song, keyDisplay, sectionViews, progressions.ListAll(), error);
    }

    private static bool IsStale(SongSlot slot, SongModel song, HandModel currentHand)
    {
        if (slot.PinnedUnder is not { } prov) { return false; }
        if (prov.ModelVersion != (int)ModelVersion.V1) { return true; }
        if (song.Tuning is not null && !string.Equals(prov.Tuning, song.Tuning, StringComparison.Ordinal)) { return true; }
        return !string.Equals(prov.HandHash, currentHand.ContentHash.ToString(), StringComparison.Ordinal);
    }

    private static string? BuildPositionRange(SongSection section)
    {
        var frets = section.Slots
            .Where(s => s.Voicing is not null)
            .SelectMany(s => s.Voicing!.Positions)
            .Where(p => p.Fret is > 0)
            .Select(p => p.Fret!.Value)
            .ToList();
        if (frets.Count == 0) { return null; }
        var min = frets.Min();
        var max = frets.Max();
        return min == max ? $"fret {min}" : $"frets {min}–{max}";
    }

    private static string? BuildHardestChordLabel(SongSection section)
    {
        var weakest = section.Slots
            .Where(s => s.Voicing is not null)
            .OrderBy(s => s.Voicing!.ComfortPct)
            .FirstOrDefault();
        return weakest is null ? null : $"{weakest.Symbol} ({weakest.Voicing!.ComfortPct}%)";
    }

    /// <summary>
    /// Key inference is display-only (D39) — nothing branches on it. A user
    /// override always wins; where inference fails, "no clear key centre" is
    /// a true and useful statement about a modal tune, not a failure.
    /// </summary>
    private static string BuildKeyDisplay(SongModel song, IReadOnlyList<ChordSpec> context)
    {
        if (!string.IsNullOrWhiteSpace(song.Key)) { return $"Key: {song.Key} (set)"; }
        var inferred = KeyInference.InferKey(context);
        if (inferred is null) { return "Key: no clear key centre"; }
        var note = Note.Spell(inferred.Value.RootPc, Letter.C);
        return $"Key: {note} {(inferred.Value.IsMinor ? "minor" : "major")} (inferred)";
    }
}

file static class StringExtensionsSongs
{
    public static string? NullIfEmpty(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
