# Whitepaper: Building Rich Test Data for Progressions

## What "progression" test data represents

In Dadabe, a *progression* is simply an ordered list of chord symbols (with an
optional tempo) — `schemas/progression.schema.json:7`. It looks trivial, but it
is the connective tissue between three otherwise-independent subsystems:

- **Voice-leading** (`voice-lead` subcommand): a progression is the *primary
  input* — the sequence of chords whose voicings must be threaded together with
  minimal fret travel (`src/Dadabe.Cli/Commands/VoiceLeadCommand.cs:11`).
- **Next-chord prediction**: a progression is *optional context* that biases
  prediction toward an inferred key (`src/Dadabe.Cli/Io/NextChordPredictionRequestDto.cs:5`,
  `src/Dadabe.Core/Chord/NextChordPredictor.cs:43`).
- **The editor**: progressions are named, persisted entities a user builds up
  chord-by-chord in a dialog (`src/Dadabe.Editor/Slices/ProgressionsForm.cshtml:23`).

Because the schema itself is an intentionally thin "placeholder" (its own
description literally says *"Add fields conservatively"* —
`schemas/progression.schema.json:5`), the real shape constraints that matter for
testing live almost entirely in the **consuming code's behaviour**, not the
schema. Rich progression test data therefore has to be musically meaningful —
varied keys, cadences, chord qualities, lengths, and "edge" harmonic moves —
because that's what actually exercises different code paths in key inference,
prediction weighting, and voice-leading search/solve.

## Schema shape summary

`schemas/progression.schema.json`:
- `chords` (`progression.schema.json:7`): **required**, array of strings (chord
  symbols, no format/pattern constraint at the schema level — validation of
  symbol *syntax* happens downstream via `ChordParser`).
- `tempo` (`progression.schema.json:8`): **optional** integer, `minimum: 1` —
  no upper bound in the schema (the editor UI caps it at 400,
  `src/Dadabe.Editor/Slices/ProgressionsForm.cshtml:20`).
- `additionalProperties: true` (`progression.schema.json:11`) — the schema
  deliberately allows extra fields, so test data can probe forward-compatibility
  (e.g. adding a `key` or `name` field and confirming it round-trips/validates).
- Only `chords` is `required` (`progression.schema.json:10`) — an empty
  `chords: []` array *passes* the JSON Schema (no `minItems`), but the CLI
  rejects it at runtime (see below). This mismatch is itself worth covering.

Two related schemas matter because progressions feed them:
- `schemas/prediction.schema.json:8` — `context` is a `$ref` straight to
  `progression.schema.json`, so any progression payload is valid prediction
  context. `prediction.schema.json:21-24` also bounds `maxResults` (1-50) and
  `entropy` (0.01-10.0), both of which interact with progression-context
  weighting.
- `schemas/voice-lead-result.schema.json:21` — the *result* echoes
  `data.chords` (string array, `minItems: 1`) and requires at least one
  `solutions[].steps` entry (`voice-lead-result.schema.json:33`); test
  progressions should be designed knowing the result schema demands non-empty
  output, so degenerate inputs (empty chord lists) must be rejected *before*
  reaching serialization.

## How the code consumes progressions

### 1. Voice-leading: progression length drives the search topology

`VoiceLeadCommand.Run` (`src/Dadabe.Cli/Commands/VoiceLeadCommand.cs:29`) splits
the raw `--chords` string on whitespace and throws `FormatException` if the
result is empty (`VoiceLeadCommand.cs:31-32`) — so an **empty progression is an
explicit error case** to test, not a schema violation. For each chord it runs a
full `VoicingSearch` (`VoiceLeadCommand.cs:44`) and optionally filters by
`minComfort` (`VoiceLeadCommand.cs:49-50`), throwing if **any single chord in
the progression has zero playable voicings** (`VoiceLeadCommand.cs:52-55`) —
this means a rich dataset needs progressions containing chords that are
*deliberately* hard to voice (exotic qualities/extensions) to exercise that
failure path, as well as ones that are always playable.

