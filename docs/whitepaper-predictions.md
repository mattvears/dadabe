# Whitepaper: Building Rich Test Data for Next-Chord Predictions

## 1. What "prediction" test data represents

A *prediction* in Dadabe is the answer to "given this chord (and optionally the
progression that led to it), what's likely to come next, and how confident is
the model?" The `predict` subcommand (`src/Dadabe.Cli/Commands/PredictCommand.cs:14`)
wraps `NextChordPredictor.Predict` (`src/Dadabe.Core/Chord/NextChordPredictor.cs:27`)
— a rules-based, softmax-scored harmony engine — in the standard envelope and
applies post-hoc filters.

Test data here means two related artefacts:

- **Request fixtures** (`schemas/prediction.schema.json`): chord + optional
  context progression + filters + tuning knobs (`maxResults`, `entropy`).
- **Result fixtures** (`schemas/prediction-result.schema.json`): the scored
  candidate list plus metadata describing what context/filters were actually
  applied.

Rich coverage matters because the predictor's behaviour bifurcates sharply on
several axes — context present vs. absent, entropy extremes, chord-quality
family, filter type — and a thin "just send `Cmaj7`" fixture set would never
exercise the key-inference path, the filter-elimination paths, the
warning-emission paths, or the entropy-driven score-shape differences that the
schema and UI both depend on.

## 2. Schema shape: what a valid request/result must contain

### 2.1 Request — `schemas/prediction.schema.json`

| Field | Type / constraint | Notes |
|---|---|---|
| `chord` | string, **required** | e.g. `"Dm7"` (`schemas/prediction.schema.json:7`) |
| `context` | `$ref` → `schemas/progression.schema.json` | object with required `chords: string[]` and optional `tempo: integer ≥ 1` (`schemas/prediction.schema.json:8`, `schemas/progression.schema.json:7-9`) |
| `filters` | array of `{ type, params? }` | `type` enum is `byChord \| byScale \| byMode \| byQuality \| custom`; `params` is a free-form object (`schemas/prediction.schema.json:14-15`) |
| `maxResults` | integer, `1..50` | "candidates returned **before** filters" — result count can shrink after filtering (`schemas/prediction.schema.json:21-22`) |
| `entropy` | number, `0.01..10.0` | per-request override of the environment default (`schemas/prediction.schema.json:23-24`) |

`additionalProperties: false` (`schemas/prediction.schema.json:27`) — fixtures
must not include stray top-level keys (e.g. don't add `tuning` here; that
belongs to the voicings schema).

### 2.2 Result — `schemas/prediction-result.schema.json`

The result is wrapped in the shared envelope (`tool`, `version`, `schemaVersion
= "1"`, `command = "predict"`, `input`, `data`, `warnings` — all required,
`schemas/prediction-result.schema.json:7`). Inside `data`:

- `results` (required): array of `{ chord, score, reasons? }`
  - `score`: number `0..1` — "normalised … higher = more likely"
    (`schemas/prediction-result.schema.json:6,29`)
  - `reasons`: optional string array — **schema allows it, but the current
    code never populates it** (see §3.2) — worth fixturing both ways
- `metadata` (optional, but if present `additionalProperties: false`):
  - `contextUsed.chords` (required if `contextUsed` present) + optional
    `tempo: integer ≥ 1` (`schemas/prediction-result.schema.json:38-46`)
  - `filtersApplied`: free-form object keyed by filter type
    (`schemas/prediction-result.schema.json:47`)

Note the result's `data` object is `additionalProperties: false` with only
`results` required (`schemas/prediction-result.schema.json:18-19`) — `metadata`
is entirely optional, so a minimal valid result can omit it altogether.

## 3. How the code consumes/produces this data

### 3.1 Request → candidate generation pipeline

`PredictCommand.Run` (`src/Dadabe.Cli/Commands/PredictCommand.cs:23`):

