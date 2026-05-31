# Dadabe v0.4 — Design

## Overview

v0.4 makes the prediction pipeline operational and adds slash chord parsing. The
infrastructure (DTOs, schemas, predictor) was scaffolded in v0.2/v0.3; this release
wires it together and ships it through a new `predict` subcommand.

---

## 1. `predict` subcommand (D21)

**Command**: `dadabe predict --input <file.json> [--out <file.json>] [--pretty] [--validate-schema]`

`--input` is required. There is no positional argument and no stdin mode (keeps
parsing unambiguous). All other options are shared with the existing subcommands.

**Request file** is a JSON object matching `schemas/prediction.schema.json`. It must
include a `chord` field (the chord to predict from). `context` and `filters` are
optional.

**Output** is an `Envelope<PredictionResultDto>` with `"command": "predict"`,
`"schemaVersion": "1"`, and the result payload — identical envelope structure to
`voicings`, `tuning`, and `chord`.

### Execution flow

```text
1. Deserialize request JSON → NextChordPredictionRequestDto
2. Parse spec: ChordParser.Parse(request.Chord)
3. Expand spec: ChordExpander.Expand(symbol)
4. Resolve entropy: request.Entropy ?? env.DefaultEntropy (see §2)
5. Call NextChordPredictor.Predict(spec, topN, entropy)
   where topN = request.MaxResults ?? 10
6. Apply hard filters (§3) to the candidate list
7. Map surviving candidates to PredictionCandidateDto (score normalised 0..1)
8. Build PredictionMetadataDto (contextUsed, filtersApplied)
9. Write Envelope<PredictionResultDto>
10. If --validate-schema: validate the written JSON (§5)
```

---

## 2. Prediction request schema update (D22)

`NextChordPredictionRequestDto` gains a required `chord` field and an optional
`entropy` override:

```csharp
public record NextChordPredictionRequestDto(
    string Chord,                        // required — chord to predict from
    ProgressionDto? Context = null,      // optional — preceding progression
    PredictionFilterDto[]? Filters = null,
    int? MaxResults = null,
    double? Entropy = null);             // optional — overrides env default
```

`schemas/prediction.schema.json` adds `"chord"` to `required` and its properties:

```json
"chord":      { "type": "string", "description": "Chord symbol to predict from, e.g. \"Dm7\"." },
"maxResults": { "type": "integer", "minimum": 1, "maximum": 50,
                "description": "Maximum candidates returned before filters. Result count may be lower after filters are applied." },
"entropy":    { "type": "number", "minimum": 0.01, "maximum": 10.0,
                "description": "Overrides the environment default entropy for this request." }
```

**Entropy resolution** (Q1): entropy is a property of the environment, not the
request. The CLI sets a default via `--entropy` (same flag as `voicings`, default
0.5). The request's optional `entropy` field overrides it for that call only. This
allows automated callers to vary diversity request-by-request without rebuilding the
environment.

`Context.Chords`, when present, is parsed and echoed in `metadata.contextUsed`. In
v0.4 the predictor does not use context to alter scoring; context-weighted scoring
lands in v0.5 (see §3.1).

---

## 3. Filter semantics (D23)

Filters are applied **after** `NextChordPredictor.Predict` produces its scored
candidate list, in declaration order. The result count may therefore be lower than
`maxResults`. All hard filters are AND-composed.

| Filter type | Semantics | v0.4 |
| --- | --- | --- |
| `byChord` | Hard: keep only candidates where `symbol == params.chord` | Implemented |
| `byQuality` | Hard: keep only candidates whose suffix equals `params.quality` | Implemented |
| `byScale` | Soft: boost candidates whose pitch classes fit the scale | Deferred — emits warning |
| `byMode` | Soft: boost via mode-degree preference | Deferred — emits warning |
| `custom` | Implementation-defined | Deferred — emits warning |

### 3.1 Context-weighted scoring (v0.5 design note, D27)

Resolved (Q2): context chords should influence scoring, but the system must remain
capable of generating harmonically surprising — yet in hindsight valid — results.
The design goal is "deceptive cadences and chromatic mediants should appear, not just
diatonic resolutions."

**Algorithm sketch for v0.5**:

1. **Key inference**: from `context.Chords`, infer the most likely key centre using a
   simple fit: find the major or minor key whose diatonic set covers the most chords
   (ties broken by recency — later chords weighted more). This is heuristic only;
   it does not need to be correct in every case.