`VoiceLeadSolver.Solve` (`src/Dadabe.Fretboard/VoiceLeadSolver.cs:37`) branches
explicitly on **progression length**:
- `n == 0` → returns `[]` (`VoiceLeadSolver.cs:43`)
- `n == 1` → every voicing is its own zero-cost, transition-less solution
  (`VoiceLeadSolver.cs:47-54`) — a **single-chord "progression"** is a real,
  separately-coded case worth a dedicated test fixture.
- `n >= 2` → backward-DP + A* search across `n-1` transition layers
  (`VoiceLeadSolver.cs:56-95`).

The CLI test suite confirms the shape implications: a 2-chord progression
yields exactly one transition (`tests/Dadabe.Cli.Tests/VoiceLeadCommandTests.cs:22`),
a 3-chord one yields exactly two (`VoiceLeadCommandTests.cs:36`), and the first
step always omits `transition` while subsequent ones require it
(`VoiceLeadCommandTests.cs:47-49`). **Repeated/static chords** (e.g. `"Am Am Am
Am"`) are an interesting variant: they should still produce valid transitions
(possibly zero-distance ones) and stress whether the solver/serializer handles
`totalDistance: 0` correctly. `solutions` is clamped to `[1, 10]`
(`VoiceLeadCommand.cs:61`), so test data should also probe `solutions` values
at, below, and above that range together with progressions long/short enough to
actually produce that many distinct paths.

### 2. Key inference: progression *content* — not just length — determines whether a key is found at all

`KeyInference.InferKey` (`src/Dadabe.Core/Chord/KeyInference.cs:25`) is the crux
of what makes progression content "interesting" for testing:

- Returns `null` immediately for an empty context (`KeyInference.cs:27`).
- Scores all 24 major/minor keys by counting how many chord *roots* fall in
  each key's diatonic pitch-class set, with **recency weighting**: a chord at
  distance `d` from the end of the list contributes `1.0 + 0.2*d`
  (`KeyInference.cs:47-49,64-65`). This means **chord order matters** — the same
  multiset of chords in a different order can infer a different key (or none).
- Requires **≥ 50% weighted coverage** to return a result; below that, returns
  `null` (`KeyInference.cs:50`). This is the single most important threshold for
  rich test data: progressions should be deliberately built to sit clearly above
  this threshold (e.g. `Cmaj7 Am7 Dm7 G7`, all roots diatonic to C major — see
  `tests/Dadabe.Core.Tests/Chord/KeyInferenceTests.cs:23`), clearly below it
  (e.g. heavily chromatic progressions with mostly non-diatonic roots), and
  *near* it (3-4 chords where exactly half are diatonic) to probe the boundary.
- **Relative-key ambiguity** is a named, tested edge case: `Am Dm Em` scores
  identically for C major and A minor because they share a diatonic PC set —
  the test only asserts the root is "C major (0) or A minor (9)"
  (`KeyInferenceTests.cs:33-41`). Rich data should include such
  relative-major/minor-ambiguous triads deliberately.
