using System.Globalization;
using Dadabe.Core.Chord;
using Dadabe.Editor.Routes;
using Dadabe.Editor.Services;
using Dadabe.Fretboard;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, cfg) => cfg
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.HttpLogging", LogEventLevel.Information)
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File("logs/editor-.log", rollingInterval: RollingInterval.Day, formatProvider: CultureInfo.InvariantCulture));

builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders
        | HttpLoggingFields.RequestBody
        | HttpLoggingFields.ResponsePropertiesAndHeaders
        | HttpLoggingFields.ResponseBody;
    o.RequestBodyLogLimit = 16 * 1024;
    o.ResponseBodyLogLimit = 16 * 1024;

    // Only log request bodies (forms) and JS responses — never the HTML page bodies.
    o.MediaTypeOptions.Clear();
    o.MediaTypeOptions.AddText("application/x-www-form-urlencoded");
    o.MediaTypeOptions.AddText("application/javascript");
    o.MediaTypeOptions.AddText("text/javascript");
});

var catalogs = Catalogs.Default();
builder.Services.AddSingleton(catalogs);
builder.Services.AddSingleton(new ChordParser(catalogs.ChordGrammar));
builder.Services.AddSingleton(new ChordExpander(catalogs.ChordGrammar));

builder.Services.AddSingleton<DataStore>();
builder.Services.AddSingleton<ProgressionService>();
builder.Services.AddSingleton<TuningService>();
builder.Services.AddSingleton<PredictionService>();
builder.Services.AddSingleton<ReferenceService>();
builder.Services.AddSingleton<SongService>();

var app = builder.Build();

app.UseHttpLogging();

// StaticFileMiddleware serves via sendfile, which bypasses the stream HttpLogging
// wraps — force it through the normal response stream so app.js bodies get logged.
app.Use(async (context, next) =>
{
    context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(context.Response.Body));
    await next();
});
app.UseStaticFiles();

app.MapGet("/", () => Results.Redirect("/dashboard"));
app.MapDashboardRoutes();
app.MapProgressionRoutes();
app.MapTuningRoutes();
app.MapPredictionRoutes();
app.MapReferenceRoutes();
app.MapVoicingRoutes();
app.MapVoiceLeadRoutes();
app.MapSongRoutes();
app.MapTransformRoutes();

app.Run();
