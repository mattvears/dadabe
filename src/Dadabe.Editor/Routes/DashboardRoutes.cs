using Dadabe.Editor.Services;
using Dadabe.Editor.Slices;

namespace Dadabe.Editor.Routes;

public static class DashboardRoutes
{
    public static void MapDashboardRoutes(this WebApplication app)
    {
        app.MapGet("/dashboard", (
            ProgressionService progressions,
            PredictionService predictions,
            TuningService tunings,
            ReferenceService reference) =>
        {
            var model = new DashboardModel(
                progressions.ListAll().Count,
                predictions.ListAll().Count,
                tunings.ListAll().Count,
                reference.ListCadences().Count + reference.ListModes().Count + reference.ListScales().Count);

            return Results.RazorSlice<Dashboard, DashboardModel>(model);
        });
    }
}
