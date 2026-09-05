using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;

namespace Dadabe.Editor.Routes;

public static class ProgressionRoutes
{
    public static void MapProgressionRoutes(this WebApplication app)
    {
        app.MapGet("/progressions", (ProgressionService svc) =>
            Results.RazorSlice<ProgressionsIndex,
                IReadOnlyList<ProgressionModel>>(svc.ListAll()));

        app.MapGet("/api/progressions/new", () =>
            Results.RazorSlice<ProgressionsForm, ProgressionModel?>(null));

        app.MapGet("/api/progressions/{slug}/edit", (string slug, ProgressionService svc) =>
        {
            var model = svc.Get(slug);
            return model is null
                ? Results.NotFound()
                : Results.RazorSlice<ProgressionsForm, ProgressionModel?>(model);
        });

        app.MapPost("/api/progressions", async (HttpRequest req, ProgressionService svc) =>
        {
            var form = await req.ReadFormAsync();
            var name = form["name"].ToString();
            var chords = form["chords"].Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();
            var tempo = int.TryParse(form["tempo"], out var t) ? t : (int?)null;

            var (ok, error) = svc.Create(name, chords, tempo);
            if (!ok) return Results.BadRequest(error);

            return Results.RazorSlice<ProgressionsList,
                IReadOnlyList<ProgressionModel>>(svc.ListAll());
        });

        app.MapPut("/api/progressions/{slug}", async (string slug, HttpRequest req, ProgressionService svc) =>
        {
            var form = await req.ReadFormAsync();
            var name = form["name"].ToString();
            var chords = form["chords"].Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();
            var tempo = int.TryParse(form["tempo"], out var t) ? t : (int?)null;
            var existing = svc.Get(slug);

            var (ok, error) = svc.Update(slug, name, chords, tempo, existing?.DerivedFrom);
            if (!ok) return Results.BadRequest(error);

            return Results.RazorSlice<ProgressionsList,
                IReadOnlyList<ProgressionModel>>(svc.ListAll());
        });

        app.MapDelete("/api/progressions/{slug}", (string slug, ProgressionService svc) =>
        {
            svc.Delete(slug);
            return Results.Ok();
        });
    }
}
