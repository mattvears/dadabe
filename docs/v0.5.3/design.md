# Dadabe v0.5.3 — Design doc (retroactive)

## Overview

v0.5.3 has no single theme — it's the small set of fixes and extensions that landed
on top of v0.5.2 while using the song mode and voicing search day to day. This doc
is written after the fact to give the work a record before `bugs.md` grows around it.

Three pieces of substance:

1. **Song-owned tuning, UI follow-through (extends D38)** — the global tuning select
   now hides itself while a song is active, matching the rule that a song's pinned
   shapes are only meaningful under the tuning they were pinned in.
2. **Hand-biomechanics comfort penalties (D54)** — four new deductions in
   `Voicing.ComputeComfort`, informed by which finger frets which string/fret, not
   just the scalar span/mute counts the v0.1 model (D15) used.
3. **Per-section next-chord predictions (D55)** — every song section with at least
   one parseable chord now shows a "predicted next" read-out inline, reusing the
   existing `NextChordPredictor` (D24/D33) instead of requiring a trip to the
   Predictions tab.

Plus one operational addition: request/response logging for the Editor (console +
rolling file), added to make the previous three easier to debug against, not part
of the product surface.

---

## 1. Song tuning display (extends D38)

`D38` established that a song owns its tuning and every pinned shape in it is
meaningless under a different one. What shipped in v0.5.2 recorded that rule in
the data model but the header still showed the *editable* global tuning select
while a song was active — inviting a change that would silently invalidate every
pinned voicing in the song.

Fix (`53dbe39`): while a song is active, the tuning select is hidden and replaced
with a plain-text display of the song's tuning (`app.js` `bindSongSelect` /
`song-tuning-display`, `SongDetailBody.cshtml`). No tuning field exists to edit by
mistake. Editing a song's tuning remains possible only through the song's own
edit form.

## 2. Hand-biomechanics comfort penalties (D54)

The v0.1 comfort score (D15, `design.md` §7 step 8) worked from a scalar summary
of a voicing — span, mute counts, barre cost, lowest-fret discount — with no
notion of *which* finger is on *which* string/fret. That was enough to rank
shapes by rough playability but missed a class of shapes that read as "easy" by
span alone yet fight the hand's actual mechanics.

`Voicing.ComputeComfort` (`src/Dadabe.Fretboard/Voicing.cs`) gained two optional
parameters — `assignments` (the per-finger `FingerAssignment`s) and
`orderedPositions` (the shape's `FretPosition`s, string-ordered) — and four new
penalty rules, each independently gated so a voicing only pays for the mechanics
it actually exhibits:

- **Rule 1 — hand angle / reverse stretch.** The index finger normally sits
  toward the nut/bass side of a shape while middle/ring reach toward the
  body/treble side as fret and string climb together. When the index instead
  reaches *past* an inner (middle/ring) finger — lower string **and** lower fret
  than that inner finger — the wrist has to supinate to keep the inner finger's
  joint upright. Cost scales with the fret gap, capped at `0.30` so one outlier
  pair can't zero out an otherwise-fine voicing.
- **Rule 2 — tendon interdependence.** Middle and ring share tendon sheaths, so
  fretting them on adjacent strings at different frets already fights their
  coupled mobility. When that's compounded by index and pinky stretching away
  from each other in opposite directions around that pair, it's a flat `0.35`
  spike rather than a graded cost — there's no "a little" version of this shape.
- **Rule 3 — inner-string clearance (the arch penalty).** A sounded-but-open
  string sandwiched between two fretted neighbors needs the fretting fingers to
  arch clear of it at the PIP joint, the joint that flexes most naturally by
  default. Cost scales with the voicing's overall span, since a hand already
  braced for a wide stretch has less articulation margin left for the arch.
- **Rule 4 — abduction.** A per-fret exponential surcharge (base `1.6`, scaled by
  `0.02`) for finger pairs on non-adjacent strings, where lateral finger spread
  compounds independently of the fret-span cost already captured elsewhere.

Backward compatibility: both new parameters default to `default` (empty), and
existing callers that only have the scalar summary in scope get exactly the
pre-Rule-1..4 score — verified directly in
`VoicingComfortTests.Omitting_assignments_and_positions_leaves_comfort_unchanged`.
`VoicingSearch` is the only caller that has `Fingering.Assignments` and
`Positions` in scope, so it's the only caller that supplies them
(`VoicingSearch.cs:262-265`).

## 3. Per-section next-chord predictions (D55)

Song sections already showed position range and hardest-chord read-outs (§10,
v0.5.2). `SongRoutes.BuildDetailModel` now also predicts the section's likely next
chord and threads it through `SectionViewModel.Predictions` to
`SongDetailBody.cshtml`, rendered as `Predicted next: Dm7 (42%), G7 (31%), …` under
the section header whenever the section has at least one parseable chord.

The predictor call reuses `NextChordPredictor.Predict` unchanged (D24 rules
engine, D33 key-weighted context) — the current chord is the section's last slot,
and every chord before it is passed as `context` so the key-weighting in D33
applies the same way it does on the standalone Predictions tab. An empty section
predicts nothing (`BuildPredictions` returns `[]` for zero specs) rather than
guessing from silence.

## 4. Editor request/response logging (operational)

Serilog (`Serilog.AspNetCore`, `Serilog.Sinks.File`) writes to console and to
`src/Dadabe.Editor/logs/editor-<date>.log` (git-ignored). ASP.NET Core's built-in
`HttpLogging` middleware captures request/response headers and bodies. Response
body capture is deliberately narrow — the media-type allow-list only covers
`application/x-www-form-urlencoded` (requests) and JS (`text/javascript`,
`application/javascript`) — so HTML page bodies, which are large and mostly
noise for debugging, are never logged; only status/headers are. A small
middleware forces static file responses through the normal stream instead of the
`sendfile` fast path, since `HttpLogging` can't see body bytes that bypass the
stream it wraps.

This is dev tooling, not a product feature — no design decision number assigned.

---

## Files touched

- `src/Dadabe.Fretboard/Voicing.cs` — D54 penalty rules.
- `src/Dadabe.Fretboard/VoicingSearch.cs` — thread `Fingering.Assignments` / `Positions` into `ComputeComfort`.
- `tests/Dadabe.Fretboard.Tests/VoicingComfortTests.cs` — coverage for D54 and the backward-compatibility guarantee.
- `src/Dadabe.Editor/Routes/SongRoutes.cs`, `Slices/Models.cs`, `Slices/SongDetailBody.cshtml` — D55 per-section predictions.
- `src/Dadabe.Editor/wwwroot/app.js` (prior commit `53dbe39`), `Slices/SongDetailBody.cshtml` — D38 tuning-display fix.
- `src/Dadabe.Editor/Program.cs`, `Dadabe.Editor.csproj`, `appsettings.json` — request/response logging.
