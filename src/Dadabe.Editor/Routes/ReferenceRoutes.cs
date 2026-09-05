using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;

namespace Dadabe.Editor.Routes;

public static class ReferenceRoutes
{
    public static void MapReferenceRoutes(this WebApplication app)
    {
        app.MapGet("/reference", (ReferenceService svc, string? tab) =>
            RenderTab(svc, tab ?? "cadences"));

        app.MapGet("/api/reference/tab/{tab}", (string tab, ReferenceService svc) =>
            RenderTabFragment(svc, tab));

        // Cadences
        app.MapGet("/api/reference/cadences/new", () =>
            Results.RazorSlice<ReferenceCadenceForm, CadenceModel?>(null));

        app.MapGet("/api/reference/cadences/{slug}/edit", (string slug, ReferenceService svc) =>
        {
            var model = svc.GetCadence(slug);
            return model is null
                ? Results.NotFound()
                : Results.RazorSlice<ReferenceCadenceForm, CadenceModel?>(model);
        });

        app.MapPost("/api/reference/cadences", async (HttpRequest req, ReferenceService svc) =>
        {
            var (model, error) = await ParseCadenceForm(req);
            if (model is null) return Results.BadRequest(error);
            var (ok, err) = svc.SaveCadence(model);
            if (!ok) return Results.BadRequest(err);
            return Results.RazorSlice<ReferenceCadenceList,
                IReadOnlyList<CadenceModel>>(svc.ListCadences());
        });

        app.MapPut("/api/reference/cadences/{slug}", async (string slug, HttpRequest req, ReferenceService svc) =>
        {
            var (model, error) = await ParseCadenceForm(req, slug);
            if (model is null) return Results.BadRequest(error);
            var (ok, err) = svc.SaveCadence(model);
            if (!ok) return Results.BadRequest(err);
            return Results.RazorSlice<ReferenceCadenceList,
                IReadOnlyList<CadenceModel>>(svc.ListCadences());
        });

        app.MapDelete("/api/reference/cadences/{slug}", (string slug, ReferenceService svc) =>
        {
            svc.DeleteCadence(slug);
            return Results.Ok();
        });

        // Modes
        app.MapGet("/api/reference/modes/new", () =>
            Results.RazorSlice<ReferenceModeForm, ModeModel?>(null));

        app.MapGet("/api/reference/modes/{slug}/edit", (string slug, ReferenceService svc) =>
        {
            var model = svc.GetMode(slug);
            return model is null
                ? Results.NotFound()
                : Results.RazorSlice<ReferenceModeForm, ModeModel?>(model);
        });

        app.MapPost("/api/reference/modes", async (HttpRequest req, ReferenceService svc) =>
        {
            var (model, error) = await ParseModeForm(req);
            if (model is null) return Results.BadRequest(error);
            var (ok, err) = svc.SaveMode(model);
            if (!ok) return Results.BadRequest(err);
            return Results.RazorSlice<ReferenceModeList,
                IReadOnlyList<ModeModel>>(svc.ListModes());
        });

        app.MapPut("/api/reference/modes/{slug}", async (string slug, HttpRequest req, ReferenceService svc) =>
        {
            var (model, error) = await ParseModeForm(req, slug);
            if (model is null) return Results.BadRequest(error);
            var (ok, err) = svc.SaveMode(model);
            if (!ok) return Results.BadRequest(err);
            return Results.RazorSlice<ReferenceModeList,
                IReadOnlyList<ModeModel>>(svc.ListModes());
        });

        app.MapDelete("/api/reference/modes/{slug}", (string slug, ReferenceService svc) =>
        {
            svc.DeleteMode(slug);
            return Results.Ok();
        });

        // Scales
        app.MapGet("/api/reference/scales/new", () =>
            Results.RazorSlice<ReferenceScaleForm, ScaleModel?>(null));

        app.MapGet("/api/reference/scales/{slug}/edit", (string slug, ReferenceService svc) =>
        {
            var model = svc.GetScale(slug);
            return model is null
                ? Results.NotFound()
                : Results.RazorSlice<ReferenceScaleForm, ScaleModel?>(model);
        });

        app.MapPost("/api/reference/scales", async (HttpRequest req, ReferenceService svc) =>
        {
            var (model, error) = await ParseScaleForm(req);
            if (model is null) return Results.BadRequest(error);
            var (ok, err) = svc.SaveScale(model);
            if (!ok) return Results.BadRequest(err);
            return Results.RazorSlice<ReferenceScaleList,
                IReadOnlyList<ScaleModel>>(svc.ListScales());
        });

        app.MapPut("/api/reference/scales/{slug}", async (string slug, HttpRequest req, ReferenceService svc) =>
        {
            var (model, error) = await ParseScaleForm(req, slug);
            if (model is null) return Results.BadRequest(error);
            var (ok, err) = svc.SaveScale(model);
            if (!ok) return Results.BadRequest(err);
            return Results.RazorSlice<ReferenceScaleList,
                IReadOnlyList<ScaleModel>>(svc.ListScales());
        });

        app.MapDelete("/api/reference/scales/{slug}", (string slug, ReferenceService svc) =>
        {
            svc.DeleteScale(slug);
            return Results.Ok();
        });

        // Voicing categories
        app.MapPut("/api/reference/voicing-categories", async (HttpRequest req, ReferenceService svc) =>
        {
            var form = await req.ReadFormAsync();
            var json = form["json"].ToString();
            var (ok, error) = svc.SaveVoicingCategories(json);
            return ok
                ? Results.Content("<p class=\"success\">Saved.</p>", "text/html")
                : Results.Content($"<p class=\"error\">{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(error)}</p>", "text/html");
        });
    }

