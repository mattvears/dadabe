# Dadabe v0.5 — Progression Voice Leading + UI Refresh

## Overview

v0.5 delivers three related improvements:

1. **Progression voice leading** — a new `dadabe voice-lead` CLI subcommand that takes a
   chord progression and returns the sequence of voicings that minimises total finger
   travel across the neck. This activates the `Transition` / `VoiceMove` scaffold from D16
   and the `Progression` memo namespace reserved in v0.1.

2. **Web Awesome UI migration** — replace BeerCSS + Material Symbols with
   [Web Awesome](https://webawesome.com/docs/) (`<wa-*>` web components) throughout the
   Editor. Cleaner form controls, native dialog/drawer, and consistent icon set via
   `<wa-icon>`.

3. **Voicings form — advanced options** — expose the remaining hand-model and filter CLI
   flags (`--frets`, `--span`, `--min-strings`, `--max-strings`, `--allow-open`,
   `--allow-barre`, `--allow-thumb`, `--categories`) in a collapsible
   `<wa-details>` section alongside the existing fields.

4. **Context-weighted prediction scoring** (D27, deferred from v0.4) — key inference from
   progression context + mild multiplier on chord-prediction candidates.

No chord diagram rendering (deferred beyond v0.5 — the `.vcd` CSS and `ChordDiagram` model
in the codebase are leftover scaffolding; voicings continue to display as ASCII notation).
No new rendering formats (tab, notation, audio), no polychord parsing, no new voicing
categories. Those remain in `docs/future.md`.

---

## 1. `voice-lead` subcommand (D28)

**Command**:
```
dadabe voice-lead --chords "Cmaj7 Am7 Dm7 G7"
                  [--tuning DADABE]
                  [--hand-profile Default]
                  [--solutions 1]
                  [--min-comfort 0.0]
                  [--out <file.json>]
                  [--pretty]
                  [--validate-schema]
```

`--chords` is required. Chord symbols are whitespace-separated. Tuning and hand-profile
follow the same resolution logic as `voicings`.

`--solutions` controls how many full-progression solutions are returned (default 1 = the
single minimum-cost path; max 10). Each solution is an independent optimal sequence with
no voicings shared between solutions.

Output is `Envelope<VoiceLeadingResultDto>` with `"command": "voice-lead"`,
`"schemaVersion": "1"`.

### Execution flow

```
1.  Parse each chord symbol → ChordSpec list
2.  For each ChordSpec, run VoicingSearch → get candidate voicing set (respect --min-comfort)
3.  Build the transition graph: nodes = (chord_index, voicing_index); edges connect every
    voicing at position i to every voicing at position i+1
4.  Assign edge cost = TotalFretDistance(v_i, v_{i+1}) (§2)
5.  Find the --solutions cheapest paths through the graph (§2)
6.  Map each path to a VoiceLeadingResultDto (§3)
7.  Optionally validate schema and write output
```

---

## 2. Voice-leading algorithm (D29)

### Fret-distance metric

The cost of transitioning from voicing A to voicing B is the sum over shared string
indices of the absolute fret change per string, counting muted→fretted and fretted→muted
as full-span moves:

```
TotalFretDistance(A, B) =
  Σ over s in strings {
    let a = A.Positions[s].Fret ?? -1   // -1 for muted
    let b = B.Positions[s].Fret ?? -1
    |a - b|
  }
```

Two voicings on different tunings are incompatible (cost = ∞); the command rejects
mixed-tuning runs.

### Path search

For short progressions (≤ 8 chords) and typical result sizes (≤ 200 voicings per chord)
the graph has at most 8 × 200 = 1 600 nodes and 200² = 40 000 edges per chord boundary.
Dijkstra over the layered DAG is O(N · K²) where N = chord count and K = voicings per
chord — tractable synchronously.

For `--solutions > 1`, run Yen's k-shortest-paths on the same DAG. Paths are de-duplicated
at the voicing-sequence level (same sequence of voicing IDs = same solution, reject).

If any chord in the progression has zero playable voicings after `--min-comfort` filtering,
the command exits with an error naming the chord and the unsatisfied comfort threshold.

### `Transition` and `VoiceMove` types (D16 activation)

These types are created in `Dadabe.Core` (they were scaffolded as stubs in D16):

```csharp
public sealed record VoiceMove(
    int StringIndex,
    int? FromFret,   // null = muted in source
    int? ToFret,     // null = muted in target
    int Distance);   // |from - to| with muted = -1

public sealed record Transition(
    Voicing From,
    Voicing To,
    IReadOnlyList<VoiceMove> Moves,
    int TotalDistance)
    : IContentHashable
{
    public ContentHash ContentHash => ContentHash.FromCanonical(
        Namespaces.Transition, 1,
        Canonical.Bytes(From.ContentHash),
        Canonical.Bytes(To.ContentHash));
}
```

`Transition` is memoised under `Namespaces.Transition`. On a long progression with
repeated chord pairs the memo avoids recomputing identical pair costs.

---

## 3. Output DTOs (D30)

New types in `src/Dadabe.Cli/Io/Dtos.cs`:

```csharp
public sealed record VoiceLeadingResultDto(
    [property: JsonPropertyName("chords")]
    IReadOnlyList<string> Chords,
    [property: JsonPropertyName("tuning")]
    string Tuning,
    [property: JsonPropertyName("solutions")]
    IReadOnlyList<VoiceLeadingSolutionDto> Solutions);

public sealed record VoiceLeadingSolutionDto(
    [property: JsonPropertyName("totalDistance")]
    int TotalDistance,
    [property: JsonPropertyName("steps")]
    IReadOnlyList<VoiceLeadingStepDto> Steps);

public sealed record VoiceLeadingStepDto(
    [property: JsonPropertyName("chord")]
    string Chord,
    [property: JsonPropertyName("voicing")]
    VoicingDto Voicing,                  // reuse existing VoicingDto
    [property: JsonPropertyName("transition"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    TransitionDto? Transition);          // null for the first step

public sealed record TransitionDto(
    [property: JsonPropertyName("totalDistance")]
    int TotalDistance,
    [property: JsonPropertyName("moves")]
    IReadOnlyList<VoiceMoveDto> Moves);

public sealed record VoiceMoveDto(
    [property: JsonPropertyName("string")]
    int String,
    [property: JsonPropertyName("from")]
    int? From,
    [property: JsonPropertyName("to")]
    int? To,
    [property: JsonPropertyName("distance")]
    int Distance);
```

`VoicingDto` is the existing type already emitted by the `voicings` subcommand; no
changes needed.

---

## 4. Web Awesome migration (D32)

Replace BeerCSS + Material Symbols with Web Awesome throughout `Dadabe.Editor`.

### CDN swap in `PageShell.cs`

Remove:
```html
<script type="module" src="https://cdn.jsdelivr.net/npm/beercss@4.0.21/dist/cdn/beer.min.js"></script>
<script type="module" src="https://cdn.jsdelivr.net/npm/material-dynamic-colors@1.1.4/dist/cdn/material-dynamic-colors.min.js"></script>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined" />
```

Add (verify exact CDN URL from [webawesome.com/docs](https://webawesome.com/docs/) during implementation):
```html
<link rel="stylesheet" href="https://ka-f.webawesome.com/webawesome@3.7.0/styles/webawesome.css" />
<script type="module" src="https://ka-f.webawesome.com/webawesome@3.7.0/webawesome.loader.js"></script>
```

### Element mapping

| BeerCSS pattern | Web Awesome replacement |
| --- | --- |
| `<button>…</button>` | `<wa-button>…</wa-button>` |
| `<div class="field label border"><input …/><label>…</label></div>` | `<wa-input label="…" name="…" />` |
| `<div class="field suffix border"><select …>…<i>arrow_drop_down</i><label>…</label></div>` | `<wa-select label="…" name="…">…</wa-select>` |
| `<dialog>` BeerCSS modal | `<wa-dialog>` |
| `<nav class="left">` sidebar | `<wa-drawer>` or plain `<nav>` with WA tokens |
| `<i>icon_name</i>` Material icon | `<wa-icon name="…"></wa-icon>` (Font Awesome icon names) |
| `<article class="border">` card | `<wa-card>` |
| `<div class="htmx-indicator">` spinner | `<wa-spinner>` hidden via same `.htmx-indicator` CSS rule |
| `<progress>` | `<wa-progress-bar>` |

### htmx compatibility notes

Web Awesome uses Shadow DOM, which means `hx-target` and `hx-swap` selectors must target
the **host element** (e.g., `wa-dialog`) rather than internal shadow nodes. Modal open/close
is controlled via the `open` boolean attribute on `<wa-dialog>`, toggled in response to
htmx `htmx:afterSwap` events or via a small inline script:

```js
document.querySelector('wa-dialog').show();
```

Form inputs inside `<wa-dialog>` must be in the light DOM (not a slot) to be picked up by
`hx-include` and standard `FormData`. Verify each form submits correctly before marking
the migration complete.

### app.css updates

Remove all BeerCSS class overrides (`.field`, `.chip`, etc.) and the leftover `.vcd*`
chord-diagram rules. Keep:
- `.htmx-indicator` / `.htmx-request` rules
- Any layout rules not covered by WA tokens

---

## 5. Context-weighted prediction scoring (D33)

Implementing the algorithm described in v0.4 §3.1 (D27).

### Key inference

Given a `context.Chords` list, infer the most probable key by finding the major or minor
key whose diatonic pitch-class set covers the most chords. Ties broken by recency weight:
chord at index `i` from the end contributes `1 + 0.2 × i⁻¹` to the fit score (later
chords weighted more heavily). Implemented as a static helper
`KeyInference.InferKey(IReadOnlyList<ChordSymbol> context) → (Note Root, bool IsMinor)?`.
Returns null when context is empty or coverage is below 50%.

### Multiplier table

After `NextChordPredictor.Predict` produces raw scores, each candidate is scaled:

| Relationship to inferred key | Multiplier |
| --- | --- |
| Diatonic root + quality | × 1.4 |
| Non-diatonic but valid: secondary dominant, tritone sub, chromatic mediant, borrowed chord | × 1.0 |
| Unrelated | × 0.7 |

Scores are re-normalised (softmax) after scaling. Entropy is still applied *before*
softmax, so high entropy flattens the distribution before the multiplier can over-weight
diatonic results.

### Classification of non-diatonic moves

A candidate is "valid non-diatonic" if any of the following hold, relative to the root of
the *last* chord in context:

- Interval from last root to candidate root is a tritone (6 semitones): tritone sub.
- Interval is a minor third (3) or major third (4): chromatic mediant.
- Interval is a semitone (1 or 11): chromatic approach.
- Candidate root is a diatonic degree in the *parallel* mode of the inferred key:
  modal interchange / borrowed chord.
- Candidate quality is a dominant 7th and its root resolves by P5 down to a diatonic
  chord: secondary dominant.

This classification is a lookup; no harmonic analysis beyond interval arithmetic and key
membership.

### Activation

Context weighting activates only when `request.Context` is non-null and
`KeyInference.InferKey` returns a non-null result. If key inference fails, prediction
behaves identically to v0.4 (no scaling applied, no warning emitted).

---

## 6. Editor — voice-lead page (D34)

Add a `/voice-lead` page to the Editor following the same htmx/RazorSlice pattern as
`/voicings`.

Form fields:
- `chords` — textarea, one symbol per line *or* space-separated (normalised server-side)
- `tuning` — same select as `/voicings`
- `handProfile` — select, options populated from registered hand profiles; only "Default" ships
- `solutions` — number, 1–5 (default 1)
- `minComfort` — number, 0–100 (default 70)

Result panel:
- One card per solution, labelled "Solution N — total distance: X frets".
- Inside each card: table with one row per chord step. Columns: Chord | ASCII | Structure | Comfort | Distance (transition cost from previous step; "—" for first).
- Close button removes the article (same as voicings).

Navigation entry added to sidebar: `<wa-icon name="music">` + "Voice Lead".

---

## 7. Voicings form — advanced options (D36)

The existing voicings form exposes chord, tuning, limit, top-n, entropy, and min-comfort.
The remaining hand-model and filter flags are added inside a `<wa-details summary="Advanced options">`
collapsible so the default view stays clean.

### New fields

| Field | Type | Default | CLI flag |
| --- | --- | --- | --- |
| `frets` | number, 1–24 | 15 | `--frets` |
| `span` | number, 1–12 | 4 | `--span` |
| `minStrings` | number, 2–6 | 3 | `--min-strings` |
| `maxStrings` | number, 2–6 | 6 | `--max-strings` |
| `allowOpen` | toggle | on | `--allow-open` |
| `allowBarre` | toggle | on | `--allow-barre` |
| `allowThumb` | toggle | off | `--allow-thumb` |
| `categories` | checkbox group | all checked | `--categories` |

`categories` options: `triad`, `shell`, `drop-2`, `drop-3`, `spread`.
All checked = no `--categories` flag is passed (server omits the flag, returning all categories).
Partial selection = comma-joined string passed as `--categories`.

### Route handling

`VoicingRoutes` maps each field to the corresponding CLI flag. Fields at their default value
are omitted from the command line (no-op flags avoided). `allowOpen` and `allowBarre` being
on by default means their flags are omitted when checked; unchecking them passes
`--allow-open false` / `--allow-barre false` (the CLI flag accepts an explicit bool).
`allowThumb` off by default: only passed when checked.

---

## 8. Contextual help and music theory display (D35)

Surface music theory information inline throughout the Editor so the UI teaches as
well as operates.

### Chord quality tooltip on voicing results

When the voicings result card renders, the chord header (`Dm7 [DADABE]`) gains a
`<wa-tooltip>` that expands to:

- **Quality name**: e.g. "minor seventh"
- **Interval formula**: `1 – b3 – 5 – b7`
- **Typical function**: e.g. "ii chord in major keys; iv chord in minor keys"

The tooltip content is generated server-side from the resolved `ChordSpec` (the
pitch-class functions are already in the envelope) and rendered as a short HTML
fragment inside the `<wa-tooltip content="…">` attribute.

### Voicing structure glossary

Each voicing row's Structure cell (e.g. `drop-2`, `shell`, `spread`) links to a
brief inline definition rendered as a `<wa-tooltip>`:

| Structure | Definition |
| --- | --- |
| triad | Root, third, fifth — the fundamental three-note chord |
| shell | Root + 3rd/7th only; omits 5th for a lean, functional sound |
| drop-2 | Second-highest voice of a close-position chord dropped an octave |
| drop-3 | Third-highest voice dropped an octave; wider, more open sound |
| spread | No specific interval rule; spans more than one octave |

Definitions live in a static `VoicingStructureGlossary.cs` dictionary, not a data
file — they are not user-configurable.

### Voice-lead page theory panel

Below the voice-lead form, a collapsible `<wa-details>` panel titled
"About voice leading" explains:

- What minimal-motion voice leading is.
- Why it matters on guitar (hand economy, smooth voice lines).
- How the distance metric is calculated (sum of per-string fret changes).
- A one-sentence note on Yen's k-shortest paths when `solutions > 1`.

Content is static HTML in the `VoiceLeadingIndex.cshtml` slice.

### Prediction next-chord theory note

The next-chord results table (shown when `Top N > 0` on the Voicings page) gains a
collapsible `<wa-details>` block above it:

> **How predictions work:** Dadabe uses a rules-based model: chord quality families
> (dominant, tonic, subdominant, …) have weighted resolution tendencies drawn from
> common-practice and jazz harmony. Entropy controls how concentrated or spread the
> distribution is — lower entropy favours the strongest resolutions; higher entropy
> surfaces surprising but valid moves.

This is static text; no server logic needed.

### Reference page theory content

The existing `/reference` page (cadences, modes, scales) is currently a data editor.
Add a read-only "Theory notes" tab per section:

- **Cadences tab**: one-sentence definitions for authentic, plagal, deceptive, and
  half cadences; a note on how each resolves tension.
- **Modes tab**: the parent scale, characteristic note, and typical mood for each
  mode in the loaded catalog.
- **Scales tab**: the interval pattern and a characteristic chord quality (e.g.
  "Dorian: b3 natural 6 — characteristic m7 sound").

Tab content is rendered from the existing catalog data (no new data files); the mode
and scale records already carry `description` fields.

---

## 9. Tests

- `VoiceLeadCommandTests` — round-trip: two-chord progression → cheapest path has
  `TotalDistance ≥ 0`; three-chord produces exactly 2 transitions.
- `TransitionTests` — `TotalFretDistance` on known pairs matches manual calculation;
  muted→fretted and fretted→muted correct.
- `KeyInferenceTests` — C major context → key C major; Am context → key A minor;
  empty context → null.
- `ContextWeightedPredictionTests` — diatonic candidate gets higher score than
  unrelated candidate given C major context; score ordering is preserved at low entropy.
- Editor integration: GET `/voice-lead` returns 200; POST `/api/voiceleading/run` with
  valid chords returns an article fragment.

---

## 10. Checklist

### Voice leading

- [x] `Transition.cs` and `VoiceMove.cs` in `src/Dadabe.Fretboard/` (D16 → D29)
- [x] `VoiceLeadSolver.cs` — backward DP + A* on the layered voicing DAG
- [x] `VoiceLeadCommand.cs` in `src/Dadabe.Cli/Commands/`
- [x] Wire `voice-lead` subcommand in `Program.cs`
- [x] `VoiceLeadingResultDto` and related DTOs in `Dtos.cs`
- [x] `schemas/voice-lead-result.schema.json`

### Web Awesome migration

- [x] Swap CDN includes in `PageShell.cs`
- [x] Migrate all `.cshtml` files: buttons, inputs, selects, dialogs → `<wa-*>`
- [x] Icon names: mapped Material icons to Font Awesome equivalents
- [x] Prune `app.css`: removed BeerCSS overrides and `.vcd*` rules; kept `.htmx-indicator`

### Context-weighted scoring

- [x] `KeyInference.cs` in `src/Dadabe.Core/Chord/`
- [x] Update `NextChordPredictor.Predict` to accept optional context; apply multipliers
- [x] Wire context from `request.Context` through `PredictCommand`

### Editor — voice-lead page

- [x] `VoiceLeadRoutes.cs` in `src/Dadabe.Editor/Routes/`
- [x] `VoiceLeadingIndex.cshtml` and `VoiceLeadingResult.cshtml` slices
- [x] Add `/voice-lead` nav entry to `PageShell.cs`

### Voicings form — advanced options

- [x] Add `<wa-details>` collapsible to `VoicingsIndex.cshtml`
- [x] Fields: `frets`, `span`, `minStrings`, `maxStrings`, `allowOpen`, `allowBarre`, `allowThumb`, `categories`
- [x] Update `VoicingRoutes.cs` to map new fields to `SearchParams`

### Contextual help and theory display

- [x] `VoicingStructureGlossary.cs` — static dictionary of structure → definition
- [x] Structure glossary tooltips in voicing result rows (`<wa-tooltip>`)
- [x] Voice-lead theory panel (`<wa-details>`) in `VoiceLeadingIndex.cshtml`
- [x] Next-chord prediction theory note in `VoicingsResult.cshtml`

### Tests

- [x] `VoiceLeadCommandTests`
- [x] `TransitionTests` — updated for fret-based API + new distance tests
- [x] `KeyInferenceTests`
- [x] `ContextWeightedPredictionTests`

### Docs

- [x] `docs/v0.5/examples/voice-lead-request.json` and `voice-lead-response.json`
- [x] `CHANGELOG.md` — v0.5 entry
- [ ] Tag release v0.5 when ready

---

## 11. Non-goals

- No chord diagram rendering (deferred beyond v0.5; `.vcd` CSS and `ChordDiagram` model are leftover scaffolding to be cleaned up).
- No tab, notation, or audio rendering.
- No polychord parsing.
- No new voicing categories.
- No progression CRUD in the Editor (deferred — the voice-lead page takes inline input).
- No persistent memo backends beyond in-memory.
- No bass-note enforcement in voicing search (Q4, deferred beyond v0.5).

---

## 12. Files to create / modify

| File | Action |
| --- | --- |
| `src/Dadabe.Core/Transitions/Transition.cs` | Create |
| `src/Dadabe.Core/Transitions/VoiceMove.cs` | Create |
| `src/Dadabe.Core/Transitions/VoiceLeadSolver.cs` | Create |
| `src/Dadabe.Core/Prediction/KeyInference.cs` | Create |
| `src/Dadabe.Core/Prediction/NextChordPredictor.cs` | Update — add context scoring |
| `src/Dadabe.Cli/Commands/VoiceLeadCommand.cs` | Create |
| `src/Dadabe.Cli/Io/Dtos.cs` | Add voice-lead DTOs |
| `src/Dadabe.Cli/Program.cs` | Add `voice-lead` subcommand |
| `schemas/voice-lead-result.schema.json` | Create |

| `src/Dadabe.Editor/PageShell.cs` | CDN swap; add voice-lead nav entry |
| `src/Dadabe.Editor/Routes/VoiceLeadRoutes.cs` | Create |
| `src/Dadabe.Editor/Slices/VoiceLeadingIndex.cshtml` | Create — includes `handProfile` select |
| `src/Dadabe.Editor/Slices/VoiceLeadingResult.cshtml` | Create |
| `src/Dadabe.Editor/Slices/VoicingsIndex.cshtml` | Update — add advanced options collapsible |
| `src/Dadabe.Editor/Routes/VoicingRoutes.cs` | Update — map new advanced fields to CLI flags |
| `src/Dadabe.Editor/wwwroot/app.css` | Remove BeerCSS overrides and `.vcd*` rules; keep `.htmx-indicator` |
| All `*.cshtml` slices | Update — `<wa-*>` migration |
| `tests/Dadabe.Cli.Tests/VoiceLeadCommandTests.cs` | Create |
| `tests/Dadabe.Core.Tests/Transitions/TransitionTests.cs` | Create |
| `tests/Dadabe.Core.Tests/Prediction/KeyInferenceTests.cs` | Create |
| `tests/Dadabe.Core.Tests/Prediction/ContextWeightedPredictionTests.cs` | Create |
| `CHANGELOG.md` | Add v0.5 entry |
