# Dadabe v0.3 — Scaffolding (polish release)

Overview

This minor release focuses on polishing existing features: documentation, tests, formatting, CLI UX, and packaging. No breaking changes or large new features are planned in v0.3; all deferred/future work remains tracked in docs/future.md.

Goals

- Polish user-facing CLI output and help text.
- Run and enforce formatting and linting across the repo (dotnet format, markdownlint, cspell).
- Tighten and expand unit/golden tests where coverage gaps are obvious.
- Fix small playability/edge-case bugs discovered in v0.1/v0.2 testing.
- Improve developer workflows: scripts, CI notices, and release notes template.

Non-goals

- No new domain features (no new voicing categories, no progression engine). Those remain in docs/future.md.

Checklist

- [ ] Update README and top-level docs with any UX changes.
- [ ] Polish CLI help and examples (src/Dadabe.Cli).
- [ ] Run dotnet format and fix violations; update CI format check if necessary.
- [ ] Expand unit tests for VoicingSearch and FingeringSolver; ensure golden tests are stable.
- [ ] Verify schema compatibility and avoid JSON contract breaks; update schemas only if necessary and document schemaVersion changes.
- [ ] Prepare release notes and changelog entry.
- [ ] Draft contracts & scaffolding notes: docs/v0.3/contracts.md (placeholders for progression, transformation, rendering, and parser extension points).
- [ ] Document prediction requirements (context, filters, response schema) in docs/v0.3/prediction-requirements.md (design-only for v0.3).
- [ ] Tag release v0.3 when ready.

Files/places to inspect

- src/Dadabe.Cli (help text, JsonEnvelope output)
- src/Dadabe.Fretboard (voicing search and solver polish)
- tests/ (expand/unstable golden tests)
- docs/ (update READMEs and concise todo)

Notes

- Preserve content-hash ID invariants and memoization golden-test invariant when changing behavior.
- Keep docs/todo.md concise; archive large history to docs/v{version}/source/ as described in .github/copilot-instructions.md.
