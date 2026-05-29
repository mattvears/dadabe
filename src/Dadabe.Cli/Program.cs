using System.Collections.Immutable;
using System.CommandLine;
using Dadabe.Cli.Commands;
using Dadabe.Fretboard;
using DadabeEnv = Dadabe.Fretboard.Environment;

namespace Dadabe.Cli;

internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitBadInput = 1;
    private const int ExitUnexpected = 2;

    public static int Main(string[] args)
    {
        var root = BuildRootCommand();
        return root.Parse(args).Invoke();
    }

    public static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("Dadabe — emit playable chord voicings as JSON.");

        // Shared options.
        var outOption = new Option<string?>("--out") { Description = "Write JSON to file (default: stdout)." };
        var prettyOption = new Option<bool>("--pretty") { Description = "Pretty-print JSON." };

        // --- voicings ---
        var chordArg = new Argument<string>("chord") { Description = "Chord symbol, e.g. \"Cmaj7\", \"G7b9\", \"F#m11\"." };
        var tuningOption = new Option<string>("--tuning")
        {
            Description = "Named tuning or comma spec (default: DADABE).",
            DefaultValueFactory = _ => "DADABE",
        };
        var fretsOption = new Option<int>("--frets") { Description = "Max fret (default: 15).", DefaultValueFactory = _ => 15 };
        var spanOption = new Option<int>("--span") { Description = "Max fret span (default: 4).", DefaultValueFactory = _ => 4 };
        var minStringsOption = new Option<int>("--min-strings") { Description = "Min sounded strings (default: 3).", DefaultValueFactory = _ => 3 };
        var maxStringsOption = new Option<int>("--max-strings") { Description = "Max sounded strings (default: 6).", DefaultValueFactory = _ => 6 };
        var allowOpenOption = new Option<bool>("--allow-open") { Description = "Allow open strings (default: true).", DefaultValueFactory = _ => true };
        var allowBarreOption = new Option<bool>("--allow-barre") { Description = "Allow barre voicings (default: true).", DefaultValueFactory = _ => true };
        var allowThumbOption = new Option<bool>("--allow-thumb") { Description = "Allow thumb-over fretting (default: false).", DefaultValueFactory = _ => false };
        var categoriesOption = new Option<string?>("--categories") { Description = "Restrict to comma-separated category list." };
        var limitOption = new Option<int>("--limit") { Description = "Cap on returned voicings (default: 100).", DefaultValueFactory = _ => 100 };
        var handProfileOption = new Option<string>("--hand-profile") { Description = "Named hand profile (default: Default).", DefaultValueFactory = _ => "Default" };
        var topNOption = new Option<int>("--top-n") { Description = "Number of next-chord candidates to predict (0 = disabled).", DefaultValueFactory = _ => 0 };
        var entropyOption = new Option<double>("--entropy") { Description = "Prediction diversity: lower = more confident, higher = more exploratory (default: 0.5).", DefaultValueFactory = _ => 0.5 };

        var voicings = new Command("voicings", "Emit all playable voicings of a chord on a tuning.")
        {
            chordArg,
            tuningOption,
            fretsOption,
            spanOption,
            minStringsOption,
            maxStringsOption,
            allowOpenOption,
            allowBarreOption,
            allowThumbOption,
            categoriesOption,
            limitOption,
            handProfileOption,
            topNOption,
            entropyOption,
            outOption,
            prettyOption,
        };
        voicings.SetAction(parse => SafeRun(() =>
        {
            var env = BuildEnvironment(parse, outOption, prettyOption, handProfileOption);
            var p = new SearchParams(
                MaxFret: parse.GetValue(fretsOption),
                MaxSpan: parse.GetValue(spanOption),
                MinStrings: parse.GetValue(minStringsOption),
                MaxStrings: parse.GetValue(maxStringsOption),
                AllowOpen: parse.GetValue(allowOpenOption),
                AllowBarre: parse.GetValue(allowBarreOption),
                AllowThumb: parse.GetValue(allowThumbOption),
                Categories: SplitCsv(parse.GetValue(categoriesOption)));
            VoicingsCommand.Run(env,
                chordSymbol: parse.GetRequiredValue(chordArg),
                tuningName: parse.GetValue(tuningOption)!,
                searchParams: p,
                limit: parse.GetValue(limitOption),
                topN: parse.GetValue(topNOption),
                entropy: parse.GetValue(entropyOption));
        }));

        // --- tuning ---
        var tuningPositionalArg = new Argument<string>("tuning") { Description = "Named tuning or comma spec." };
        var tuning = new Command("tuning", "Resolve a tuning and emit its open-string pitches as JSON.")
        {
            tuningPositionalArg,
            outOption,
            prettyOption,
        };
        tuning.SetAction(parse => SafeRun(() =>
        {
            var env = BuildEnvironment(parse, outOption, prettyOption, handProfileOption: null);
            TuningCommand.Run(env, parse.GetRequiredValue(tuningPositionalArg));
        }));

        // --- chord ---
        var chordPositionalArg = new Argument<string>("symbol") { Description = "Chord symbol." };
        var chord = new Command("chord", "Parse and expand a chord symbol; emit root, quality, pitch classes.")
        {
            chordPositionalArg,
            outOption,
            prettyOption,
        };
        chord.SetAction(parse => SafeRun(() =>
        {
            var env = BuildEnvironment(parse, outOption, prettyOption, handProfileOption: null);
            ChordCommand.Run(env, parse.GetRequiredValue(chordPositionalArg));
        }));

        root.Add(voicings);
        root.Add(tuning);
        root.Add(chord);
        return root;
    }

    private static int SafeRun(Action action)
    {
        try
        {
            action();
            return ExitOk;
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return ExitBadInput;
        }
        catch (KeyNotFoundException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return ExitBadInput;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"unexpected error: {ex.Message}");
            return ExitUnexpected;
        }
    }

    private static DadabeEnv BuildEnvironment(
        System.CommandLine.ParseResult parse,
        Option<string?> outOption,
        Option<bool> prettyOption,
        Option<string>? handProfileOption)
    {
        var cwd = System.Environment.CurrentDirectory;
        var hand = HandModel.Default;
        // Future: resolve hand profile + per-flag overrides. v0.1 ships one profile.
        _ = handProfileOption;
        var output = new OutputTarget(parse.GetValue(outOption), parse.GetValue(prettyOption));
        return DadabeEnv.Build(cwd, hand, output);
    }

    private static ImmutableArray<string> SplitCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) { return ImmutableArray<string>.Empty; }
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToImmutableArray();
    }
}
