# Copilot / Assistant instructions for Dadabe

Purpose: help future Copilot sessions understand how to build, test, and reason about this repository and point out project-specific patterns that matter across files.

---

## Quick commands

- Build solution
  - dotnet build
  - CI: dotnet build --configuration Release --no-restore

- Run CLI (example)
  - dotnet run --project src/Dadabe.Cli -- voicings Cmaj7 --tuning DADABE --pretty

- Run all tests
  - dotnet test
  - CI: dotnet test --configuration Release --no-build --logger "trx;LogFileName=test-results.trx"

- Run a single test project
  - dotnet test tests\Dadabe.Core.Tests\Dadabe.Core.Tests.csproj

- Run a single test by name (example)
  - dotnet test tests\Dadabe.Fretboard.Tests\Dadabe.Fretboard.Tests.csproj --filter "FullyQualifiedName~VoicingSearchTests"

- Formatting / linting
  - dotnet format
  - CI format check: dotnet format --verify-no-changes
  - Markdown lint & spell: npm run lint:docs (uses markdownlint + cspell)

- JSON/schema validation
  - Schemas live in `schemas/`. Tests validate CLI JSON against these files during CI.

---

## High-level architecture (big picture)

- Solution layout
  - src/Dadabe.Core: core music-theory domain types and deterministic helpers (ContentHash, chord expansion)
  - src/Dadabe.Fretboard: fretboard, fingering, playability, voicing search
  - src/Dadabe.Cli: console front-end that builds an Environment and emits JSON envelopes
  - tests/: unit and golden tests for CLI and libraries
  - schemas/: JSON Schema files that pin the CLI's JSON output

- Data & runtime flow
  1. CLI parses args and calls Environment.Build(workingDirectory, cliArgs) (see Environment.cs).
  2. Catalogs (tunings, chord grammar, voicing categories) are loaded from embedded defaults plus a same-named CWD overlay (embedded resource winning rules in code).
  3. Chord parsing & expansion (Core) → Voicing generation (Fretboard) → Fingering solver → JSON envelope written by CLI.
  4. Output is validated by schema-based tests; golden tests assert stable output.

- Release / contracts
  - JSON envelope carries `schemaVersion` (schema pin) and `version` (tool semver). See schemas/envelope.schema.json.

---

## Key conventions and repo-specific patterns (important to follow)

- Content-hash IDs
  - Domain objects expose canonical content hashes (`IContentHashable`, ContentHash record). JSON `id` fields are content hashes (format: `<namespace>:<version>:<hex>`). Searches and memos key on these.

- Deterministic emission & ordering
  - Voicing generation uses a deterministic lexicographic order over per-string position tuples; `--limit` truncates a prefix of that order (see design.md §7 and D5 in docs/todo.md). Tests rely on deterministic output.

- Model gating / ModelVersion
  - `ModelVersion` enum gates behavior (v1/v2). Example: `Voicing.Functions` is empty in v1; future features are gated by the enum. When changing behavior, add a model version increment rather than mutating v1 outputs.

- Embedded catalogs + overlays
  - Default catalogs live under `src/*/` as embedded JSON (e.g., `Tunings.json`, `ChordGrammar.json`, `VoicingCategories.json`). Runtime loads embedded defaults then overlays files from CWD with same names to allow non-code customization during runs/tests.

- Schema-first JSON contract
  - The CLI output shape is pinned by `schemas/*.schema.json`. Update schemas when adding consumer-visible fields; prefer additive changes and schema version bumps for breaking changes.

- Memoization & golden-test invariant
  - In-memory memo layer exists; golden tests assert identical output with and without memoization. When adding caching/backends, preserve byte-identical golden outputs.

- Tests & CI
  - CI runs `dotnet format --verify-no-changes` and `dotnet test` (see .github/workflows/ci.yml). Keep tests fast and deterministic; golden tests are part of the test suite.

---

## Files to inspect first when making changes
- Environment.cs (runtime wiring / catalog loading)
- VoicingSearch.cs, FingeringSolver.cs (generation & playability)
- schemas/*.schema.json (JSON contract)
- src/Dadabe.Core/Memo and ContentHash.cs (content-hash invariants)
- docs/todo.md and docs/design.md for rationale and decision IDs referenced in code

---

If this file already exists, append missing commands and these repository conventions. Keep the file minimal and factual so assistants can rely on it.

---

## Documentation folder pattern (docs/)

Purpose: keep the docs folder navigable and ensure long-running design history is archived by version so todo.md remains a concise, actionable index.

- Canonical files
  - docs\design.md — long-form design and rationale.
  - docs\todo.md — current short TODO, decisions summary, and links to archives.
  - docs\v{version}\source\*.md — archived full documents (per-release archives). Example: docs\v0.1\source\design.md and docs\v0.2\source\pseudocode.md.

- Pattern
  1. Active, short-form docs live at top-level (docs\todo.md, docs\design.md can be selective summaries).
  2. When a document grows large or needs historical retention, move the full content into an archive folder: docs\v{version}\source\<topic>-<YYYY-MM-DD>.md and replace the top-level file with a short index that links to the archive.
  3. Keep archives named with version and date for easy discovery (use ISO date: YYYY-MM-DD).

- Archiving rules for docs\todo.md (new requirement)
  - Goal: docs\todo.md must remain concise and focused. Target maximum: 200 lines or 20KB. If exceeded, archive.
  - When to archive: any of these triggers
    - file length > 200 lines
    - file size > 20 KB
    - major milestone/release cut (e.g., v0.1 → v0.2)
  - Archive process (PowerShell-friendly commands)
    1. Create archive dir (if needed):
       - if (!(Test-Path "docs\v0.2\source")) { New-Item -ItemType Directory -Path "docs\v0.2\source" -Force }
    2. Move full file into archive with versioned name:
       - git mv docs\todo.md docs\v0.2\source\todo-full-2026-05-28.md
    3. Create a new concise docs\todo.md containing:
       - short header (one-line release summary), top 5 decisions, current top-level todos, and links to the archived full file(s).
    4. Commit:
       - git add docs\todo.md docs\v0.2\source\todo-full-2026-05-28.md
       - git commit -m "docs: archive todo.md -> docs/v0.2/source/todo-full-2026-05-28"
    5. Push and tag release as needed.

  - Content expectations for the new concise docs\todo.md:
    - Max 200 lines; prefer 80–120 lines
    - Include: release header, 2–3 sentences of context, a short Decisions table (D1..), Work breakdown top-level checkboxes, and explicit links to archived full docs.
    - Archive files must be preserved verbatim and are the source of truth for historical rationale.

- Cross-references
  - When archiving, update docs\design.md or the new concise todo.md to include links to archives.
  - Tests and scripts that reference docs paths should use the archived path if they depend on historical content.

---

Maintainers: follow the pattern on release or when todo.md grows. Ask for automation later (e.g., a script to auto-archive when file size threshold exceeded).
