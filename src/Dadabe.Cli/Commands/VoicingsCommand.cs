using Dadabe.Cli.Io;
using Dadabe.Core.Chord;
using Dadabe.Fretboard;
using static Dadabe.Cli.Commands.Mappings;
using Environment = Dadabe.Fretboard.Environment;
using NextChordPredictor = Dadabe.Core.Chord.NextChordPredictor;

namespace Dadabe.Cli.Commands;

/// <summary>
/// <c>dadabe voicings &lt;chord&gt; [options]</c>: enumerate all musically
/// meaningful playable voicings of <c>chord</c> on <c>tuning</c> within the
/// hand model's geometric envelope, then write them to <see cref="Environment.Output"/>.
/// </summary>
public static class VoicingsCommand
{
    public static void Run(
        Environment env,
        string chordSymbol,
        string tuningName,
        SearchParams searchParams,
        int limit,
        int topN = 0,
        double entropy = 0.5)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(chordSymbol);
        ArgumentNullException.ThrowIfNull(tuningName);
        ArgumentNullException.ThrowIfNull(searchParams);

        var parser = new ChordParser(env.Catalogs.ChordGrammar);
        var expander = new ChordExpander(env.Catalogs.ChordGrammar);

        var symbol = parser.Parse(chordSymbol);
        var spec = expander.Expand(symbol);
        var tuning = ResolveTuning(env, tuningName);

        var set = VoicingSearch.Search(spec, tuning, env.HandModel, searchParams, env.Catalogs.VoicingCategories, version: env.Version);

        var voicings = set.Voicings;
        if (limit > 0 && voicings.Length > limit)
        {
            voicings = voicings[..limit];
        }

        var nextChords = NextChordPredictor.Predict(spec, topN, entropy)
            .Select(c => new NextChordDto(c.Symbol, c.Probability))
            .ToList();

        var payload = new VoicingsPayload(
            Chord: ToDto(spec, chordSymbol),
            Tuning: ToDto(tuning),
            Voicings: voicings.Select(ToDto).ToList(),
            NextChords: nextChords);

        var envelope = new Envelope<VoicingsPayload>(
            Tool: JsonEnvelope.ToolName,
            Version: JsonEnvelope.ToolVersion,
            SchemaVersion: JsonEnvelope.SchemaVersion,
            Command: "voicings",
            Input: JsonEnvelope.BuildInput(env.HandModel, chord: chordSymbol, tuning: tuningName),
            Data: payload,
            Warnings: Array.Empty<string>());

        JsonEnvelope.Write(envelope, env.Output);
    }

    internal static Dadabe.Core.Tuning ResolveTuning(Environment env, string tuningNameOrSpec)
    {
        if (env.Catalogs.Tunings.TryGet(tuningNameOrSpec, out var named))
        {
            return named!;
        }
        if (tuningNameOrSpec.Contains(',', StringComparison.Ordinal))
        {
            return Dadabe.Core.Tuning.ParseSpec(tuningNameOrSpec);
        }
        throw new FormatException($"Unknown tuning '{tuningNameOrSpec}' (not a named tuning and not a comma spec).");
    }
}