1. Rejects empty/whitespace `chord` (`PredictCommand.cs:37-40`).
2. Parses & expands the chord into a `ChordSpec` (`PredictCommand.cs:43-46`).
3. `topN = request.MaxResults ?? 10`; `effectiveEntropy = request.Entropy ?? entropy`
   (the CLI's `--entropy`, default `0.5`, see `Program.cs:50`) — test data should
   exercise both "request overrides" and "request omits, env default applies"
   (`PredictCommand.cs:49-50`).
4. Context chords are parsed leniently — `parser.TryParse`, **silently skipping
   unparsable entries** rather than failing the whole request
   (`PredictCommand.cs:57-61`). A context with one bad symbol among good ones is
   a useful edge case.
5. Calls `NextChordPredictor.Predict(spec, topN, effectiveEntropy, contextSpecs)`
   (`PredictCommand.cs:64`).
6. Filters are applied **in declaration order** post-prediction
   (`PredictCommand.cs:68`, `99-140`).
7. Metadata is built only `if (hasContext || hasFilters)` — otherwise `null`
   and omitted from output via `JsonIgnoreCondition.WhenWritingNull`
   (`PredictCommand.cs:144-146`, `Dtos.cs:169-170`).

### 3.2 The prediction algorithm — what shapes the scores

`NextChordPredictor.Predict` (`src/Dadabe.Core/Chord/NextChordPredictor.cs:27`):

- `topN <= 0` short-circuits to an **empty list** — a legitimate, schema-valid
  "no results" shape (`NextChordPredictor.cs:33`).
- Candidates come from a **per-quality-family lookup table**
  `GetProgressions` (`NextChordPredictor.cs:99-181`), keyed by
  `ClassifyQuality` (`NextChordPredictor.cs:183-195`). Each family has a fixed,
  small candidate set (5–7 entries) with hand-tuned raw scores — e.g. Major
  yields `IV (3.0)`, `V (2.5)`, `vi (2.0)`, `ii (2.0)`, `bVII (1.5)`, `iii
  (1.0)`, `Imaj7 (1.0)` (`NextChordPredictor.cs:102-111`). **This means the
  candidate set for a given root+family is deterministic** — great for
  asserting exact symbols/ordering in fixtures.
- `entropy` is clamped to `[0.01, 10.0]` and used as a softmax temperature
  divisor *before* context weighting (`NextChordPredictor.cs:39-40,72-73`).
  Low entropy → sharply peaked distribution (top candidate dominates); high
  entropy → flat distribution. The CLI test
  `tests/Dadabe.Cli.Tests/PredictCommandTests.cs:148-159` asserts
  `lowTop > highTop` for `0.01` vs `10.0` — fixtures at both extremes plus a
  mid value (e.g. `0.5`, the default) are the minimum spread worth encoding.
- **Context weighting (D33)** only kicks in when `context is { Count: > 0 }`
  *and* `KeyInference.InferKey` succeeds (`NextChordPredictor.cs:43-46`).
  `InferKey` requires ≥ 50% of context chords' roots to be diatonic to the
  best-scoring key, with **recency weighting** (`weight = 1 + 0.2 *
  distFromEnd`) — later chords count more
  (`src/Dadabe.Core/Chord/KeyInference.cs:46-50,57-69`). A 1-chord context, a
  context that fails the 50% threshold, and a context that passes are three
  distinct code paths worth fixturing.
- When key inference succeeds, each raw candidate is reclassified via
  `KeyInference.Classify` into `Diatonic` (×1.4), `ValidNonDiatonic` (×1.0), or
  `Unrelated` (×0.7) (`NextChordPredictor.cs:51-57`,
  `KeyInference.cs:195-200`). `ValidNonDiatonic` covers tritone subs (interval
  6), chromatic mediants (3/4), chromatic approach (1/11), borrowed
  parallel-mode chords, and secondary dominants resolving by P5
  (`KeyInference.cs:151-171`) — each is a distinct harmonic-relationship
  category a "rich" context fixture could be designed to trigger.
- Final scores are produced by a **softmax** (`Softmax`,
  `NextChordPredictor.cs:75-82`) and rounded to 4 decimals
  (`NextChordPredictor.cs:66`); `ContextWeightedPredictionTests.cs:24-30`
  asserts they sum to ≈ 1.0.
- **`reasons` is never populated** by the predictor or the command — DTO
  always passes `Reasons: null` (`PredictCommand.cs:72`). Schema-valid result
  fixtures should mostly omit `reasons`, but one fixture *with* `reasons`
  populated (hand-authored) is valuable to prove the schema/UI tolerate it for
  a future feature.

### 3.3 Filters — three live paths, three deferred

`ApplyFilters` (`PredictCommand.cs:99-140`) handles, **in array order**:

- `byChord` — exact `Ordinal` string match on `c.Symbol` against
  `params.chord` (`PredictCommand.cs:111-118`); a non-existent target chord
  legitimately yields an **empty results array** (asserted by
  `ByChord_filter_all_eliminated_returns_empty_results`,
  `PredictCommandTests.cs:107-118`).
- `byQuality` — matches the quality *suffix* extracted by `ExtractSuffix`
  (strips leading root letter + accidentals, e.g. `"Gm7"` → `"m7"`, `"Bb"` →
  `""`, `"F#maj7"` → `"maj7"`) against `params.quality`
  (`PredictCommand.cs:120-126,187-193`). Test the empty-suffix case (plain
  major triad) explicitly.
- `byScale`, `byMode`, `custom` — **not implemented**; each adds a warning
  `"Filter type '{type}' is not implemented in v0.4; it was ignored."` and
  passes candidates through unchanged (`PredictCommand.cs:128-132`). Any
  unknown `type` string also produces a warning (`PredictCommand.cs:134-136`).
  These are first-class, schema-legal inputs (the enum explicitly allows them,
  `schemas/prediction.schema.json:14`) that *must* be exercised to prove
  warning emission and pass-through behaviour.
- Filters compound: applying `byQuality` then `byChord` (or several of the
  same type) progressively narrows the candidate list — useful for proving
  "filters apply in declaration order."
- `filtersApplied` metadata excludes the three deferred types
  (`PredictCommand.cs:154`) — so a request mixing an implemented filter and a
  deferred one produces metadata containing only the implemented one's params,
  *plus* a warning for the deferred one. That combination is an excellent
  "two things happening at once" fixture.

### 3.4 The Editor's view (`PredictionRoutes.cs` / `PredictionsResult.cshtml`)

The editor (`src/Dadabe.Editor/Routes/PredictionRoutes.cs:60-115`) re-derives
predictions from a saved `PredictionModel` rather than re-reading JSON request
files: it loads context chords from a **linked saved progression** by
`ContextSlug` (`PredictionRoutes.cs:85-99`), defaults `entropy` to `0.5` and
`maxResults` to `10` (`PredictionRoutes.cs:78-79`), and re-implements the same
two filter types inline (`PredictionRoutes.cs:150-179`). The result view
(`src/Dadabe.Editor/Slices/PredictionsResult.cshtml`) renders a context badge
with the chord chain (`Model.ContextChords`, joined by `→`,
`PredictionsResult.cshtml:11-13`), a probability bar + percentage + raw score
per row, and an explicit **empty state** "No candidates survived the filters."
when `Results.Count == 0` (`PredictionsResult.cshtml:41-43`). Each row is
clickable to fetch voicings for that chord/tuning — so realistic chord symbols
matter even in UI-facing fixtures (they get round-tripped into a voicings
lookup).

## 4. Recipe for a rich prediction test dataset

Vary along these independent dimensions and combine them — the cross-product
is what actually stresses the schema *and* the code's branch points:

1. **Chord quality family** (drives the candidate table,
   `NextChordPredictor.cs:99-181`/`ClassifyQuality`): cover at least one
   exemplar per family — `Major` (`C`, `6`), `Maj7` (`Cmaj7`, `Cmaj9`),
   `Minor` (`Am`, `Am6`), `M7` (`Am7`, `AmMaj7`), `Dominant` (`G7`, `G9`),
   `Diminished` (`Bdim`, `Bdim7`, `Bm7b5`), `Augmented` (`Caug`), `Sus`
   (`Csus4`, `Dsus2`). Each family yields a *different, deterministic* raw
   candidate set — perfect for golden-file assertions.
2. **Root/key spread**: don't cluster everything around C — use roots with
   accidentals (`F#`, `Bb`, `Eb`, `Db`) to exercise `PcName` mapping
   (`NextChordPredictor.cs:197-212`) and root parsing in `KeyInference`
   (`KeyInference.cs:173-192`).
3. **Context: absent vs. present vs. inference-failing**:
   - no `context` key at all (the common case — also the only way to get
     `metadata: null`/omitted when there are no filters either);
   - `context` with chords that *clearly* establish a key (e.g. `["F", "G",
     "C"]` before predicting from `C` — recency-weighted toward C major);
   - `context` with chords that *fail* the 50% diatonic-coverage threshold
     (e.g. wildly chromatic chords) so `InferKey` returns `null` and weighting
     is skipped — same code path as "no context" but exercised via a non-empty
     array;
   - `context` containing **one unparsable chord symbol** (e.g. `"Hmaj"`)
     mixed with valid ones, to hit the `TryParse`-skip branch
     (`PredictCommand.cs:59-61`);
   - single-chord context (minimal valid array, still subject to the 50%
     threshold with `maxScore = 1.0`);
   - `tempo` present vs. absent on the context object.
4. **Entropy spread**: `0.01` (schema minimum, sharply peaked), `0.5`
   (environment default), `2.0`–`3.0` (moderate), `10.0` (schema maximum,
   near-flat). Pair low/high entropy with *and without* context to show the
   interaction between temperature scaling and key-relationship multipliers.
   Also test **omitting** `entropy` so the CLI's `--entropy` default applies.
5. **`maxResults` edge values**: `1` (single-candidate result — degenerate
   softmax, sum-to-one trivially satisfied), schema minimum; a mid value like
   `5`–`10`; the schema maximum `50` (likely exceeds the family's raw
   candidate count of 5–7, so the result naturally truncates to however many
   unique symbols `BuildCandidates` produced — a good "fewer results than
   requested" case, `NextChordPredictor.cs:84-96`); and **omitted** (defaults
   to `10`, `PredictCommand.cs:49`).
6. **Filters — type, combination, and elimination behaviour**:
   - no `filters` key (→ `metadata` omitted unless context present);
   - empty `filters: []` array;
   - single `byChord` that matches something vs. matches nothing (empty
     `results`);
   - single `byQuality` with a populated suffix (e.g. `"m7"`, `"7"`) and with
     the **empty suffix** `""` (matches plain triads only);
   - a **deferred** type (`byScale`, `byMode`, `custom`, or an unknown string
     like `"byMood"`) alone — to assert the warning text and pass-through;
   - a **mix**: one implemented + one deferred filter together, to prove
     `filtersApplied` only records the implemented one while a warning is
     still emitted for the deferred one;
   - chained filters of the same/different implemented types that
     progressively narrow the list (order-dependent result).
7. **Edge/degenerate result shapes** (for *result* fixtures, hand-authored or
   captured): `results: []` (all filtered out, or `maxResults: 0`/negative
   passed through to `topN <= 0`); a single-element `results`; results
   containing **tied scores** (possible when two raw-score candidates collapse
   to the same symbol via `seen.Add` dedup, or when extreme entropy flattens
   the softmax to near-equal values — `NextChordPredictor.cs:93,75-82`); a
   result with `reasons` populated on some/all candidates (schema-legal,
   currently code-silent — proves forward compatibility).
8. **Envelope-level variations**: confirm `warnings: []` vs. non-empty;
   confirm `metadata` is entirely absent for the simplest `{ "chord": "C" }`
   request (no context, no filters) — this is the minimal valid result and
   easy to get wrong by always emitting an empty `metadata: {}`.

## 5. Example test cases

### 5.1 Minimal request — no context, no filters, defaults everywhere

Exercises: minimal valid shape, `metadata` entirely omitted, environment
default entropy/topN.

```json
{ "chord": "Cmaj7" }
```

Expected result shape highlights: `data.metadata` absent;
`data.results` has up to 10 entries drawn from the `Maj7` family table
(`NextChordPredictor.cs:112-120`) — e.g. `Fmaj7`, `G7`, `Dm7`, `Am7`, `Em7`,
`Bbmaj7` — each `score` in `[0,1]`, summing to ≈ 1.0.

### 5.2 Context present, key inference succeeds, low entropy, compound filter

Exercises: `contextUsed` echo, diatonic-vs-non-diatonic weighting (×1.4 boost),
sharply peaked low-entropy distribution, an implemented + a deferred filter
together.

```json
{
  "chord": "Dm7",
  "context": { "chords": ["Cmaj7", "Fmaj7", "G7"], "tempo": 96 },
  "entropy": 0.2,
  "maxResults": 8,
  "filters": [
    { "type": "byQuality", "params": { "quality": "7" } },
    { "type": "byMode",    "params": { "mode": "dorian" } }
  ]
}
```

Expected: `warnings` contains `"Filter type 'byMode' is not implemented in
v0.4; it was ignored."`; `data.metadata.filtersApplied` contains only
`{ "byQuality": { "quality": "7" } }` (the `byMode` entry is excluded,
`PredictCommand.cs:154`); surviving candidates all end in `"7"`; among them,
`G7` (V7, diatonic in C major) should outscore `Bb7`/other non-diatonic
dominant-quality candidates because the `["Cmaj7","Fmaj7","G7"]` context
strongly implies C major (mirrors `ContextWeightedPredictionTests.cs:33-50`).

### 5.3 High entropy, context that fails key inference, single result requested

Exercises: near-flat softmax at the schema's entropy ceiling, a non-empty
context that nonetheless fails the ≥50% diatonic-coverage check (so scoring
falls back to the un-weighted path), and the `maxResults: 1` degenerate case.

```json
{
  "chord": "F#7",
  "context": { "chords": ["Bbm7", "Edim7"] },
  "entropy": 9.5,
  "maxResults": 1
}
```

Expected: `data.results` has exactly one entry with `score` close to but not
equal to `1.0` (softmax over a near-flat distribution still normalises to one
survivor); `data.metadata.contextUsed.chords` echoes `["Bbm7","Edim7"]` with no
`tempo` key (omitted, matches `ProgressionDto`'s
`JsonIgnoreCondition.WhenWritingNull`); no `filtersApplied` (no filters were
sent, so `filtersApplied` stays absent even though `metadata` is present
because `context` is present, `PredictCommand.cs:144-146`).

### 5.4 All-eliminated filter + malformed context entry — empty-results edge case

Exercises: `byChord` filter that matches nothing (→ `results: []`, the
"No candidates survived the filters" UI state), and a context array containing
one symbol the parser rejects, silently dropped before key inference.

```json
{
  "chord": "G",
  "context": { "chords": ["C", "Hmaj", "Am"] },
  "filters": [
    { "type": "byChord", "params": { "chord": "Zb13#11" } }
  ],
  "maxResults": 10
}
```

Expected: `data.results` is `[]`; `data.metadata.contextUsed.chords` echoes the
*original* three-element array including the unparsable `"Hmaj"` (the DTO
echoes the request's context verbatim — `PredictCommand.cs:163` passes
`request.Context` straight through — even though only `["C","Am"]` were used
internally for key inference); `data.metadata.filtersApplied` is
`{ "byChord": { "chord": "Zb13#11" } }`; `warnings` is empty (an
elimination-to-zero is not itself a warning condition).

## 6. Summary checklist

- [ ] One fixture per chord-quality family (8 families) at default settings
- [ ] Roots spanning naturals + sharps + flats (not just C-cluster)
- [ ] Context: absent / present-and-inferable / present-but-uninferable /
      contains an unparsable symbol / single-chord / with & without `tempo`
- [ ] Entropy: `0.01`, default (`0.5`), mid (`2`–`3`), `10.0`, and omitted
- [ ] `maxResults`: `1`, mid, `50`, and omitted
- [ ] Filters: none / empty array / `byChord` (hit & miss) / `byQuality`
      (populated & empty suffix) / each deferred type / unknown type / mixed
      implemented+deferred / chained same-type
- [ ] Result-side: empty `results`, single result, tied scores, `reasons`
      populated (hand-authored), `metadata` fully absent vs. partially present
- [ ] Envelope basics on every fixture: `schemaVersion: "1"`, `command:
      "predict"`, `tool: "dadabe"`, `warnings` array always present
