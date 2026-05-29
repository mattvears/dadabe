# Changelog

All notable changes to Dadabe are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [v0.3] — 2026-05-28 — Scaffolding (polish release)

### Added

- `ITransformation` interface placeholder in `Dadabe.Core` — surface for
  future harmonic transformations (tritone sub, modal interchange, etc.).
- `IOutputRenderer` interface placeholder in `Dadabe.Cli.Io` — abstracts the
  JSON sink so future diagram/tab renderers share the same contract.
- `schemas/prediction-result.schema.json` — response shape for next-chord
  prediction (`results[].chord`, `results[].score` in `[0,1]`, optional
  `reasons[]`).
- `schemas/voicing-category.schema.json` — schema for `VoicingCategories.json`
  overlay files; enables third-party category extensions.
- Parser extension TODO comments + `[Skip]` polychord test in
  `ChordParserTests` documenting the desired future split-symbol behaviour.
- `TransitionTests` — content-hash determinism and direction-sensitivity tests
  for `Transition` / `VoiceMove` (D16/D17).
- `MemoBackendTests` — mock persistent-backend scaffold asserting the
  `IMemo<TIn,TOut>` contract for any future filesystem/SQLite implementation
  (D18).
- `VoicingCategoryCatalogTests` — overlay loading, priority replacement, and
  determinism tests for `VoicingCategoryCatalog` (D20).
- CI step: validates `docs/v0.3/examples/*.json` are well-formed JSON on every
  pull request.
- README updated: v0.3 section, full CLI option listing, schemas table.

### Changed

- README Features list indentation fixed (MD007 compliance).
- `CHANGELOG.md` introduced (this file).

### Notes

- No breaking schema changes; all new fields are optional and additive.
- No new CLI commands; all new types are scaffolding for future releases.
- `--predict`, `--validate-schema`, progression engine, and rendering
  backends remain deferred (see `docs/future.md`).

---

## [v0.2] — 2026-05-28 — Next-chord prediction

### Added

- Rules-based `NextChordPredictor` in `Dadabe.Core.Chord`: softmax-scored
  candidates per chord quality family; exposed via `voicings --top-n` /
  `--entropy`.
- `ProgressionDto`, `ScaleDto`, `ModeDto`, `CadenceDto`,
  `PredictionFilterDto`, `NextChordPredictionRequestDto` scaffolding DTOs.
- `schemas/progression.schema.json`, `schemas/prediction.schema.json`,
  `schemas/scale.schema.json`, `schemas/mode.schema.json`,
  `schemas/cadence.schema.json`, `schemas/transformation.schema.json`.
- `ProgressionTests`, `ScaleCadenceModeTests`, `PredictionContractsTests`.
- Expanded `VoicingSearchTests` and `FingeringSolverTests`.
- `docs/v0.3/` design documents: `plan.md`, `contracts.md`,
  `contracts-scales.md`, `contracts-prediction.md`,
  `prediction-requirements.md`, and example JSON files.

---

## [v0.1] — initial release

### Added

- `Dadabe.Core`: `Note`, `Pitch`, `PitchClass`, `Interval`, `Tuning`,
  `TuningCatalog`, chord grammar (`ChordGrammar`, `ChordParser`,
  `ChordExpander`, `ChordSpec`), memoization layer (`IMemo`,
  `InMemoryMemo`, `ContentHash`, `Canonical`).
- `Dadabe.Fretboard`: `VoicingSearch`, `FingeringSolver`, `Classifier`,
  `VoicingCategoryCatalog`, `HandModel`, `Transition` / `VoiceMove` (types
  only), `Playability`, `Reachability`.
- `Dadabe.Cli`: `voicings`, `tuning`, `chord` subcommands with JSON envelope
  output; `--pretty`, `--out`, `--limit`, and all search-control flags.
- JSON schemas for envelope, voicings, tuning, and chord.
- Content-hash IDs (D17): deterministic `id` fields across runs and machines.
- CI: build, format-check, and test on every push/PR.
