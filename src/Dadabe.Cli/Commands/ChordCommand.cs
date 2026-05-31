using Dadabe.Cli.Io;
using Dadabe.Core.Chord;
using Dadabe.Fretboard;
using static Dadabe.Cli.Commands.Mappings;
using Environment = Dadabe.Fretboard.Environment;

namespace Dadabe.Cli.Commands;

/// <summary>
/// <c>dadabe chord &lt;symbol&gt;</c>: parse and expand a chord symbol,
/// emit the resolved root, quality, modifiers, spelled pitch classes, and
/// required-tone set as JSON.
/// </summary>
public static class ChordCommand
{
    public static void Run(Environment env, string chordSymbol, bool validateSchema = false)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(chordSymbol);

        var parser = new ChordParser(env.Catalogs.ChordGrammar);
        var expander = new ChordExpander(env.Catalogs.ChordGrammar);

        var symbol = parser.Parse(chordSymbol);
        var spec = expander.Expand(symbol);

        var envelope = new Envelope<ChordPayload>(
            Tool: JsonEnvelope.ToolName,
            Version: JsonEnvelope.ToolVersion,
            SchemaVersion: JsonEnvelope.SchemaVersion,
            Command: "chord",
            Input: JsonEnvelope.BuildInput(env.HandModel, chord: chordSymbol),
            Data: ToChordPayload(spec, symbol, chordSymbol),
            Warnings: Array.Empty<string>());

        JsonEnvelope.Write(envelope, env.Output);

        if (validateSchema)
        {
            JsonEnvelope.ValidateSchema(envelope, "chord", env.WorkingDirectory, env.Output);
        }
    }
}
