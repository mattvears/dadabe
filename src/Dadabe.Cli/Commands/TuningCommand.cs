using Dadabe.Cli.Io;
using Dadabe.Fretboard;
using static Dadabe.Cli.Commands.Mappings;
using Environment = Dadabe.Fretboard.Environment;

namespace Dadabe.Cli.Commands;

/// <summary>
/// <c>dadabe tuning &lt;name|spec&gt;</c>: resolve a named tuning or
/// inline spec, emit the open-string pitches as JSON.
/// </summary>
public static class TuningCommand
{
    public static void Run(Environment env, string tuningNameOrSpec, bool validateSchema = false)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(tuningNameOrSpec);

        var tuning = VoicingsCommand.ResolveTuning(env, tuningNameOrSpec);

        var envelope = new Envelope<TuningPayload>(
            Tool: JsonEnvelope.ToolName,
            Version: JsonEnvelope.ToolVersion,
            SchemaVersion: JsonEnvelope.SchemaVersion,
            Command: "tuning",
            Input: JsonEnvelope.BuildInput(env.HandModel, tuning: tuningNameOrSpec),
            Data: ToTuningPayload(tuning),
            Warnings: Array.Empty<string>());

        JsonEnvelope.Write(envelope, env.Output);

        if (validateSchema)
        {
            JsonEnvelope.ValidateSchema(envelope, "tuning", env.WorkingDirectory, env.Output);
        }
    }
}