2. **Key weight multiplier**: after the quality-family rules produce raw scores,
   multiply each candidate's score by a key-weight factor:
   - Diatonic to inferred key: `×1.4`
   - Non-diatonic but categorically valid (secondary dominant, borrowed chord,
     tritone substitution, chromatic mediant): `×1.0` (unchanged)
   - Unrelated (no recognisable harmonic connection): `×0.7`

3. **Surprise preservation**: the multipliers are mild enough that at moderate entropy
   (≥ 0.4) non-diatonic moves retain meaningful probability. Entropy remains the
   primary control for how adventurous the output is. High entropy → the distribution
   flattens → surprising-but-valid moves surface readily.

4. **Categorisation of non-diatonic moves**: implemented as a lookup from the
   interval between the current chord root and the candidate root, cross-referenced
   with quality family. Examples that get `×1.0` rather than `×0.7`:
   - Semitone above/below the current root (chromatic approach)
   - Tritone away (tritone substitution)
   - Minor third or major third away (chromatic mediant)
   - Any chord whose root is a diatonic degree of the *parallel* mode
     (modal interchange / borrowed chord)

This approach means the system does not always predict the most obvious resolution,
and never predicts *only* diatonic chords. The "surprising but in hindsight obvious"
quality emerges from the combination of key weighting and entropy.

Deferred filter types add a string to `Envelope.Warnings`:
`"Filter type 'byScale' is not implemented in v0.4; it was ignored."`.

If all candidates are eliminated by hard filters, `results` is an empty array (not
an error). Consumers should check `results.length` before use.

**Quality suffix matching** for `byQuality`: compare the suffix component of the
candidate symbol string against `params.quality`. Extraction rule: strip the leading
root (letter + optional `b`/`#`) to get the suffix. Examples: `Gm7` → `m7`, `Bb`
→ `""`, `F#maj7` → `maj7`. Matching is lexical (string equality); notation variants
such as `Maj7` and `maj7` are distinct and will not match each other.

---

## 4. Output DTOs (D24)

New types in `src/Dadabe.Cli/Io/Dtos.cs`:

```csharp
public sealed record PredictionResultDto(
    [property: JsonPropertyName("results")]
    IReadOnlyList<PredictionCandidateDto> Results,
    [property: JsonPropertyName("metadata"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PredictionMetadataDto? Metadata);

public sealed record PredictionCandidateDto(
    [property: JsonPropertyName("chord")]   string Chord,
    [property: JsonPropertyName("score")]   double Score,
    [property: JsonPropertyName("reasons"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Reasons);

public sealed record PredictionMetadataDto(
    [property: JsonPropertyName("contextUsed"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ProgressionDto? ContextUsed,
    [property: JsonPropertyName("filtersApplied"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, object?>? FiltersApplied);
```

`score` in the DTO is the normalised probability value already produced by
`NextChordPredictor.Predict` (softmax output, range 0..1, rounded to 4 decimal
places). No additional normalisation step.

---

## 5. `--validate-schema` (D25)

A new shared option `--validate-schema` (bool, default false) is available on all
subcommands. When set, after the envelope JSON is written:

1. Re-read the written JSON (from file if `--out` was given, from the in-memory
   string otherwise).
2. Determine which schema file to load by subcommand name:
   - `voicings` → `schemas/envelope.schema.json`
   - `predict` → `schemas/prediction-result.schema.json` (wrapping envelope)
   - `tuning` / `chord` → `schemas/envelope.schema.json`
3. Validate using `JsonSchema.Net`. Emit all errors to stderr, one per line.
4. Return exit code `ExitSchemaViolation = 3` if any error is found.

Schema files are loaded from the working directory (`schemas/` subdirectory) at
runtime. If the file is not found, fall back to the embedded resource in the
assembly. This lets CI validate against the checked-in schemas without a special
build step.

---

## 6. Slash chord parsing (D26)

### Grammar change

`ChordGrammar.json` — change `parseRules.rejectSlash` from `true` to `false`. The
field and its handling in `ChordParser` are retained so overlays can still set it to
`true` to restore rejection.

### Parser change

In `ChordParser.TryParse`, after the root is extracted and before quality matching:

