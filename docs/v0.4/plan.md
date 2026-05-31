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

- [ ] Add required `chord` field (string) to `NextChordPredictionRequestDto`; update
      `schemas/prediction.schema.json`.
- [ ] `PredictionResultDto` / `PredictionCandidateDto` / `PredictionMetadataDto` in
      `src/Dadabe.Cli/Io/Dtos.cs` — match `schemas/prediction-result.schema.json`.
- [ ] `PredictCommand.cs` — new static class in `src/Dadabe.Cli/Commands/`.
- [ ] Wire `predict` subcommand in `Program.cs` (`--input`, `--out`, `--pretty`,
      `--validate-schema`).
- [ ] Filter application in predictor: `byChord` (hard) and `byQuality` (hard); deferred
      types emit a warning string into `Envelope.Warnings`.

### Schema validation

- [ ] `--validate-schema` option available on all subcommands.
- [ ] Embed schema files as resources in `Dadabe.Cli.csproj`; load via
      `Assembly.GetManifestResourceStream`.
- [ ] Exit code 3 on schema violation (new constant `ExitSchemaViolation`).

### Slash chord parsing

- [ ] Set `rejectSlash: false` as the default in `ChordGrammar.json`; keep the flag so
      overlays can restore rejection.
- [ ] Update `ChordParser.cs`: detect `/`, split, parse bass note (`Letter` + optional
      accidental); reject if bass token is unparseable.
- [ ] Add `Note? BassNote` to `ChordSymbol`; include it in `ContentHash` when non-null.
- [ ] Add `bassNote` (nullable, `JsonIgnore` when null) to `ChordDto` in `Dtos.cs`.
- [ ] Remove `Skip` from slash chord tests in `ChordParserTests`; add bass-note assertion;
      keep existing rejection tests removed (or rephrased for overlay case).

### Tests

- [ ] `PredictCommandTests` — round-trip: request file → predict → result envelope shape.
- [ ] Filter-wiring tests: `byChord` hard filter; `byQuality` hard filter; deferred type
      adds warning.
- [ ] Schema validation tests: valid envelope passes; deliberately-invalid envelope exits 3.
- [ ] Slash chord tests: `C/E` → root C, bass E; `Fmaj7/A` → root F, bass A; bad bass
      token throws.

### Docs

- [ ] `docs/v0.4/examples/prediction-request.json` and `prediction-response.json`.
- [ ] `README.md` — add `predict` subcommand entry and slash chord notation note.
- [ ] `CHANGELOG.md` — v0.4 entry.
- [ ] `docs/todo.md` — move v0.4 items to completed; add v0.5 deferred section.
- [ ] Tag release v0.4 when ready.

## Files to create / modify

| File | Action |
|---|---|
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
