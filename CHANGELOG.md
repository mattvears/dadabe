# Changelog

All notable changes to Dadabe are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

## [v0.6] — 2026-09-05 — Key-aware transforms

### Added

- **Strictness policy (D54) and out-of-key chord policy (D46).** New `TransformRequestOptions`
  (`Strictness: Strict | Loose`, optional key override) threaded through `TransformChain.Apply`
  via two reserved step-parameter keys, so the eleven existing key-blind transforms are
  unaffected. Defaults to `loose`: approximate (nearest diatonic, triad-reduce then re-extend)
  and flag every approximation, rather than v0.5.2's blanket skip-and-report. An out-of-key
  chord chromatic-transposes with quality left alone and is always flagged (`out-of-key` note,
  never silent); a progression `KeyInference.InferKey` cannot resolve (below 50% coverage)
  produces a uniform `needs-key` note instead of falling back to C major.
- **Key-aware transform family (D47)**: `diatonic-transpose` (moves by scale degree, not
  semitone — quality changes as a side effect), `parallel-mode` (rebuilds each chord on its
  degree in a different mode of the same tonic; a `positions` param doubles as selective
  borrowing), `substitute` (functional-group substitution — tonic/subdominant/dominant — chosen
  by live common-tone count rather than a fixed per-key table), and `negative-harmony` (mirrors
  roots about the inferred key's tonic and flips major↔minor). `TransformCatalog` grows from
  eleven to fifteen registered transforms. Built on new shared `Dadabe.Core.Transform` helpers —
  `KeyAwareTransform` (key resolution + the strict/loose branch), `ScaleTriads` (generalizes
  `KeyInference`'s degree/quality table to any 7-note scale), `ChordApproximation` (the
  triad-reduce-and-reassign-quality logic, shared with a refactored `ReduceTransform`) — plus a new
  Core-native `DiatonicModes` table (ionian…locrian), kept separate from the Editor's
  reference-glossary `ModeModel` catalog and cross-checked against it in tests.
- Editor transform panel gains request-level strictness and key-override controls (shared by
  every key-aware transform) plus the four new transform types in the chain-step select; the
  CLI `transform` subcommand's `--all` discovery mode and single-arg (`type:value`) shorthand
  are extended to match.

---

## [v0.5.4] — 2026-09-05 — Transform engine, song mode, and comfort model refinements

Covers what shipped across the v0.5.2/v0.5.3 development commits and was tagged as v0.5.4;
this range was never split into per-patch changelog entries, so it is recorded here as one.

### Added

- Power-chord (fifth) support. New `5` form in `ChordGrammar.json` — `C5`, `F#5`, `Bb5`
  parse to root + perfect fifth with no third. Unlike the triad forms, `required` lists
  `1` alongside `5`, since a rootless root-and-fifth dyad has no meaning.
- New `power` voicing category in `VoicingCategories.json`, ordered ahead of `triad` in
  the priority list so the octave-doubled root/fifth/root shape is not mis-labelled a
  triad. Exposed as a filter checkbox on the Editor voicings form and in the
  `VoicingStructureGlossary` tooltips.
- Slash chords now affect voicing generation. `ChordSpec` gains `Bass`; `VoicingSearch`
  emits only voicings whose lowest sounding pitch matches it, so `C/G` and `C` no longer
  return the same set. Previously the bass was parsed and then discarded at expansion.
- A slash bass foreign to the chord (`C/D`, `Dm7/G`) is appended to the tone set with the
  `ChordSpec.BassFunction` (`"bass"`) label, keeping the spelling the user typed, so the
  fretboard search can reach it. An inverted chord tone (`C/G`) keeps its own function.
- `bassNote` added to the `chord` command payload and `chord.schema.json`.
- Unplayable slash bass reports a warning on the CLI envelope and a non-fatal notice in the
  Editor result panel (new `VoicingResultModel.Notice`) instead of silently falling back to
  root position.
- Rows in the Voicings screen's "next chords" panel are clickable, drilling into voicings for
  the candidate the same way the Predictions screen does. New `VoicingResultModel.TuningValue`
  carries the raw tuning the search ran with — result cards stack, so a card must query its
  own tuning rather than re-reading a form that may have moved on. Active-row highlighting
  moved into `window.selectChordRow()`, which scopes the clear to the owning `.result-card`.
- **Symbol emission (D44).** `ChordSymbol.ToSymbol()` renders a parsed chord back to text in
  canonical grammar order (root, quality, extensions, alterations, bass), normalising aliases
  (`C-` → `Cm`) and modifier order (`C7b13#9` and `C7#9b13` both emit `C7#9b13`). Verified by a
  round-trip property test over the full grammar cross-product.
- **Spelling-correct transposition (D45).** `Interval.DiatonicSteps` derives letter-steps from
  a quality label's digits; `Note.Transpose(Interval)` walks the stacked-thirds letter then
  corrects the accidental, so `m2` and `A1` — both one semitone — spell as `Db` and `C#`
  respectively. Overflow (more than ±2 accidentals) respells enharmonically instead of
  throwing, reporting a human-readable note (`"Fbb respelled as Eb"`) rather than crashing a
  transform chain three steps in. New `Note.Spell(pitchClass, preferredLetter)` picks the
  simplest accidental for transforms with no diatonically "correct" spelling to aim for
  (inversion, serial multiplication), so double application returns to the original spelling.
- **Transform engine (D50) and eleven key-blind transforms** in `Dadabe.Core.Transform`:
  `transpose`, `retrograde`, `rotate`, `invert`, `interval-negate`, `interval-reverse`,
  `interval-multiply`, `quality-map`, `reduce`, `tritone-sub`, and `plr` (Neo-Riemannian
  P/L/R). Chord-shape guards (D53) skip thirdless chords under `quality-map`/`plr`,
  non-triads under `plr`, and non-dominants under `tritone-sub`, reporting a `TransformNote`
  rather than silently no-opping. Chains (`TransformChain`) parse once at the front and emit
  once at the back. `TransformCatalog` is the DI-friendly registry.
- **Playability ranking (D51).** `PlayabilityRanking.Score` runs the deployed
  `VoicingSearch`/`VoiceLeadSolver` pipeline over a transform's output and reports worst
  comfort, total hand-travel distance, fret span, and any chord with no voicing above the
  floor — which always sorts last, labelled rather than hidden.
- **CLI `transform` subcommand**: `dadabe transform --chords "C F G" --chain
  "retrograde,transpose:M2"` or `--all` to run the whole catalogue ranked by playability.
  New `schemas/transform-result.schema.json`; `schemas/transformation.schema.json` tightened
  from its placeholder state to enumerate the eleven registered transform ids.
- **Editor transform panel** on `/progressions`: a chain builder (add/remove steps, each a
  transform type plus free-form `k=v;k=v` parameters), a diff preview, key-before/after and
  playability annotations, an **Explore** action that ranks the whole catalogue with one-click
  **Use**, and **Save as new** / **Replace** actions. `ProgressionModel` gains an optional
  `derivedFrom` field (`{ sourceSlug, chain }`) recording the transform chain that produced it.
- **Song mode foundation (D37, D38, D42, D43).** New `Song` / `SongSection` / `SongSlot` data
  model (`SongService`, one JSON file per song under `data/songs/`); a slot with a null
  `Voicing` is a chord you know but haven't fingered yet. Active-song and active-section
  selects in the header (mirroring the tuning select); selecting a song sets the active tuning
  to the song's own tuning (D38) and warns if the user then changes it while the song is
  active. Song-level hand constraints (D42) fall back to `HandModel.Default` per null field.
  Each pin records `{ modelVersion, tuning, handHash }` (D43) and is badged stale when that no
  longer matches the current model/tuning/hand.
- **Pin and fill.** Every voicings-result row gets a pin button that fills the active song
  section's matching unpinned slot (or appends a new one). *Fill unpinned slots* runs
  `VoiceLeadSolver` over the whole section, treating pinned slots as single-candidate layers
  the solver routes around — the two shapes you love stay put; everything between them is
  chosen at minimum motion.
- **Song read-outs**: inferred (or user-overridden) key per song, per-section position range
  and hardest-chord ("weakest link") labels, and completion counts. Voice-lead and song
  section views now render the per-string move for each transition (`VoiceMoveSummary`) —
  "only string 6 moves (7→8, 1 fret) — 5 common tones held" — instead of a bare scalar
  distance.
- **Chord diagram rendering.** `VoicingRow.Diagram` (computed on every voicings request since
  v0.4.1 but never rendered) now renders as a collapsible CSS-grid diagram — no JS, no canvas
  — on the voicings results table and, as the primary display, on the song chart. Shared by
  `ChordDiagramBuilder.FromVoicing`/`FromPinned` so both a live search result and a stored
  pinned shape use the same partial.
- **Song chart, export/import, and progression interop.** `/songs/{slug}/chart` is a
  print-friendly chart (`@media print` hides the shell nav) with position ranges in the
  header. Song JSON export/import round-trips the file as-is. A pasted chart becomes unpinned
  slots in a section; a saved progression can be instantiated as a section; a section can be
  promoted to a saved progression — the same whitespace-split parse path the voice-lead form
  already uses.

### Changed

- Barre comfort is no longer a flat 0.15 per barre (D15). `Voicing.ComputeComfort` now takes
  the `BarreGroup`s themselves and scores each via the new public `Voicing.BarrePenalty`,
  which weighs fret position, width, and finger. Barre cost is dominated by *where* the barre
  sits: the nut surcharge decays quadratically to zero by fret 5, so a full barre at fret 3
  costs 0.105 while the same shape at the nut costs 0.240. Width contributes at most 0.03,
  and a non-index barre is scaled by 1.7.
- Net effect on real output: F major `[1 3 3 2 1 1]` drops 0.647 → 0.557, while the same
  shape at fret 5 rises to 0.723 and `C/G [3 3 5 5 5 3]` rises 0.64 → 0.685.
- Muted strings forming an unbroken run off either end of the neck now cost **nothing**.
  They are not muted at all — the picking hand simply never strikes them. `ComputeComfort`
  takes `interiorMutedStrings` (total mutes minus the new `Voicing.CountEdgeMutes`) in place
  of a raw mute count; the 0.15 isolated-mute surcharge is unchanged. `CountIsolatedMutes`
  moved from `VoicingSearch` to `Voicing` to sit beside it.
- Combined effect of both comfort changes: `C5/G [3 3 5 5 x x]` 0.59 → 0.79, and voicings
  clearing a 70% comfort filter rise roughly 4× (C/G: 5 → 46 of 266). Sparse shapes with
  unstruck outer strings now score near the top of the range, which is correct for *comfort*
  but means comfort is even less of a proxy for musical quality than before.
- `docs/future.md`: the slash-chord half of "Slash chords and polychord input parsing" is
  done; polychords remain.

### Fixed

- The Voicings "next chords" probability bar was invisible: it painted with
  `var(--wa-color-brand-600)`, which is not a Web Awesome 3 token, and had no fallback.
  Both screens now share `.prob-cell`/`.prob-bar`/`.prob-pct` in `app.css`, with a literal
  fallback color and a 2px minimum width so low-probability rows still render a stub.
- Voicing *structure* labels rendered as blank cells wherever they were wrapped in
  `wa-tooltip`. In Web Awesome 3 the tooltip's default slot is the tooltip body, not the
  anchor, so the label was only ever shown inside the popup. `VoicingsResult`,
  `VoicingFragment`, and `VoiceLeadingResult` now use `<abbr title>` instead.

### Notes

- `ChordSpec.ContentHash` appends bass bytes only when a bass is present, so root-position
  specs keep their exact canonical form and published voicing ids do not churn.

---

## [v0.5] — 2026-06-05 — Progression voice leading + UI refresh

### Added

- `dadabe voice-lead --chords "<progression>" [options]` subcommand: finds the minimum
  fret-travel path through a chord progression using backward DP + A* search (D28, D29).
  `--solutions 1–10` returns k distinct paths. Output: `Envelope<VoiceLeadingResultDto>`.
- `VoiceLeadSolver` in `Dadabe.Fretboard`: `TotalFretDistance`, Dijkstra/A* path search,
  `BuildTransition` helper.
- `VoiceMove` (fret-based: `StringIndex`, `FromFret`, `ToFret`, `Distance`) and updated
  `Transition` (`TotalDistance`, `IReadOnlyList<VoiceMove>`) activate the D16 stubs.
- `KeyInference.cs` in `Dadabe.Core.Chord`: infers key centre from context chord list;
  `Classify` returns `Diatonic / ValidNonDiatonic / Unrelated` per candidate.
- Context-weighted prediction scoring (D33): `NextChordPredictor.Predict` accepts optional
  `IReadOnlyList<ChordSpec>? context`; diatonic candidates get ×1.4, unrelated ×0.7.
  `PredictCommand` parses context chords and passes them through.
- `schemas/voice-lead-result.schema.json` (embedded resource in `Dadabe.Cli`).
- Web Awesome UI migration: all Editor slices migrated from BeerCSS + Material Symbols to
  `<wa-*>` Web Awesome components. `PageShell.cs` swaps CDN; `app.css` pruned of
  BeerCSS and `.vcd*` rules.
- Editor voicings form — Advanced options collapsible (`<wa-details>`): exposes `--frets`,
  `--span`, `--min-strings`, `--max-strings`, `--allow-open`, `--allow-barre`,
  `--allow-thumb`, `--categories`. `VoicingRoutes` maps these to `SearchParams`.
- Editor `/voice-lead` page: form with tuning/solutions/minComfort, A* result panel
  (chord | ASCII | structure | comfort | distance columns), and "About voice leading"
  theory panel.
- `VoicingStructureGlossary.cs`: static inline definitions shown as `<wa-tooltip>` on
  voicing structure cells throughout the Editor.
- Next-chord prediction theory note in `VoicingsResult.cshtml` (collapsible `<wa-details>`).

### Changed

- `Transition.cs`: `VoiceMove` is now fret-based (`StringIndex / FromFret / ToFret /
  Distance`); `Transition.TotalDistance` replaces `TotalSemitones`.
- `VoicingRoutes.cs`: reads advanced form fields; falls back to `SearchParams.Default`
  values when absent.
- `JsonEnvelope.ToolVersion` bumped to `0.5.0`.
- `VoicingsResult.cshtml`: index column removed; structure column gains glossary tooltip;
  next-chord section gains collapsible theory note.

### Notes

- No breaking schema changes; all new envelope fields are nullable/optional.
- Chord diagram rendering remains deferred (`.vcd*` CSS and `ChordDiagram` model are
  leftover scaffolding cleaned up in this release).
- Bass-note enforcement in voicing search (Q4) remains deferred beyond v0.5.

---

## [v0.4] — 2026-06-04 — Prediction pipeline + slash chords

### Added

- `dadabe predict --input <file.json>` subcommand: reads a `prediction.schema.json`
  request, runs `NextChordPredictor`, applies hard filters, and writes a
  schema-valid `Envelope<PredictionResultDto>`.
- `byChord` and `byQuality` hard filters in `PredictCommand`; unimplemented filter
  types (`byScale`, `byMode`, `custom`) emit a warning and are skipped.
- `--validate-schema` flag on all subcommands: post-write JSON schema validation;
  exit code `3` on violation (`SchemaViolationException`).
- Schema files embedded as assembly resources in `Dadabe.Cli` for offline validation.
- Slash chord parsing: `C/E`, `Cmaj7/G`, `F#m7/A` etc. are now accepted by
  `ChordParser`. Bass note stored in `ChordSymbol.Bass` and emitted as
  `chord.bassNote` in JSON output. Grammar `rejectSlash` flag retained for overlays.
- `Note? Bass` on `ChordSymbol`; included in `ContentHash` when non-null.
- `PredictCommandTests` — round-trip, filter-wiring, per-request entropy override,
  context echo, and schema-validation tests.
- `SchemaValidationTests` — invalid-output `SchemaViolationException` and
  valid-output exit-0 tests via the CLI pipeline.
- `ChordParserTests` — slash chord round-trip, bad bass token, double-slash,
  overlay-rejection tests.
- `docs/v0.4/examples/prediction-request.json` and `prediction-response.json`.

### Changed

- `voicings` subcommand gains `--min-comfort` (filter voicings below comfort threshold)
  and `--top-n` / `--entropy` are now documented in the usage text.
- `JsonEnvelope.ToolVersion` bumped to `0.4.0`.

### Notes

- No breaking schema changes; all new fields are nullable/optional.
- Bass-note enforcement in voicing search (filter to voicings where lowest note
  matches slash chord bass) is deferred to v0.5.
- Context-weighted prediction scoring (D27) deferred to v0.5.

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