```text
if symbol contains '/' at index i (i > 0, i < symbol.Length - 1):
    leftPart  = symbol[..i]   // chord above the slash
    rightPart = symbol[(i+1)..] // bass note below
    parse leftPart normally → gets root, quality, etc.
    parse rightPart as a Note (letter + optional accidental, no quality suffix)
    if rightPart parse fails → error: "Bass note '{rightPart}' is not a valid note."
    set chordSymbol.BassNote = parsed bass note
```

Only the first `/` is treated as a slash delimiter. `C/E/G` is rejected with a
descriptive error.

### `ChordSymbol` change

```csharp
public sealed record ChordSymbol(
    Note Root,
    string Quality,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> Alterations,
    Note? BassNote = null)        // new — null if not a slash chord
    : IContentHashable
{
    public ContentHash ContentHash => ContentHash.FromCanonical(
        Namespaces.ChordSymbol, 1,
        Canonical.Bytes(Root),
        Canonical.String(Quality),
        Canonical.StringList(Extensions),
        Canonical.StringList(Alterations),
        BassNote.HasValue ? Canonical.Bytes(BassNote.Value) : Array.Empty<byte>());
}
```

`BassNote` is included in the hash when non-null. `C` and `C/E` produce distinct
content hashes.

### `ChordDto` change

```csharp
public sealed record ChordDto(
    // ... existing fields ...
    [property: JsonPropertyName("bassNote"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BassNote);   // e.g. "E" for C/E — null when not a slash chord
```

The bass note is emitted as a plain note name string (same spelling convention as
the root: letter + accidental symbol if needed). The `voicings` subcommand does not
filter or sort by bass note in v0.4; it is informational only. Actual bass-note
enforcement in voicing search is deferred. The `predict` subcommand similarly ignores
`BassNote` in v0.4: `C` and `C/E` produce identical prediction results.

### Test changes

- Remove `[Skip]` from the existing slash chord rejection test — rename it to assert
  that `C/G` now **succeeds** and `ChordSymbol.BassNote` is `G`.
- Add: bad bass token `C/xyz` throws `FormatException`.
- Add: `C/E/G` (double slash) throws `FormatException`.
- Existing `Slash_chords_rejected_in_v01` test is removed (behaviour changed).
  Add a new test `Slash_chord_rejection_survives_overlay` that loads a grammar with
  `rejectSlash: true` and verifies rejection still works.

---

## 7. Example files

`docs/v0.4/examples/prediction-request.json`:

```json
{
  "chord": "Dm7",
  "context": { "chords": ["Cmaj7", "Am7", "Dm7"], "tempo": 120 },
  "filters": [
    { "type": "byQuality", "params": { "quality": "7" } }
  ],
  "maxResults": 5
}
```

`docs/v0.4/examples/prediction-response.json`:

```json
{
  "tool": "dadabe",
  "version": "0.4.0",
  "schemaVersion": "1",
  "command": "predict",
  "input": { "chord": "Dm7" },
  "data": {
    "results": [
      { "chord": "G7",  "score": 0.4821 },
      { "chord": "Bb7", "score": 0.2314 },
      { "chord": "A7",  "score": 0.1703 }
    ],
    "metadata": {
      "contextUsed": { "chords": ["Cmaj7", "Am7", "Dm7"], "tempo": 120 },
      "filtersApplied": { "byQuality": { "quality": "7" } }
    }
  },
  "warnings": []
}
```

---

## 8. Decisions resolved for v0.5

- **Entropy** (Q1 — resolved): entropy is an environment property (CLI default `--entropy 0.5`),
  overridable per-request via `request.entropy`. Implemented in v0.4 (§2).
- **Context-weighted scoring** (Q2 — resolved): context chords weight candidates toward the
  implied key, but non-diatonic moves (tritone sub, chromatic mediant, borrowed chord,
  secondary dominant) retain meaningful probability. Entropy remains the primary control
  for how adventurous the output is. See §3.1 (D27) for the full algorithm; implemented
  in v0.5.
- **`--validate-schema` in CI** (Q3 — resolved): add a `--ci` flag in v0.5 that sets
  `--validate-schema` and exits non-zero on any warning. Keeps the default behaviour
  lenient for interactive use.
- **Bass note enforcement in voicing search** (Q4 — deferred beyond v0.5): filter voicings
  to those where the lowest sounded note matches the slash chord's bass. Requires changes
  to `VoicingSearch` and the playability model; revisit when slash chords see real use.