- `Classify` (`KeyInference.cs:89`) labels a *candidate* chord relative to an
  inferred key as `Diatonic`, `ValidNonDiatonic`, or `Unrelated`
  (`KeyInference.cs:195-200`). `IsValidNonDiatonic` (`KeyInference.cs:151`)
  explicitly recognises: **tritone substitution** (interval 6),
  **chromatic mediants** (interval 3 or 4), **chromatic approach** (semitone,
  interval 1 or 11), **borrowed/modal-interchange chords** (root in the
  *parallel* mode's diatonic set — `KeyInference.cs:160-161`), and **secondary
  dominants** (a dominant-7th-family chord whose root resolves up a fourth into
  the diatonic set — `KeyInference.cs:163-169`). Each of these is a distinct,
  separately-branched musical relationship that test progressions should
  contain *as context chords* so that downstream `Classify` calls exercise every
  branch — e.g. ending a context on `G7` (sets up classifying `Db7` as a tritone
  sub, per `KeyInferenceTests.cs:61-67`) or `Am7` (sets up `F#` being scored as
  `ValidNonDiatonic` via the tritone-interval rule, surprisingly — see
  `KeyInferenceTests.cs:69-76`, where what looks "unrelated" is actually
  classified `ValidNonDiatonic`).

### 3. Prediction: progressions as weighting context, gated by entropy and key success

`NextChordPredictor.Predict` (`src/Dadabe.Core/Chord/NextChordPredictor.cs:27`)
only applies context weighting when `context is { Count: > 0 }` **and**
`KeyInference.InferKey` succeeds (`NextChordPredictor.cs:43-46`). When it
succeeds, each raw candidate is multiplied by 1.4 (Diatonic), 1.0
(ValidNonDiatonic), or 0.7 (Unrelated) before the softmax
(`NextChordPredictor.cs:52-57`). This means **two categories of progression
context are functionally distinct and both need coverage**: (a) progressions
that successfully infer a key (changing the output distribution) and (b)
progressions that are too short, too ambiguous, or too chromatic to infer one
(falling back to context-free scoring). `entropy` further reshapes the
distribution via temperature scaling (`NextChordPredictor.cs:39-40,72-73`,
clamped to `[0.01, 10.0]`), so combining varied progression context with varied
entropy values multiplies the interesting state space.

`PredictCommand.Run` (`src/Dadabe.Cli/Commands/PredictCommand.cs:53-62`) parses
context chords leniently: it uses `parser.TryParse` and **silently skips chords
it can't parse** (`PredictCommand.cs:59-61`) rather than failing the request.
A progression containing one malformed/unknown symbol alongside valid ones is
therefore a meaningful edge case — it should still produce a usable (shorter)
context list. The metadata echo (`PredictCommand.cs:76`,
`PredictionContractsTests.cs`/`PredictCommandTests.cs:134-146`) round-trips the
*original* `ProgressionDto` including `tempo`, so test data should verify tempo
survives the full request → response → `contextUsed` cycle.

### 4. The editor: progressions are named, slugged, persisted, hand-built

`ProgressionService` (`src/Dadabe.Editor/Services/ProgressionService.cs:19-35`)
slugifies the `name`, rejects empty names (`"Name is required."`,
`ProgressionService.cs:22`) and duplicate slugs on create
(`ProgressionService.cs:23-24`), and stores `chords` as a simple
`List<string>` alongside an optional `tempo`. The form
(`src/Dadabe.Editor/Slices/ProgressionsForm.cshtml:19-20,29-41`) lets a user
add/remove chord rows freely (client-side, no validation of chord *syntax*) and
caps tempo to `[1, 400]` via the `min`/`max` HTML attributes — but note this is
**only a UI hint**; the POST/PUT handlers parse tempo with a permissive
`int.TryParse` and impose no range check server-side
(`src/Dadabe.Editor/Routes/ProgressionRoutes.cs:30,44`). Rich data generation
for the editor layer should include: progressions with zero chords (the form
allows submitting with an empty `chord-list`), progressions with blank/
whitespace chord entries (filtered out via `Where(c =>
!string.IsNullOrWhiteSpace(c))`, `ProgressionRoutes.cs:29,43`), out-of-range or
non-numeric tempo strings, and duplicate names.

## Recipe: building a rich progression test dataset

Vary along these independent dimensions, and combine them — the cross-product
is what actually stresses the system end-to-end:

1. **Length**
   - Single chord (`["Cmaj7"]`) — exercises `VoiceLeadSolver`'s `n == 1`
     branch and zero-`Count` checks; also a minimal/degenerate prediction
     context.
   - Short (2-3 chords) — minimal multi-step voice-leading; matches existing
     unit tests' shapes (`VoiceLeadCommandTests.cs:22,36`).
   - Medium (4-8 chords) — typical "song section" length; good for
     `KeyInference` coverage scoring and for exercising multiple `solutions`.
   - Long (12+ chords) — stresses A* search performance/solution diversity
     and exercises recency-weighting tail effects in `KeyInference`.
   - Empty (`[]`) — must be tested as a *rejection* path for `voice-lead`
     (`VoiceLeadCommand.cs:31-32`) even though the JSON Schema alone would
     accept it.

2. **Key / tonal centre clarity**
   - Unambiguous major (e.g. `Cmaj7 Am7 Dm7 G7` — straight from
     `KeyInferenceTests.cs:23`, ~100% diatonic-root coverage).
   - Unambiguous minor (e.g. `Am7 Dm7 E7 Am7` — minor ii-V-i with a raised
     leading tone via the dominant `E7`).
   - Relative major/minor ambiguous (e.g. `Am Dm Em` —
     `KeyInferenceTests.cs:37`; deliberately produces a non-deterministic-but-
     bounded key result).
   - Borderline coverage (~50%): mix exactly half diatonic, half chromatic
     roots, to probe the `bestScore / maxScore < 0.5` cutoff
     (`KeyInference.cs:50`).
   - No inferable key: heavily chromatic/atonal sequences (e.g. `C F# B Eb`)
     that should make `InferKey` return `null` and force prediction to fall
     back to context-free weighting.

3. **Diatonic vs. chromatic / borrowed / secondary harmony**
   Deliberately end progressions on chords that set up each
   `IsValidNonDiatonic` branch for the *next* prediction call
   (`KeyInference.cs:151-171`):
   - Tritone substitution target (e.g. context ending `G7`, candidate `Db7`).
   - Chromatic mediant (root a major/minor third away).
   - Chromatic approach / semitone neighbour chords.
   - Borrowed / modal-interchange chords (parallel-mode roots, e.g. `Fm` in a
     C major context — the iv borrowed from C minor).
   - Secondary dominants (e.g. `A7` resolving to `Dm` in C major — V7/ii).

4. **Chord quality variety**
   Cover every `ChordQualityFamily` branch the predictor classifies
   (`NextChordPredictor.cs:183-195`): plain major/`6`, `maj7`/`maj9`+, minor/
   `m6`, `m7`/`m9`/`mMaj7`, dominant `7`/`9`/`13`, diminished (`dim`, `dim7`,
   `m7b5`), augmented (`aug`), and `sus2`/`sus4`. A rich dataset should ensure
   each family appears as *both* the seed chord and inside context progressions.

5. **Repetition / static harmony**
   - Immediate repeats (`Am Am Am Am`) — zero-distance transitions, vamps.
   - Pedal-point style progressions (one bass note, changing upper structure).
   - These stress whether `totalDistance: 0` and empty/trivial `moves` arrays
     serialize and validate correctly against
     `voice-lead-result.schema.json:46,57`.

6. **Cadential patterns**
   - Common: ii-V-I (`Dm7 G7 Cmaj7` — used directly in
     `VoiceLeadCommandTests.cs:57`), I-IV-V-I, I-V-vi-IV ("pop" progression),
     12-bar blues (I-IV-I-V-IV-I).
   - Unusual/strong test signal: plagal (IV-I), deceptive (V-vi), Andalusian
     cadence (i-bVII-bVI-V), chromatic descending bass lines.

7. **Chords that are hard or impossible to voice**
   - Include at least one progression containing a chord with an exotic
     extension/alteration likely to have zero or very few playable voicings
     under a restrictive `--min-comfort`, to exercise the
     `"... has no playable voicings ..."` `FormatException`
     (`VoiceLeadCommand.cs:52-55`).
   - Pair the same progression with `minComfort = 0.0` and a high threshold to
     compare behaviour.

8. **Malformed / partially-invalid content**
   - A progression with one unparseable symbol (e.g. `"Cmaj7 Xyz123 G7"`) to
     exercise `PredictCommand`'s silent-skip parsing (`PredictCommand.cs:59-61`)
     — verify the resulting context list is shorter than the input.
   - Whitespace-only or empty-string chord entries, to exercise the editor's
     filter (`ProgressionRoutes.cs:29,43`).
   - Whitespace-padded / multiply-spaced raw `--chords` strings, to exercise
     `Split(' ', RemoveEmptyEntries | TrimEntries)` (`VoiceLeadCommand.cs:29-30`).

9. **Tempo variations**
   - Absent (`tempo: null`, the DTO's default — `ProgressionDto.cs:8`).
   - Minimum valid (`1`), typical (`120`), at the editor's UI cap (`400`),
     and beyond it (`500`+) to see whether the (UI-only) bound is enforced
     anywhere server-side.

10. **`solutions` / `entropy` cross-products** (for voice-lead and predict
    respectively)
    - Pair short/long progressions with `solutions` at 1, mid-range, and the
      clamped max of 10 (`VoiceLeadCommand.cs:61`).
    - Pair key-clear/key-ambiguous contexts with low (`0.01`) and high
      (`10.0`) entropy (`NextChordPredictor.cs:72-73`,
      `PredictCommandTests.cs:149-159`) to confirm context weighting and
      temperature compose as expected.

## Example test cases

**1. Canonical diatonic ii-V-I — clean key inference, multi-step voice-leading**
```json
{
  "chords": ["Dm7", "G7", "Cmaj7"],
  "tempo": 120
}
```
Mirrors `VoiceLeadCommandTests.cs:57`; produces a 3-step solution with two
transitions, and as prediction context infers C major cleanly
(all roots diatonic, weighted coverage well above 50%).

**2. Long, modally-mixed progression with borrowed chords and a secondary dominant**
```json
{
  "chords": ["Cmaj7", "Fm6", "Cmaj7", "A7", "Dm7", "G7", "Cmaj7", "Ab7"],
  "tempo": 96
}
```
Mostly diatonic to C major, but `Fm6` is borrowed from C minor (modal
interchange / parallel-mode root, `KeyInference.cs:160-161`), `A7` is a
secondary dominant resolving to `Dm7` (V7/ii, `KeyInference.cs:163-169`), and
`Ab7` is a tritone substitute for `D7` (interval 6, `KeyInference.cs:154`).
Stresses both `KeyInference.InferKey`'s coverage threshold (still > 50%
diatonic-root coverage) and `Classify`'s non-diatonic branches when this is
fed back as prediction context.

**3. Relative-minor-ambiguous, short context for prediction**
```json
{
  "chords": ["Am", "Dm", "Em"],
  "tempo": null
}
```
Directly mirrors `KeyInferenceTests.cs:37` — scores identically for C major and
A minor; useful for asserting the predictor doesn't crash or behave
inconsistently when `InferKey` returns a "correct but ambiguous" result, and
for confirming `tempo: null` round-trips through `JsonIgnore(Condition =
WhenWritingNull)` (`ProgressionDto.cs:7`).

**4. Single-chord and static/repeated-harmony edge cases**
```json
{ "chords": ["Gmaj7"] }
```
```json
{ "chords": ["Am", "Am", "Am", "Am"], "tempo": 70 }
```
The first exercises `VoiceLeadSolver`'s `n == 1` zero-cost branch
(`VoiceLeadSolver.cs:47-54`) and the "single tonic chord still infers a
plausible key" case (`KeyInferenceTests.cs:43-50`). The second produces
zero-distance transitions between identical voicings (or near-identical ones,
depending on search diversity), testing whether `totalDistance: 0` and minimal
`moves` arrays serialize correctly against
`voice-lead-result.schema.json:30,46,57`, and whether heavy repetition skews
`KeyInference`'s recency-weighted scoring toward a single root's key.
