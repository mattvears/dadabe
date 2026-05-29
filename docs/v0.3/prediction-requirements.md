# v0.3 — Prediction requirements (context, filters, and responses)

Purpose: capture requirements for next-chord prediction features so v0.3 documentation clearly records the contracts and non-implementation expectations. This is documentation-only: no code changes are made in this task.

Overview

- Goal: define conservative, testable contracts that a NextChordPredictor implementation can honor. Keep shapes additive and optional so early adopters or alternative backends can implement partial behavior.

Key artifacts to document (for future implementation)

1) Prediction request

- Reuse NextChordPredictionRequestDto (Progression context + PredictionFilterDto[] filters + maxResults).
- Context: optional ProgressionDto advising the predictor of prior chords and tempo. Implementations may use last-N chords.
- Filters: typed array (byChord, byScale, byMode, byQuality, custom). Each filter has `type` and optional `params` object. Filters may be treated as hard excludes or soft boosts — implementation choice, but behavior must be documented per backend.

1) Prediction response schema (required)

- File: schemas/prediction-result.schema.json (additive placeholder)
- Minimal shape:
  - result: array of objects { "chord": string, "score": number, "reasons": string[] }
  - metadata: { "contextUsed": Progression (optional), "filtersApplied": object }
- Scoring: `score` is a float where higher is better; normalise or document expected range (e.g., 0..1 or arbitrary positive). Choose one and document it in the schema/README. Prefer 0..1 for consumer clarity.

1) Predictor API surface (design note)

- NextChordPredictor should accept NextChordPredictionRequestDto and return a typed PredictionResultDto (align with schemas/prediction-result.schema.json).
- Design as an interface in Dadabe.Core to allow multiple implementations (heuristic, ML-backed, rule-based).
- Document how filters are interpreted and whether they are applied as hard or soft constraints by default.

1) CLI UX (design note)

- Flags to add in future: `--predict --input <file.json>` (reads a JSON request and writes JSON responses to stdout or file) and `--validate-schema` (validates request/response against schema and exits non-zero on failure).
- Provide examples under docs/v0.3/examples/prediction-request.json and prediction-response.json.

1) Tests & validation

- Add schema-validation tests using JsonSchema.Net to assert all example requests/responses validate against their schemas.
- Add unit tests that exercise: request round-trip, response shape round-trip, and a small integration test that verifies filters are parsed into filter DTOs.
- Maintain memoization & deterministic emission invariants in tests when prediction logic later plugs into generation.

1) CI

- Add a CI job/step that validates docs/v0.3/examples/*.json against the schemas on PRs.
- Fail the job if any example fails schema validation.

1) Backwards compatibility and extension rules

- Keep all new schema fields optional when possible.
- For breaking changes, increment `schemaVersion` in the envelope and record migration notes in docs/v{version}/source.

Documentation expectations

- Add examples (request + response) in docs/v0.3/examples/.
- Add a short section in docs/v0.3/contracts.md referencing prediction-requirements.md and linking to schema files.
- When implementing: document exactly whether filters are hard or soft by default for that predictor implementation.

Notes

- This file is intentionally prescriptive about shapes but permissive about interpretation (hard vs soft filters). Implementation decisions go in the NextChordPredictor docs when implemented.

(End of requirements; no code changes performed.)
