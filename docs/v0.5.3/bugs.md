# Dadabe v0.5.3 — Bug tracker

Add each discovered problem as a row. Keep **Status** as `open` until a fix is
confirmed; change to `fixed` when the commit lands. Add a **Notes** column entry
for any workaround or diagnosis detail worth keeping.

---

## Editor bugs

| # | Area               | Description                                                                                                                                                                                                                | Status | Notes |
|---|--------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------|-------|
| 1 | Prediction results | When a chord is clicked to show voicings, tuning must come from the song if it's active, not the tuning dropdown.                                                                                                          | fixed | Added `window.resolveVoicingTuning()` (app.js) — prefers the active song's tuning (mirrored onto `global-tuning-select`) over the result card's local `pred-tuning-select`. Wired into both `hx-vals` on `PredictionsResult.cshtml`. |
| 2 | Predictions        | The voicing options should be settable on this page, as a chord is clicked to show voicings.                                                                                                                               | fixed | Extracted the "Voicing options" panel to a shared partial (`VoicingOptionsFields.cshtml`); added one to `PredictionsIndex.cshtml` (`#pred-voicing-options`) and wired it via `hx-include` on the chord-click requests. `/api/voicings/fragment` now parses the same fields as `/api/voicings/run` via the new `VoicingRoutes.BuildSearchParams` helper. |
| 3 | Voicing Options    | Include root should default to true.                                                                                                                                                                                       | fixed | `requireRoot` checkbox now ships `checked` by default in the shared `VoicingOptionsFields.cshtml` partial (used everywhere the panel appears). |
| 4 | Songs              | The voicing options should be settable on this page, and should be used for "fill unpinned"                                                                                                                                | fixed | Added the same shared panel (`#song-voicing-options`) to `SongDetailBody.cshtml`; the "Fill unpinned" form now `hx-include`s it, and `Fill()` takes an optional `SearchParams` override (built from the submitted panel) instead of always using the song's persisted hand settings. |
| 5 | Voice Lead         | The results of finding a voice lead should include a display similar to the /{song}/chart output.                                                                                                                          | fixed | Each step now carries a `ChordDiagram`; `VoiceLeadingResult.cshtml` renders a `song-chart-slots`/`song-chart-slot` row per solution, reusing `ChordDiagramPartial` exactly as `SongChart.cshtml` does. |
| 6 | Voice Lead         | Keep the current voice lead input style (text of chords), but also add the ability to select a song, then a section. If this method is used, the user can replace the pinned voicings with the selected voice lead result. | fixed | Reused the existing header song/section selects (`#form-song`/`#form-section`, synced by `window.bindSongSelect()`) plus a new "use active section" checkbox; `/api/voiceleading/run` sources chords/bars/tuning/hand from the section when checked. Added `/api/voiceleading/apply` + `window.applyVoiceLead()` and a "Replace pinned voicings" button per solution, which calls `SongService.ReplaceSlots` — the same primitive "Fill unpinned" uses. |

## Core bugs

| # | Area | Description | Status | Notes |
|---|------|-------------|--------|-------|
| 1 | Voicing comfort (D54) | `HandAnglePenalty` (Rule 1) counts a barred inner finger (middle/ring) once per string the barre covers instead of once per physical finger, since it loops over every `FingerAssignment` rather than distinct fingers. Inflates the reverse-stretch penalty for barred shapes; masked in practice by the `0.30` cap. | fixed | Deduped `assignments` by `Finger` (`GroupBy(Finger).Select(First)`) before the inner loop in `Voicing.cs`. Added `Rule1_barred_inner_finger_is_penalized_once_not_per_string` covering a 3-string barre. |

