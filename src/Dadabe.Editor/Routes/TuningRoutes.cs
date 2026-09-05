using System.Net;
using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;
using Dadabe.Fretboard;
using Microsoft.AspNetCore.Mvc;

namespace Dadabe.Editor.Routes;

public static class TuningRoutes
{
    public static void MapTuningRoutes(this WebApplication app)
    {
        app.MapGet("/api/tunings/options", (
            [FromServices] Catalogs catalogs,
            [FromServices] TuningService tunings) =>
        {
            var sb = new System.Text.StringBuilder();
            foreach (var t in catalogs.Tunings.All.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                var val = WebUtility.HtmlEncode(t.Name);
                var selected = t.Name == "DADABE" ? " selected" : "";
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"""<wa-option value="{val}"{selected}>{val}</wa-option>""");
            }
            foreach (var t in tunings.ListAll())
            {
                var spec = WebUtility.HtmlEncode(string.Join(",", t.Strings));
                var name = WebUtility.HtmlEncode(t.Name);
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"""<wa-option value="{spec}" data-name="{name}">{name}</wa-option>""");
            }
            return Results.Content(sb.ToString(), "text/html");
        });

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
