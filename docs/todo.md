# Dadabe v0.5.2 — TODO (concise)

v0.5 is complete. v0.5.1 is a bugfix release. v0.5.2 is the transform engine + song mode
foundation.

- Design: `docs/v0.5.2/design.md`
- Source specs: `docs/v0.5.2/progression-transforms.md`, `docs/v0.5.2/ui-song-mode.md`
- Deferred work: `docs/v0.6/design.md`
- Bug tracker: `docs/v0.5.1/bugs.md`
- Archived v0.3 TODO: `docs/v0.3/todo-full-2026-05-29.md`

Decisions D1–D20 are archived in `docs/v0.1/source/design.md` and subsequent version
design docs. D37–D53 are specified in the two v0.5.2 source specs; D54+ in v0.6.

## v0.5.1 work

- [ ] Triage all bugs in `docs/v0.5.1/bugs.md`
- [ ] Fix checklist (populate from bugs.md once triaged)

## v0.5.2 work

Full checklist in `docs/v0.5.2/design.md` §14. Summary:

### Foundation (blocks everything else)

- [x] `ChordSymbol.ToSymbol()` + round-trip property test (D44)
- [x] `Interval.DiatonicSteps`, `Note.Transpose(Interval)`, enharmonic overflow policy (D45)

### Transform engine

- [x] Activate `ITransformation`; `TransformCatalog` + chain application (D50, partial)
- [x] Eleven key-blind transforms: transpose, retrograde, rotate, invert, interval-\*,
      quality-map, reduce, tritone-sub, P/L/R
- [x] Chord-shape guards — thirdless, non-triad, non-dominant, slash (D53)
- [x] Playability ranking + apply-all (D51)
- [x] `transform` CLI subcommand, DTOs, schemas
- [x] Editor transform panel: chain builder, diff preview, provenance

### Song mode foundation

- [x] Song / Section / Slot model + `SongService` CRUD (D37)
- [x] Active song + section selects; song owns its tuning (D38)
- [x] Song hand constraints (D42); pin provenance (D43)
- [x] Pin from voicings tab; fill unpinned slots via `VoiceLeadSolver`
- [x] Read-outs: key display, position map, practice targets, per-string transitions
- [x] Chord diagram rendering (retires scaffolding computed since v0.4.1)
- [x] Song chart + print; export; progression interop

### Docs

- [x] `CHANGELOG.md` v0.5.2 entry

## Guiding constraint for v0.5.2

**Nothing branches on `KeyInference`.** Key is displayed as read-only information; no
transform depends on it succeeding. That single rule is what keeps the release cheap and
is why the whole key-aware family sits in v0.6.

(Keep this file under 200 lines.)
