# Dadabe v0.5.1 — Bug tracker

Add each discovered problem as a row. Keep **Status** as `open` until a fix is
confirmed; change to `fixed` when the commit lands. Add a **Notes** column entry
for any workaround or diagnosis detail worth keeping.

---

## Editor bugs

| # | Area | Description | Status | Notes |
|---|------|-------------|--------|-------|
| 1 | Sidebar | `wa-drawer` is an overlay, not a persistent sidebar — closes on X with no way to reopen; gray backdrop covers content | fixed | Replaced with plain `<nav class="app-nav">` + CSS |
| 2 | Sidebar | `form-row` CSS class missing from app.css; used in voicings/voice-lead forms | fixed | Added to app.css |
| 3 | Voice Lead / Voicings | Result header shows `[undefined]` for tuning name | fixed | `wa-select.value` is undefined before custom element upgrades; `ResolveLabel()` falls back to catalog name server-side; client script deferred to `customElements.whenDefined('wa-select')` |
| 4 | Predictions | Context progression saved to model but never passed to `NextChordPredictor.Predict` — context had zero effect at runtime | fixed | Load progression chords in run handler, parse to `ChordSpec[]`, pass as context arg; result card now shows a "context: name" badge |
| 5 | Voicings | Structure column is empty | fixed | `wa-tooltip` with a plain text slot renders invisibly in WA4; replaced with `<abbr title>`. **Regressed** — the `wa-tooltip` wrapper was back in `VoicingsResult.cshtml`; re-fixed alongside #11, and the same pattern in `VoiceLeadingResult.cshtml` was cleaned up too |
| 6 | Voicings | Voicings output should have a column to show notes in the chord: ie [D A D A x x] | fixed | Added `Notes` field to `VoicingRow`; built from `FretPosition.DisplayNote` per string |
| 7 | Voicings | Voicings output should have a column to show intervals in the chord: ie [I V I V x x] | fixed | Added `Intervals` field to `VoicingRow`; `FunctionToRoman` converts `"b3"` → `"bIII"` etc. |
| 8 | Predictions | Predictions output should show context chord progression when applicable | fixed | Added `ContextChords` to `PredictionResultModel`; displayed inline in the context badge |
| 9 | Voicings | In the 'Next Chords' area in the probability column, no percentage bar is rendered. The percentage bar renders correctly on the predictions screen. | fixed | Bar used `background:var(--wa-color-brand-600)` with no fallback — not a WA3 token, so it painted transparent. Both screens now share `.prob-cell`/`.prob-bar`/`.prob-pct` in app.css, with a literal-color fallback and `min-width:2px` so low-probability rows still show a stub |
| 10 | Voicings | In the 'Next Chords' area, chords should be clickable. Clicking should display voicings. | fixed | Rows reuse the predictions drill-down: `hx-get /api/voicings/fragment` into a `.voicing-panel-inline` inside the details panel. New `VoicingResultModel.TuningValue` carries the raw tuning the search ran with, so a stacked card queries its own tuning rather than whatever the form now holds. Row highlighting moved to `window.selectChordRow()` (app.js), which scopes the clear to the owning `.result-card` |
| 11 | Predictions | When showing voicings in the predictions area, ASCII notation and comfort is displayed, but structure is not.  | fixed | Same root cause as #5 — in WA3 `wa-tooltip`'s default slot is the tooltip *body*, not the anchor, so the wrapped label never rendered. `VoicingFragment.cshtml` now uses `<abbr title>`; app.css styles `abbr[title]` with a dotted underline |


## Core bugs

| # | Area | Description | Status | Notes |
|---|------|-------------|--------|-------|
| 1 | Voicings | The comfort algorithm is assigning a low comfort to chord [2 1 2 x x x] but because only the top three strings are played this one is actually pretty comfortable, whereas a drop-2 version [2 1 x 3 x 3] gets the same comfort score even though this one is tricky because two non-consecutive strings must be muted. | fixed | Added an "isolated mute" penalty: `VoicingSearch.CountIsolatedMutes` counts muted strings sandwiched between two sounded strings (harder to deaden mid-chord than a contiguous block of mutes at the edge); `Voicing.ComputeComfort` now takes `isolatedMutedStrings` and applies an extra `-0.15` per occurrence on top of the base per-mute penalty |

