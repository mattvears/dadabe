
# Dadabe v0.2 - TODO (concise)

Archived full TODO: docs\v0.2\source\todo-full-2026-05-28.md

Short context: The full todo was archived to keep docs/todo.md concise. See the archived file for the full history and rationale.

## Top decisions (excerpt)

| # | Topic | Decision
| --- | --- | ---
| D1  | Voicing categories       | **Core 5**: triads, shell, drop-2, drop-3, spread                                                                                                                                                                                                                                                                                                           |
| D2  | Chord grammar            | **Standard + altered**: maj/m/dim/aug/sus, 6/7/maj7/m7/m7b5/dim7, extensions 9/11/13, alterations b5 #5 b9 #9 #11 b13. No slash chords or polychords in v0.1.                                                                                                                                                                                               |
| D3  | Named tunings            | **Common catalog from JSON**: DADABE, Standard, Drop D, DADGAD, Open G, Open D. Loaded from `tunings.json`; user-extensible without recompile.                                                                                                                                                                                                              |
| D4  | Inclusion criterion      | **Permissive**: any playable voicing whose sounded pitch classes âŠ† chord spec is emitted. No quality-based filtering.                                                                                                                                                                                                                                       |
| D5  | `--limit` cut order      | **Deterministic generation order**, lexicographic ascending over the per-string position tuple `(pos[0], â€¦, pos[n-1])`; each `pos[i]` is the fret on string `i` (0 = open) with muted encoded as `int.MaxValue` so fretted positions always sort before muted on a per-string basis. The search iterates the cartesian product in this order; `--limit` truncates the prefix. No separate sort step. Full spec in [design.md Â§7 step 3](design.md#7-algorithm-sketch--voicing-generation). |
| D6  | Playability model        | **Full hand model required.** A voicing is emitted only if a valid fingering exists â€” every fretted note assigned to a finger (1â€“4, plus thumb), respecting per-finger reach, inter-finger stretch, barre semantics, and physical mute legality. The assigned fingering is part of the JSON output, not just evidence used internally.                      |
| D7  | Programmatic library API | **CLI only in v0.1.** `Dadabe.Core` and `Dadabe.Fretboard` stay internal to the solution; no NuGet publish, no public library contract. Revisit post-v0.1.                                                                                                                                                                                                  |
| D8  | Open strings             | **`--allow-open` defaults to true.** Open strings are welcome in arbitrary chord contexts; no per-chord opt-in needed.                                                                                                                                                                                                                                      |
| D9  | License                  | **MIT.**                                                                                                                                                                                                                                                                                                                                                    |
| D10 | .NET version & tooling   | **`net9.0`.** Tooling per [design.md Â§9](design.md#9-tech-stack): `System.CommandLine`, `System.Text.Json` + source gen, `xUnit` + `FluentAssertions` + `FsCheck.Xunit`, `JsonSchema.Net`.                                                                                                                                                                  |
| D11 | Hand-model constants     | Defaults for the v0.1 `HandModel`: max fret 15; max span 4 frets; min strings 3; max strings 6; inter-finger stretch 1â†”2 â‰¤ 2, 2â†”3 â‰¤ 2, 3â†”4 â‰¤ 2, 1â†”4 â‰¤ 4; thumb-over (T) on lowest string only at fret â‰¤ 5, **off by default** behind `--allow-thumb`; max 1 simultaneous barre per voicing.                                                                 |
| D12 | Enharmonic spelling      | **Baked into v0.1.** A `Note` type carries letter (Aâ€“G) + accidental (âˆ’2..+2); `PitchClass` (0â€“11) is math-only and never user-facing. `F#maj7` spells as Fâ™¯/Aâ™¯/Câ™¯/Eâ™¯, `Dbmaj7` as Dâ™­/F/Aâ™­/C. All JSON `note` strings and chord pitch-class names are spelled per chord context. Octave in SPN follows the letter (`Cb4` sounds like B3 but is labelled 4). |
| D13 | JSON schema versioning   | **Baked into v0.1.** Envelope carries a top-level `"schemaVersion": "1"` separate from `"version"` (tool semver). Consumers pin the schema independently of the tool release. |
| D14 | Hand-model in output     | **Baked into v0.1.** The envelope's `input` block records `"handModel"` â€” name plus fully resolved parameters (stretch matrix, thumb policy, max barres) â€” so a run is reproducible without re-supplying flags. |
| D15 | Comfort score            | **Baked into v0.1.** Each `Voicing` carries `comfort: float âˆˆ [0, 1]` derived from span, mute count, position, and barre count. **Reported, not used for ordering** â€” D5 (deterministic generation order) still governs emission. Consumers may sort by comfort themselves. |
| D16 | Transition types         | **Baked into v0.1 (types only).** `Transition` and `VoiceMove` records live in `Dadabe.Core` so v0.2 progression features are additive. v0.1 produces none and the CLI exposes no command that uses them. |
| D17 | Content-hash IDs         | **Baked into v0.1.** Every memoizable domain type implements `IContentHashable` and exposes a `ContentHash` derived from canonical serialization of its identity fields. Format `<namespace>:<version>:<hex-digest>` (SHA-256 truncated to 128 bits, lowercase hex), e.g. `voicing:1:b5f3a8d2c1e4f6a78b9c0d1e2f3a4b5c`. The JSON `id` field carries this string; same inputs â†’ same id across runs and machines. Affected types: `Tuning`, `ChordSpec`, `HandModel`, `Voicing`, `Fingering`, `Transition`. Resolves [E6](#future-expansion-risks). Full spec in [design.md Â§8.1](design.md#81-content-hashes-d17). |
| D18 | Memoization layer        | **Baked into v0.1.** `Dadabe.Core.Memo` defines `IMemo<TIn, TOut>` keyed on content hashes (D17); ships `InMemoryMemo<,>` as the only implementation. `VoicingSearch`, `FingeringSolver`, `ChordExpander`, and `Classifier` accept an optional memo and consult it before computing. The CLI does **not** enable a cache in v0.1 â€” the layer is library surface for v0.2 to plug into. Persistent backends (filesystem, SQLite) reserved for v0.2. Namespace coverage for all deferred features pre-allocated as constants. Cache invariant: every golden test runs with and without a memo and must produce byte-identical output. Full spec in [design.md Â§8.2](design.md#82-the-memo-interface-d18). |
| D19 | Chord grammar config     | **Baked into v0.1.** Chord grammar is defined by `src/Dadabe.Core/Chord/ChordGrammar.json` (embedded resource). Schema: `forms[]` (atomic chord patterns with `tokens`, `tones`, `required`) + `modifiers[]` (additions and alterations) + `parseRules` (root regex + longest-token-first matching). Parser tokenizes `<root><form><modifier>*` longest-match-first per form, then any-order modifiers. Default catalog covers D2's standard + altered vocabulary. The required-tones table falls out of the config (each form's `required` âˆª each applied modifier's `required`) â€” no separate table. User-extensible via filesystem overlay (per E9). Full spec in [design.md Â§10.2](design.md#102-chordgrammarjson-per-d19). |
| D20 | Voicing category config  | **Baked into v0.1.** Categories defined by `src/Dadabe.Fretboard/VoicingCategories.json` (embedded resource). Categories evaluated in `priority` order; first matching template wins; shipped `spread` is a `matchAny: true` fallthrough so every voicing has a non-null category. Rule types: `noteCount`, `noteCountRange`, `allFunctionsIn`, `requireFunctions`, `forbidFunctions`, `functionSequenceLowToHigh`, `adjacentIntervalMinSemitones`, `matchAny`. Default catalog implements the Core 5 (D1): triads, shell, drop-2, drop-3, spread. Drop-2 and drop-3 are spelled as four function-sequence templates each (one per inversion). User-extensible via filesystem overlay. Full spec in [design.md Â§10.3](design.md#103-voicingcategoriesjson-per-d20). |

## Current work (excerpt of checkboxes)

- [x] `dotnet new sln -n Dadabe` at repo root.
- [x] Create projects:
- [x] Create test projects:
- [x] Wire project references: `Cli â†’ Core, Fretboard`;
- [x] Target framework `net9.0` (per D10). Enable `Nullable` and
- [x] Add NuGet deps:
- [x] `Directory.Build.props` with shared `<LangVersion>`, `<Nullable>`,
- [x] `.editorconfig` for C# style; verify `dotnet format` passes.
- [x] Add `LICENSE` (MIT, per D9).
- [x] Extend `.gitignore` with .NET entries (`bin/`, `obj/`, `*.user`,
- [x] `Memo/ContentHash.cs` *(D17)*
  - [x] `ContentHash` readonly record struct
  - [x] SHA-256 helper that hashes a byte sequence and returns the first
- [x] `Memo/IContentHashable.cs` â€” interface exposing
- [x] `Memo/Canonical.cs` â€” deterministic byte serialization helpers
- [x] `Memo/IMemo.cs` *(D18)*
  - [x] Generic `IMemo<TIn, TOut> where TIn : IContentHashable
  - [x] Methods: `bool TryGet(TIn key, out TOut value)`;
- [x] `Memo/InMemoryMemo.cs` â€” `ConcurrentDictionary<string, TOut>` keyed on
- [x] `Memo/Namespaces.cs` â€” string constants for every memo namespace.

## Links

- Full archived TODO: docs\v0.2\source\todo-full-2026-05-28.md

(Edit this concise file to add the most important top-level todos; keep it under 200 lines.)

## v0.3 next steps

- [ ] Add a prediction response schema: `schemas/prediction-result.schema.json` (shape: `chord: string`, `score: float`, `reasons: string[]`).
- [ ] Implement NextChordPredictor API surface in Dadabe.Core to accept `NextChordPredictionRequestDto` and return a typed `PredictionResultDto`.
- [ ] Wire CLI flags: `--predict --input <file.json>` and `--validate-schema` to validate requests/responses against schemas.
- [ ] Add schema-validation tests (JsonSchema.Net) asserting request and response shapes and current output compatibility.
- [ ] Add example request/response JSON under `docs/v0.3/examples/` for prediction, scale, mode, and cadence.
- [ ] Add a CI job step to run schema validation against examples on PRs.
- [ ] Document prediction requirements: docs/v0.3/prediction-requirements.md

(These are v0.3 scaffolding tasks; keep future feature work in docs/future.md.)

## v0.3 scaffolding created

Scaffolding for a polish minor release has been added: docs/v0.3/plan.md

Scope: polish and quality improvements (docs, tests, formatting, CLI UX, small bugfixes). No major new features. All future work remains planned in docs/future.md.
