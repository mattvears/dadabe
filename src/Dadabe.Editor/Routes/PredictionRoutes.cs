using Dadabe.Core.Chord;
using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;
using Dadabe.Fretboard;
using Microsoft.AspNetCore.Mvc;

namespace Dadabe.Editor.Routes;

public static class PredictionRoutes
{
    public static void MapPredictionRoutes(this WebApplication app)
    {
        app.MapGet("/predictions", (PredictionService svc) =>
            Results.RazorSlice<PredictionsIndex, PredictionsIndexModel>(
                new PredictionsIndexModel(svc.ListAll())));

        app.MapGet("/api/predictions/new", (ProgressionService progressions) =>
            Results.RazorSlice<PredictionsForm, PredictionFormModel>(
                new PredictionFormModel(null, progressions.ListAll())));

        app.MapGet("/api/predictions/{slug}/edit", (string slug, PredictionService svc, ProgressionService progressions) =>
        {
            var model = svc.Get(slug);
            return model is null
                ? Results.NotFound()
                : Results.RazorSlice<PredictionsForm, PredictionFormModel>(
                    new PredictionFormModel(model, progressions.ListAll()));
        });

        app.MapPost("/api/predictions", async (HttpRequest req, PredictionService svc) =>
        {
            var (model, error) = await ParseForm(req);
            if (model is null) return Results.BadRequest(error);

            var (ok, err) = svc.Create(model);
            if (!ok) return Results.BadRequest(err);

            return Results.RazorSlice<PredictionsList,
                IReadOnlyList<PredictionModel>>(svc.ListAll());
        });

        app.MapPut("/api/predictions/{slug}", async (string slug, HttpRequest req, PredictionService svc) =>
        {
            var (model, error) = await ParseForm(req);
            if (model is null) return Results.BadRequest(error);

            var (ok, err) = svc.Update(slug, model);
            if (!ok) return Results.BadRequest(err);

            return Results.RazorSlice<PredictionsList,
                IReadOnlyList<PredictionModel>>(svc.ListAll());
        });

        app.MapDelete("/api/predictions/{slug}", (string slug, PredictionService svc) =>
        {
            svc.Delete(slug);
            return Results.Ok();
        });

        app.MapPost("/api/predictions/{slug}/run", (
            string slug,
            PredictionService svc,
            [FromServices] ProgressionService progressions,
            [FromServices] Catalogs catalogs,
            [FromServices] ChordParser parser,
            [FromServices] ChordExpander expander) =>
        {
            var model = svc.Get(slug);
            if (model is null)
                return Results.RazorSlice<PredictionsResult, PredictionResultModel>(
                    PredictionResultModel.FromError("?", "Prediction request not found."));

            if (!parser.TryParse(model.Chord, out var symbol, out var parseError))
                return Results.RazorSlice<PredictionsResult, PredictionResultModel>(
                    PredictionResultModel.FromError(model.Chord, $"Invalid chord: {parseError}"));

            var spec = expander.Expand(symbol);
            var topN = model.MaxResults ?? 10;
            var entropy = model.Entropy ?? 0.5;

            // Load context chords from the linked progression (D33).
            List<Dadabe.Core.Chord.ChordSpec>? contextSpecs = null;
            string? contextName = null;
            IReadOnlyList<string>? contextChords = null;
            if (model.ContextSlug is not null)
            {
                var prog = progressions.Get(model.ContextSlug);
                if (prog is not null)
                {
                    contextName = prog.Name;
                    contextChords = prog.Chords;
                    contextSpecs = [];
                    foreach (var cs in prog.Chords)
                    {
                        if (parser.TryParse(cs, out var cSym, out _))
                            contextSpecs.Add(expander.Expand(cSym));
                    }
                }
            }

            var candidates = NextChordPredictor.Predict(spec, topN, entropy, contextSpecs);
            var filtered = ApplyFilters(candidates, model.Filters);

            var results = filtered
                .Select(c => new PredictionCandidate(c.Symbol, c.Probability))
                .ToList();

            var tuningNames = catalogs.Tunings.All
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .Select(t => t.Name)
                .ToList();

            return Results.RazorSlice<PredictionsResult, PredictionResultModel>(
                new PredictionResultModel(model.Chord, results, null, contextName, tuningNames, contextChords));
        });
    }

    private static async Task<(PredictionModel? model, string error)> ParseForm(HttpRequest req)
    {
        var form = await req.ReadFormAsync();
        var name = form["name"].ToString();
        var chord = form["chord"].ToString();
        if (string.IsNullOrWhiteSpace(name)) return (null, "Name is required.");
        if (string.IsNullOrWhiteSpace(chord)) return (null, "Chord is required.");

        var slug = DataStore.ToSlug(name);
        var contextSlug = form["contextSlug"].ToString().NullIfEmpty();
        var maxResults = int.TryParse(form["maxResults"], out var mr) ? mr : (int?)null;
        var entropy = double.TryParse(form["entropy"],
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var e) ? e : (double?)null;

        var filterTypes = form["filterType"].ToList();
        var filterParams = form["filterParam"].ToList();
        var filters = filterTypes
            .Zip(filterParams)
            .Select(pair =>
            {
                var paramKey = pair.First == "byChord" ? "chord" : "quality";
                return new PredictionFilterModel(pair.First!, new Dictionary<string, string>
                {
                    [paramKey] = pair.Second ?? string.Empty,
                });
            })
            .ToList();

        return (new PredictionModel(slug, name, chord, contextSlug, filters, maxResults, entropy), string.Empty);
    }

    private static IReadOnlyList<Dadabe.Core.Chord.NextChordCandidate> ApplyFilters(
        IReadOnlyList<Dadabe.Core.Chord.NextChordCandidate> candidates,
        List<PredictionFilterModel> filters)
    {
        var result = candidates.ToList();
        foreach (var f in filters)
        {
            switch (f.Type)
            {
                case "byChord":
                    if (f.Params.TryGetValue("chord", out var targetChord))
                        result = result.Where(c =>
                            string.Equals(c.Symbol, targetChord, StringComparison.Ordinal)).ToList();
                    break;

                case "byQuality":
                    if (f.Params.TryGetValue("quality", out var quality))
                        result = result.Where(c => ExtractSuffix(c.Symbol) == quality).ToList();
                    break;
            }
        }
        return result;
    }

    private static string ExtractSuffix(string symbol)
    {
        var i = 1;
        while (i < symbol.Length && symbol[i] is 'b' or '#') { i++; }
        return symbol[i..];
    }
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
