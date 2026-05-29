# v0.3 — Contracts & scaffolding notes

Purpose: capture the minimal contracts and scaffolding to prepare for future features (from docs/future.md) while keeping v0.3 a polish release. These are design notes and tests to add now so later feature work integrates cleanly.

1) JSON/schema compatibility

- Confirm schemas/ are authoritative. Any consumer-visible field changes must update schemas and the `schemaVersion` in envelopes.
- Add schema snippets for planned extension points:
  - `transformation` union type (placeholder) for harmonic transformations.
  - `progression` envelope shape: list of chord symbols + optional `tempo`/`meter` metadata.
- Add backward-compatibility tests: ensure current sample outputs still validate against schemas.

1) Progressions & transitions

- `Dadabe.Core` already defines `Transition`/`VoiceMove` types; add `Progression` DTO in `src/Dadabe.Cli/Io` as a placeholder and schema in `schemas/progression.schema.json` (empty/optional fields allowed).
- Add a test that serializes/deserializes `Transition` structures to validate round-trip.

1) Transformation API surface

- Define an interface `ITransformation` in `Dadabe.Core` (obvious placeholder) with metadata + parameters so UI/frontends can present transformation choices.
- Add JSON schema placeholder `schema/transformation.schema.json`.

1) Voicing category extension

- Expose `VoicingCategoryCatalog` overlay behavior in docs and add unit tests ensuring runtime overlays from CWD are applied and deterministic.
- Add a small sample `schemas/voicing-category.schema.json` example for third-parties.

1) Parsing extensions (slash chords, polychords)

- Mark parser extension points in `src/Dadabe.Core/Chord/ChordParser.cs` with TODO comments; add tests asserting current parser rejects slash/polychord input (preserves current behavior) and a future test marked [Skip] that documents desired behavior.

1) Rendering & frontends

- Add an `IOutputRenderer` interface placeholder in `src/Dadabe.Cli/` to abstract JSON sink vs diagram renderers.
- Create `docs/v0.3/rendering.md` later with API expectations for diagram/tab sinks.

1) Memoization & caching

- Add `IMemo` persistence backend interface doc and a test scaffold that exercises InMemoryMemo vs a mock persistent backend to assert invariants.

1) Tests & golden expectations

- Add a checklist and tests asserting: schema validation, memo invariant (same output with/without memo), deterministic emission ordering, and formatting rules.

1) Developer ergonomics

- Add CLI hooks for `--dry-run` and `--validate-schema` flags in plan and tests to help frontends and CI validate outputs without publishing.

---

Next actions (recommended):

- Create `schemas/progression.schema.json` and a DTO `ProgressionDto` as a placeholder.
- Add unit tests listed above under tests/ with TODO markers for future feature implementation.
- Keep all new schemas additive and conservative to avoid breaking consumers.

(These notes are intentionally lightweight — v0.3 is polish-only. Implementations remain for future releases.)
