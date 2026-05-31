using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;

namespace Dadabe.Editor.Routes;

public static class TuningRoutes
{
    public static void MapTuningRoutes(this WebApplication app)
    {
        app.MapGet("/tunings", (TuningService svc) =>
            Results.RazorSlice<TuningsIndex,
                IReadOnlyList<TuningModel>>(svc.ListAll()));

        app.MapGet("/api/tunings/new", () =>
            Results.RazorSlice<TuningsForm, TuningModel?>(null));

        app.MapGet("/api/tunings/{slug}/edit", (string slug, TuningService svc) =>
        {
            var model = svc.Get(slug);
            return model is null
                ? Results.NotFound()
                : Results.RazorSlice<TuningsForm, TuningModel?>(model);
        });

        app.MapPost("/api/tunings", async (HttpRequest req, TuningService svc) =>
        {
            var form    = await req.ReadFormAsync();
            var name    = form["name"].ToString();
            var strings = form["strings"].Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList();

            var (ok, error) = svc.Create(name, strings);
            if (!ok) return Results.BadRequest(error);

            return Results.RazorSlice<TuningsList,
                IReadOnlyList<TuningModel>>(svc.ListAll());
        });

        app.MapPut("/api/tunings/{slug}", async (string slug, HttpRequest req, TuningService svc) =>
        {
            var form    = await req.ReadFormAsync();
            var name    = form["name"].ToString();
            var strings = form["strings"].Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList();

            var (ok, error) = svc.Update(slug, name, strings);
            if (!ok) return Results.BadRequest(error);

            return Results.RazorSlice<TuningsList,
                IReadOnlyList<TuningModel>>(svc.ListAll());
        });

        app.MapDelete("/api/tunings/{slug}", (string slug, TuningService svc) =>
        {
            svc.Delete(slug);
            return Results.Ok();
        });
    }
}
