# Dadabe v0.4 — Prediction pipeline

## Overview

v0.4 makes the prediction pipeline fully operational. The new `predict` subcommand
reads a JSON request, invokes `NextChordPredictor` with optional context and filters,
and writes a schema-valid envelope. Slash chord parsing is also added, closing the
oldest known parser gap.

No progression engine, no rendering, no persistent memo backends. Those remain in
`docs/future.md`.

## Goals

- Ship a standalone `dadabe predict --input <file.json>` subcommand.
- Wire `NextChordPredictionRequestDto` (chord, context, filters) through to the predictor.
- Implement `byChord` and `byQuality` hard filters; document deferred filter types.
- Add `--validate-schema` flag: post-write JSON schema validation with non-zero exit on failure.
- Implement slash chord parsing (`C/E`, `Cmaj7/E`) in `ChordParser`.
- All changes additive; no breaking changes to existing envelope schemas.

## Non-goals

- No progression engine or voice-leading transitions.
- No new voicing categories.
- No rendering backends (diagram, tab, audio).
- No persistent memo backends (filesystem/SQLite).
- No polychord parsing.
- `byScale` / `byMode` filter logic deferred (warn + ignore if supplied).

## Checklist

### Prediction pipeline

- [x] Add required `chord` field (string) to `NextChordPredictionRequestDto`; update
      `schemas/prediction.schema.json`.
- [x] `PredictionResultDto` / `PredictionCandidateDto` / `PredictionMetadataDto` in
      `src/Dadabe.Cli/Io/Dtos.cs` — match `schemas/prediction-result.schema.json`.
- [x] `PredictCommand.cs` — new static class in `src/Dadabe.Cli/Commands/`.
- [x] Wire `predict` subcommand in `Program.cs` (`--input`, `--out`, `--pretty`,
      `--validate-schema`).
- [x] Filter application in predictor: `byChord` (hard) and `byQuality` (hard); deferred
      types emit a warning string into `Envelope.Warnings`.

### Schema validation

- [x] `--validate-schema` option available on all subcommands.
- [x] Embed schema files as resources in `Dadabe.Cli.csproj`; load via
      `Assembly.GetManifestResourceStream`.
- [x] Exit code 3 on schema violation (new constant `ExitSchemaViolation`).

### Slash chord parsing

- [x] Set `rejectSlash: false` as the default in `ChordGrammar.json`; keep the flag so
      overlays can restore rejection.
- [x] Update `ChordParser.cs`: detect `/`, split, parse bass note (`Letter` + optional
      accidental); reject if bass token is unparseable.
- [x] Add `Note? Bass` to `ChordSymbol`; include it in `ContentHash` when non-null.
- [x] Add `bassNote` (nullable, `JsonIgnore` when null) to `ChordDto` in `Dtos.cs`.
- [x] Slash chord tests in `ChordParserTests`: round-trip, bad bass token, double-slash,
      overlay-rejection.

### Tests

- [x] `PredictCommandTests` — round-trip: request file → predict → result envelope shape.
- [x] Filter-wiring tests: `byChord` hard filter; `byQuality` hard filter; deferred type
      adds warning.
- [x] Schema validation tests: valid envelope passes; deliberately-invalid envelope throws
      `SchemaViolationException` (exit 3 path).
- [x] Slash chord tests: `C/E` → root C, bass E; `Fmaj7/A` → root F, bass A; bad bass
      token throws.

### Docs

- [x] `docs/v0.4/examples/prediction-request.json` and `prediction-response.json`.
- [x] `README.md` — updated to v0.4 current release; `predict` subcommand and slash chord
      notation documented.
- [x] `CHANGELOG.md` — v0.4 entry added.
- [x] `docs/todo.md` — move v0.4 items to completed; add v0.5 deferred section.
- [x] Tag release v0.4 (released as v0.4.1).

## Files to create / modify

| File | Action |
| --- | --- |
| `src/Dadabe.Cli/Commands/PredictCommand.cs` | Create |
| `src/Dadabe.Cli/Io/Dtos.cs` | Add prediction result DTOs; `BassNote` on `ChordDto` |
| `src/Dadabe.Cli/Io/NextChordPredictionRequestDto.cs` | Add `Chord` field |
| `src/Dadabe.Cli/Program.cs` | Add `predict` subcommand; `--validate-schema` |
| `src/Dadabe.Core/Chord/ChordParser.cs` | Slash chord split |
| `src/Dadabe.Core/Chord/ChordSymbol.cs` | Add `Note? BassNote` |
| `src/Dadabe.Core/Chord/ChordGrammar.json` | Set `rejectSlash: false` |
| `schemas/prediction.schema.json` | Add required `chord` field |
| `tests/Dadabe.Cli.Tests/PredictCommandTests.cs` | Create |
| `tests/Dadabe.Core.Tests/Chord/ChordParserTests.cs` | Update slash tests |
| `CHANGELOG.md` | Add v0.4 entry |
| `README.md` | Document `predict` and slash chords |

## Notes

- Preserve content-hash ID invariants. `BassNote` is included in `ChordSymbol.ContentHash`
  only when non-null; the canonical byte form is `[Letter byte, Accidental byte]`.
- `--validate-schema` does not embed schemas at compile time; it reads them from
  `schemas/` relative to the working directory, falling back to embedded resources.
  See design.md §4.
- Keep `voicings` unchanged; its `Envelope<VoicingsPayload>` schema is unaffected by
  the prediction or slash chord work.
