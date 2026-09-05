using Dadabe.Core.Chord;
using Dadabe.Editor.Routes;
using Dadabe.Editor.Services;
using Dadabe.Fretboard;

var builder = WebApplication.CreateBuilder(args);

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