    private static IResult RenderTab(ReferenceService svc, string tab)
    {
        var model = new ReferenceIndexModel(
            tab,
            svc.ListCadences(),
            svc.ListModes(),
            svc.ListScales(),
            svc.GetVoicingCategories());
        return Results.RazorSlice<ReferenceIndex, ReferenceIndexModel>(model);
    }

    private static IResult RenderTabFragment(ReferenceService svc, string tab) => tab switch
    {
        "modes" => Results.RazorSlice<ReferenceModeList, IReadOnlyList<ModeModel>>(svc.ListModes()),
        "scales" => Results.RazorSlice<ReferenceScaleList, IReadOnlyList<ScaleModel>>(svc.ListScales()),
        "voicing-categories" => Results.RazorSlice<ReferenceVoicingCategories, string?>(svc.GetVoicingCategories()),
        _ => Results.RazorSlice<ReferenceCadenceList, IReadOnlyList<CadenceModel>>(svc.ListCadences()),
    };

    private static async Task<(CadenceModel? model, string error)> ParseCadenceForm(
        HttpRequest req, string? existingSlug = null)
    {
        var form = await req.ReadFormAsync();
        var type = form["type"].ToString();
        if (string.IsNullOrWhiteSpace(type)) return (null, "Type is required.");

        var slug = existingSlug ?? DataStore.ToSlug(type + "-" + Guid.NewGuid().ToString("N")[..6]);
        var chords = form["chords"].Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();

        return (new CadenceModel(
            slug, type,
            form["resolution"].ToString().NullIfEmpty(),
            chords,
            form["description"].ToString().NullIfEmpty()), string.Empty);
    }

    private static async Task<(ModeModel? model, string error)> ParseModeForm(
        HttpRequest req, string? existingSlug = null)
    {
        var form = await req.ReadFormAsync();
        var name = form["name"].ToString();
        if (string.IsNullOrWhiteSpace(name)) return (null, "Name is required.");

        var slug = existingSlug ?? DataStore.ToSlug(name);
        var intervals = ParseIntList(form["intervals"].ToString());
        var noteNames = ParseStringList(form["noteNames"].ToString());

        return (new ModeModel(
            slug, name, intervals,
            form["parentScale"].ToString().NullIfEmpty(),
            int.TryParse(form["degreeIndex"], out var d) ? d : null,
            noteNames.Count > 0 ? noteNames : null,
            form["description"].ToString().NullIfEmpty()), string.Empty);
    }

    private static async Task<(ScaleModel? model, string error)> ParseScaleForm(
        HttpRequest req, string? existingSlug = null)
    {
        var form = await req.ReadFormAsync();
        var name = form["name"].ToString();
        if (string.IsNullOrWhiteSpace(name)) return (null, "Name is required.");

        var slug = existingSlug ?? DataStore.ToSlug(name);
        var notes = ParseStringList(form["notes"].ToString());
        var intervals = ParseIntList(form["intervals"].ToString());

        return (new ScaleModel(
            slug, name, notes, intervals,
            form["modeOf"].ToString().NullIfEmpty(),
            form["description"].ToString().NullIfEmpty()), string.Empty);
    }

    private static List<int> ParseIntList(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .Select(x => int.TryParse(x, out var i) ? i : (int?)null)
         .Where(x => x.HasValue)
         .Select(x => x!.Value)
         .ToList();

    private static List<string> ParseStringList(string s) =>
        s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .ToList();
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
