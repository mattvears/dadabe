using System.Collections.Immutable;
using Dadabe.Cli.Io;
using Dadabe.Core.Chord;
using Dadabe.Fretboard;
using static Dadabe.Cli.Commands.Mappings;
using Environment = Dadabe.Fretboard.Environment;

namespace Dadabe.Cli.Commands;

/// <summary>
/// <c>dadabe voice-lead --chords "Cmaj7 Am7 Dm7 G7" [options]</c>:
/// finds the sequence of voicings across a progression that minimises total
/// fret travel, using backward-DP + A* search (D28, D29).
/// </summary>
public static class VoiceLeadCommand
{
    public static void Run(
        Environment env,
        string chordsRaw,
        string tuningName,
        int solutions,
        double minComfort,
        bool validateSchema,
        bool requireRoot = false)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(chordsRaw);

        // 1. Parse chord list (whitespace-separated).
        var chordSymbols = chordsRaw
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (chordSymbols.Length == 0)
            throw new FormatException("--chords requires at least one chord symbol.");

        var parser = new ChordParser(env.Catalogs.ChordGrammar);
        var expander = new ChordExpander(env.Catalogs.ChordGrammar);
        var tuning = VoicingsCommand.ResolveTuning(env, tuningName);

        // 2. For each chord, run VoicingSearch and collect candidates.
        var voicingsPerChord = new ImmutableArray<Voicing>[chordSymbols.Length];
        for (int i = 0; i < chordSymbols.Length; i++)
        {
            var symbol = parser.Parse(chordSymbols[i]);
            var spec = expander.Expand(symbol);
            var set = VoicingSearch.Search(
                spec, tuning, env.HandModel, SearchParams.Default with { RequireRoot = requireRoot },
                env.Catalogs.VoicingCategories, version: env.Version);

            var voicings = set.Voicings;
            if (minComfort > 0.0)
                voicings = voicings.Where(v => v.Comfort >= minComfort).ToImmutableArray();

            if (voicings.Length == 0)
                throw new FormatException(
                    $"Chord '{chordSymbols[i]}' has no playable voicings" +
                    (minComfort > 0.0 ? $" above min-comfort {minComfort:F2}" : "") + ".");

            voicingsPerChord[i] = voicings;
        }

        // 3. Solve.
        int maxSolutions = Math.Clamp(solutions, 1, 10);
        var solved = VoiceLeadSolver.Solve(voicingsPerChord, maxSolutions);

        // 4. Build DTOs.
        var solutionDtos = solved.Select(sol =>
        {
            var stepDtos = sol.Steps.Select(step =>
            {
                VoiceLeadingTransitionDto? transDto = null;
                if (step.TransitionDistance is int td)
                {
                    // Find the transition from the previous step.
                    var stepIdx = sol.Steps.ToList().IndexOf(step);
                    var prevVoicing = sol.Steps[stepIdx - 1].Voicing;
                    var tr = VoiceLeadSolver.BuildTransition(prevVoicing, step.Voicing);
                    transDto = new VoiceLeadingTransitionDto(
                        TotalDistance: tr.TotalDistance,
                        Moves: tr.Moves
                            .Select(m => new VoiceMoveDto(m.StringIndex, m.FromFret, m.ToFret, m.Distance))
                            .ToList());
                }
                return new VoiceLeadingStepDto(
                    Chord: step.Voicing.ChordSpec.Root.ToString() + step.Voicing.ChordSpec.Quality,
                    Voicing: ToDto(step.Voicing),
                    Transition: transDto);
            }).ToList();

            return new VoiceLeadingSolutionDto(sol.TotalDistance, stepDtos);
        }).ToList();

        var payload = new VoiceLeadingResultDto(
            Chords: chordSymbols.ToList(),
            Tuning: tuning.Name,
            Solutions: solutionDtos);

        var envelope = new Envelope<VoiceLeadingResultDto>(
            Tool: JsonEnvelope.ToolName,
            Version: JsonEnvelope.ToolVersion,
            SchemaVersion: JsonEnvelope.SchemaVersion,
            Command: "voice-lead",
            Input: new InputDto(Chord: chordsRaw, Tuning: tuningName, HandModel: null),
            Data: payload,
            Warnings: Array.Empty<string>());

        JsonEnvelope.Write(envelope, env.Output);

        if (validateSchema)
        {
            JsonEnvelope.ValidateSchema(envelope, "voice-lead", env.WorkingDirectory, env.Output);
        }
    }
}
