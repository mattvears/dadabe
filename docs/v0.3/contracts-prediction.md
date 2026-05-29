# v0.3 — Next-chord prediction: context & filters contract

Purpose: define a conservative request contract for next-chord prediction that accepts an optional context (progression) and an optional set of filters. This scaffolding allows predictor implementations (NextChordPredictor) to express rules like "only suggest Gm next" or "prefer chords in the current scale/mode" while keeping the contract simple and extensible.

Schema: schemas/prediction.schema.json

Key fields
- context (Progression): optional ProgressionDto providing the preceding chords and metadata (tempo). Implementations should treat `context` as advisory input when scoring or constraining predictions.
- filters: array of filter objects. Each filter requires a `type` and may carry `params`.
- maxResults: number of predictions to return (1..100)

Suggested filter types and `params`
- byChord
  - params: { "chord": "Gm" }
  - Meaning: only emit predictions equal to the specified chord (exact match) or treat as a strong boost depending on implementation.

- byScale
  - params: { "scale": "Dorian" }
  - Meaning: prefer chords whose pitch classes fit the named scale; implementations may consult `schemas/scale.schema.json` or runtime catalogs.

- byMode
  - params: { "mode": "Dorian" }
  - Meaning: similar to byScale but uses mode-specific rules (degree-focused suggestions).

- byQuality
  - params: { "quality": "minor" }
  - Meaning: only or preferentially suggest chords matching quality.

- custom
  - params: free-form { "expr": "function(chord) => boolean" } or other opaque metadata for backend plugins.

Implementation notes
- Filters are declarative constraints; predictors may treat them as hard filters (exclude non-matching results) or soft preferences (boost matching results). The contract intentionally leaves that decision to the implementation to support varied backends.

- Context usage: ProgressionDto should be a first-class input to the predictor. Predictors may use the last N chords, harmonic function, or analysis of voice-leading to produce suggestions. Document how context is consumed in the NextChordPredictor implementation.

- Backward compatibility: keep schema additions additive. If a filter requires more complex structure later, introduce a named `type` and new `params` shape rather than changing existing types.

- Tests: add unit tests that serialize/deserialize the prediction request and tests that assert filter parsing logic in the predictor (when implemented).

Examples

1) Predict next chord only if it's G minor

{
  "filters": [ { "type": "byChord", "params": { "chord": "Gm" } } ],
  "maxResults": 5
}

2) Provide context progression and prefer chords in Dorian

{
  "context": { "chords": ["Dm7", "G7"], "tempo": 100 },
  "filters": [ { "type": "byMode", "params": { "mode": "Dorian" } } ],
  "maxResults": 10
}

Placeholders
- Predictors should accept NextChordPredictionRequestDto and return a typed result (TBD). Keep result shape stable and schema-validated in a future iteration.
