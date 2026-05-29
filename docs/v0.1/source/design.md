# Dadabe v0.1 — Design Document

## 1. Purpose

A first, deliberately small slice of Dadabe: a command line tool that, given a
tuning and a chord symbol, emits all musically meaningful playable voicings of
that chord on the fretboard as JSON.

Everything else in the [README](../README.md) — harmonic transformations,
progressions, inner-line generation, chord-melody — is **out of scope for v0.1**.
The goal is to lock down the core domain model (notes, intervals, tunings,
fretboard, voicings) and a stable JSON contract, so future features compose on
top.

## 2. Scope

### In scope (v0.1)

- Configurable tuning, default `DADABE` (D2 A2 D3 A3 B3 E4).
- Parse a chord symbol (e.g. `Cmaj7`, `G7b9`, `Dm11`) into pitch classes.
- Enumerate fretboard positions where the chord's pitch classes are reachable.
- Filter positions by playability constraints (hand span, barres, mutes).
- Classify each voicing — the **Core 5** categories (triads, shell, drop-2,
  drop-3, spread) per [todo.md D1](todo.md#decisions-locked).
- Emit results as JSON to stdout or to a file.

### Out of scope (deferred)

- Harmonic transformations (tritone sub, negative harmony, modal interchange).
- Progressions and minimal-motion voice leading between chords.
- Inner-line generation.
- Chord-melody harmonization.
- Rendering (chord diagrams, tab, notation, audio).
- Interactive / TUI / server modes.

## 3. CLI Surface

A single binary `dadabe` with subcommands. v0.1 ships one real subcommand:

```text
dadabe voicings <chord> [options]

  <chord>                    Chord symbol, e.g. "Cmaj7", "G7b9", "F#m11"

  --tuning <name|spec>       Named tuning or explicit spec.
                             Named:   DADABE (default), STANDARD, DROP_D, ...
                             Spec:    "D2,A2,D3,A3,B3,E4" (low to high)
  --frets <n>                Max fret to consider (default: 15)
  --span <n>                 Max fret span per voicing (default: 4)
  --min-strings <n>          Minimum strings sounded (default: 3)
  --max-strings <n>          Maximum strings sounded (default: 6)
  --allow-open               Allow open strings (default: true)
  --allow-barre              Allow barre voicings (default: true; max 1 per voicing)
  --allow-thumb              Allow thumb-over fretting (default: false;
                             when enabled: lowest string only, fret ≤ 5)
  --hand-profile <name>      Named hand model (default: Default)
  --categories <list>        Restrict output to listed categories,
                             e.g. "shell,drop-2,quartal"
  --limit <n>                Cap on returned voicings (default: 100)
  --out <path>               Write JSON to file (default: stdout)
  --pretty                   Pretty-print JSON
```

Two helper subcommands, both thin wrappers over the same domain code, are also
useful for debugging and downstream tooling:

```text
dadabe tuning <name|spec> [--out path] [--pretty]
  # Emits the tuning's resolved string pitches as JSON.

dadabe chord <symbol> [--out path] [--pretty]
  # Emits the parsed chord (root, quality, extensions, pitch classes) as JSON.
```

Exit codes: `0` on success, `1` on bad input (unparseable chord/tuning), `2`
on unexpected error.

## 4. Domain Model

The core types, in rough dependency order. Concrete C# types live under
`src/Dadabe.Core/` and `src/Dadabe.Fretboard/` (see §6).

- **PitchClass** — integer `0..11` (C=0). **Math only** — never serialised
  to the user. Use `Note` for anything the user types or sees.
- **Note** — `{ Letter: A..G, Accidental: −2..+2 }`. Carries enharmonic
  spelling. `Note(B, +1)` and `Note(C, 0)` share `PitchClass` 0 but are
  distinct `Note`s, and round-trip through parse/format independently.
- **Pitch** — `{ Note, Octave }`, plus a derived MIDI number for ordering.
  **Octave follows the letter, not the sound:** `Cb4` is labelled octave 4
  even though it sounds like B3 (both MIDI 59).
- **Interval** — semitone distance, with a quality label (`m3`, `M3`, `P5`,
  `b9`, `#11`, …).
- **Tuning** — ordered list of open-string `Pitch`es, low to high. Strings are
  indexed `0` (lowest) to `n-1` (highest). Identity carries the open-string
  *spellings* (letter + accidental + octave), not just sounding MIDI, so a
  re-spelled tuning hashes distinctly (per §8.1).
- **ChordSymbol** — parsed result of a symbol string: `{ Root: Note, Quality,
  Extensions[], Alterations[], Bass?: Note }`. Root and bass keep the
  spelling the user typed (`F♯maj7` ≠ `G♭maj7`).
- **ChordSpec** — the chord tones a `ChordSymbol` expands to. Each tone
  carries a **function** (`1`, `3`, `5`, `b7`, `9`, `#11`, …), an
  **interval quality** (`P1`, `M3`, `P5`, `m7`, `M9`, `A11`, …), and a
  **spelled `Note`** computed by the stacked-thirds letter walk: third =
  root letter + 2, fifth = root letter + 4, seventh = root letter + 6,
  with accidentals adjusted so the semitone distance matches the interval
  quality. `F♯maj7 → F♯ A♯ C♯ E♯`; `D♭maj7 → D♭ F A♭ C`. Identity
  includes the spelled `Note`, so `F♯maj7` and `G♭maj7` hash distinctly
  even though their pitch-class multisets coincide (per §8.1).
- **FretPosition** — `{ String, Fret, Pitch, ChordTone, DisplayNote }`.
  `Pitch` carries the sounding pitch; `DisplayNote` is the chord-context
  spelling (e.g. the same fret may be written `D♯3` for `B7` and `E♭3` for
  `C7`). `ChordTone` is the function this position satisfies (`null` for
  muted).
- **HandModel** — parameters defining what a human hand can play: finger reach
  per finger, inter-finger stretch matrix, thumb usage rules, max simultaneous
  barres. One named default profile with CLI overrides. The resolved
  parameters are echoed into the output envelope for reproducibility.
- **Fingering** — the contract is concrete; the solver algorithm is TBD
  (a v0.1 implementation choice). Records:

  ```csharp
  public enum Finger { Index = 1, Middle = 2, Ring = 3, Pinky = 4, Thumb = 5 }

  public enum MuteSource              // closed enum (per D21)
  {
      AdjacentUnderside,              // adjacent fretting finger underside
      BarreExtended,                  // barre extends past chord to dampen
      ThumbWrap,                      // thumb wraps neck to mute lowest string(s)
      OuterHand,                      // palm/heel contact on outer string(s)
      Unfretted                       // string not reached by any finger; only
                                      //   valid when --min-strings allows fewer
                                      //   sounded strings and no other source applies
  }

  public sealed record FingerAssignment(int String, int Fret, Finger? Finger);
      // Finger is null iff Fret == 0 (open string); never null when Fret > 0.

  public sealed record BarreGroup(Finger Finger, int Fret,
                                  int LowStringInclusive, int HighStringInclusive);

  public sealed record MuteAssignment(int String, MuteSource Source);

  public sealed record Fingering(
      FingerAssignment[] Assignments,   // one per sounded string (fret ≥ 0)
      BarreGroup[]       Barres,        // possibly empty
      MuteAssignment[]   Mutes);        // one per muted string
  ```

  A `Fingering` is **valid** if and only if every condition below holds.
  These conditions are what the solver's algorithm must enforce; *how* it
  searches is TBD.

  1. Coverage: every input string has exactly one entry across
     `Assignments` ∪ `Mutes`.
  2. Open-string rule: `Assignments[i].Fret == 0 ⇒ Finger == null`;
     `Fret > 0 ⇒ Finger != null`.
  3. Finger uniqueness: each fretted `Finger` value (1–5) appears on at
     most one fret, **unless** that finger is the head of a `BarreGroup`
     — then it may appear on every string in the group at the same fret.
  4. Barre consistency: each `BarreGroup` references a finger whose
     `Assignments` are exactly the strings in `[Low, High]` at the barre's
     `Fret`, with no gaps among strings the barre spans.
  5. Barre count: `Barres.Length ≤ HandModel.MaxBarres`.
  6. Inter-finger reach: for every pair of fingers `(f1, f2)` in
     `{1,2,3,4}` with `f1 < f2`, the absolute fret distance between their
     assignments is `≤ HandModel.Stretch[(f1, f2)]`.
  7. Thumb rule: `Finger.Thumb` may appear only if
     `HandModel.Thumb.Allowed`, only on string 0, and only at
     `Fret ≤ HandModel.Thumb.MaxFret`.
  8. Mute-source legality: every `MuteAssignment.Source` must be
     consistent with the rest of the fingering — `AdjacentUnderside`
     requires a fretting finger on a string within ±1 of the muted
     string; `BarreExtended` requires a barre that physically covers the
     muted string; `ThumbWrap` requires `Finger.Thumb` in use on string 0;
     `OuterHand` is only valid on the topmost or bottommost still-unmuted
     string; `Unfretted` is only valid if no other source applies *and*
     the string lies outside the fretting hand's footprint.

  A voicing is emitted only if a valid `Fingering` exists. When multiple
  valid fingerings exist, the solver returns one deterministically; the
  preference order is part of the algorithm spec (TBD) and must be
  documented when the solver is implemented so that `Fingering.ContentHash`
  (D17) stays stable across runs.
- **Voicing** — an array of `FretPosition` (one per string, possibly muted),
  a `Fingering`, plus derived metadata: bass note, top note, contained
  intervals, span, category, and a **comfort score** `∈ [0, 1]` computed
  from span, mute count, position, and barre count. Comfort is reported,
  not used for ordering — emission order is deterministic per §7 / D5.
  A `Voicing` is a *rendering* of a chord in a context: identity binds
  the `ChordSpec`, `Tuning`, and `HandModel` that produced it (per
  §8.1), so `voicing.id` is a faithful content hash of the JSON output
  — not of the bare physical shape.
- **VoiceMove** — `{ From: Pitch, To: Pitch, Semitones: int }`. *Defined in
  v0.1; not produced by any v0.1 command.*
- **Transition** — `{ From: Voicing, To: Voicing, Moves: VoiceMove[],
  TotalSemitones: int }`. *Defined in v0.1; reserved for v0.2 progressions.*

All identity-defining types above — `Tuning`, `ChordSpec`, `HandModel`,
`Voicing`, `Fingering`, `Transition` — implement `IContentHashable`. Their
content hashes are cache keys, JSON `id` values, and the substrate that lets
v0.2 progressions and transformations chain derivations without re-deriving
state. See §8.

Pitch classes are the chord's identity; pitches (with octave) are what land on
the fretboard. Keeping these distinct early prevents enharmonic / octave bugs
later.

## 5. JSON Output Contract

All output is a single JSON object. Shape is stable across subcommands —
results live under `data`, with shared metadata.

```json
{
  "tool": "dadabe",
  "version": "0.1.0",
  "schemaVersion": "1",
  "command": "voicings",
  "input": {
    "chord": "Cmaj7",
    "tuning": "DADABE",
    "handModel": {
      "name": "Default",
      "stretch": { "1-2": 2, "2-3": 2, "3-4": 2, "1-4": 4 },
      "thumb": { "allowed": false, "maxFret": 5, "string": 0 },
      "maxBarres": 1
    }
  },
  "data": { ... },
  "warnings": []
}
```

`schemaVersion` is the contract version for this document shape; `version`
is the tool's semver. Consumers pin the schema independently.

The `input.handModel` block echoes the resolved hand-model parameters so the
run is reproducible without re-supplying every flag.

### `voicings` payload

```json
{
  "chord": {
    "symbol": "Cmaj7",
    "root": "C",
    "quality": "maj7",
    "pitchClasses": [
      { "name": "C",  "function": "1" },
      { "name": "E",  "function": "3" },
      { "name": "G",  "function": "5" },
      { "name": "B",  "function": "7" }
    ]
  },
  "tuning": {
    "name": "DADABE",
    "strings": ["D2", "A2", "D3", "A3", "B3", "E4"]
  },
  "voicings": [
    {
      "id": "voicing:1:b5f3a8d2c1e4f6a78b9c0d1e2f3a4b5c",
      "category": "drop-2",
      "positions": [
        { "string": 0, "fret": null, "muted": true },
        { "string": 1, "fret": 3,    "note": "C3",  "function": "1" },
        { "string": 2, "fret": 2,    "note": "E3",  "function": "3" },
        { "string": 3, "fret": 5,    "note": "G3",  "function": "5" },
        { "string": 4, "fret": 0,    "note": "B3",  "function": "7" },
        { "string": 5, "fret": null, "muted": true }
      ],
      "span": 5,
      "lowestFret": 0,
      "highestFret": 5,
      "barres": [],
      "bassNote": "C3",
      "topNote": "B3",
      "openStrings": 1,
      "mutedStrings": 2,
      "comfort": 0.62,
      "fingering": {
        "assignments": [
          { "string": 1, "fret": 3, "finger": 3 },
          { "string": 2, "fret": 2, "finger": 2 },
          { "string": 3, "fret": 5, "finger": 4 },
          { "string": 4, "fret": 0, "finger": null }
        ],
        "barres": [],
        "mutes": [
          { "string": 0, "source": "thumb-wrap" },
          { "string": 5, "source": "outer-hand" }
        ]
      }
    }
  ]
}
```

Stable field rules:

- Strings are always low-to-high, indexed from 0.
- `fret: 0` is an open string; `fret: null` with `muted: true` is silenced.
- `function` uses chord-tone labels (`1`, `3`, `5`, `b7`, `9`, `#11`, …).
- Notes are scientific pitch notation (`C#4`, `Bb3`), spelled per chord
  context (per D12). The same fret position may serialise differently
  depending on which chord is being voiced.
- `comfort` is a hint in `[0, 1]`; emission order is deterministic
  (per D5), not sorted by comfort.
- `id` is a content hash (per D17), formatted as
  `<namespace>:<version>:<32-char-hex>`. Same identity inputs produce the
  same id across runs and machines; see §8 for the canonical-input rules
  per type.
- Unknown future fields are additive; consumers should ignore unknown keys.
  Breaking changes bump `schemaVersion`.

A JSON Schema for each payload lives in `schemas/` and is the source of truth
for downstream tools and tests.

## 6. Architecture

The codebase is a .NET solution with three projects (two class libraries plus
the CLI executable) and one test project per library.

```text
Dadabe.sln
src/
  Dadabe.Core/                 # class library — pure music theory
    Memo/                      # content-hashing + cache substrate (D17, D18)
      ContentHash.cs           # struct { Namespace, Version, Digest }
      IContentHashable.cs
      Canonical.cs             # deterministic byte serialization primitives
      IMemo.cs                 # IMemo<TIn, TOut> generic cache interface
      InMemoryMemo.cs          # ConcurrentDictionary-backed default impl
      Namespaces.cs            # string constants per cache namespace
    PitchClass.cs              # math-only identity (per D12)
    Note.cs                    # letter + accidental; the user-facing pitch (D12)
    Pitch.cs                   # Pitch (Note + Octave) + MIDI + SPN parse/format
    Interval.cs
    Tuning.cs                  # named tunings + spec parser
    Tunings.json               # shipped catalogue (DADABE, Standard, ...) — §10.1
    Chord/
      ChordSymbol.cs
      ChordSpec.cs
      ChordParser.cs           # symbol -> ChordSymbol (data-driven per §10.2)
      ChordExpander.cs         # ChordSymbol -> ChordSpec (stacked-thirds spelling)
      ChordGrammar.json        # forms + modifiers + parse rules (per D19) — §10.2
  Dadabe.Fretboard/            # class library — board + hand
    Environment.cs             # runtime context aggregate (D22) — §6.1
    Catalogs.cs                # Tunings + ChordGrammar + VoicingCategories
    OutputTarget.cs            # { FilePath?, Pretty } — plain data (D22)
    FretPosition.cs
    Voicing.cs                 # includes comfort score (D15)
    Fingering.cs
    HandModel.cs               # parameters & default profile
    FretLayout.cs              # enumerate FretPositions for a tuning
    Reachability.cs            # positions per string for a PitchClass
    Playability.cs             # cheap geometric prunes during search
    FingeringSolver.cs         # candidate -> Fingering? (per D6)
    VoicingSearch.cs           # full pipeline
    Classifier.cs              # rule-driven; templates per §10.3
    VoicingCategoryCatalog.cs  # loader for VoicingCategories.json
    VoicingCategories.json     # category templates (per D20) — §10.3
    Transition.cs              # Transition + VoiceMove (D16 — type stubs)
  Dadabe.Cli/                  # console application — `dadabe` binary
    Program.cs                 # entry; System.CommandLine wiring
    Commands/
      VoicingsCommand.cs
      TuningCommand.cs
      ChordCommand.cs
    Io/
      JsonEnvelope.cs          # shared envelope + pretty / file / stdout
schemas/
  voicings.schema.json
  tuning.schema.json
  chord.schema.json
tests/
  Dadabe.Core.Tests/
  Dadabe.Fretboard.Tests/
  Dadabe.Cli.Tests/
```

Two layers, kept separate on purpose:

1. **`Dadabe.Core`** — pure music theory. No fretboard, no playability. Easy
   to test in isolation; reusable by every later feature. No dependency on
   `Dadabe.Fretboard`.
2. **`Dadabe.Fretboard`** — everything that depends on a tuning and physical
   reach. Depends on `Dadabe.Core`.

`Dadabe.Cli` is the thinnest possible shell over the two libraries. The same
libraries will back any future TUI, server, or library consumer.

### 6.1 Environment (D22)

A CLI invocation builds a single `Environment` that aggregates the runtime
context library code needs to do its job:

```csharp
public sealed record Environment(
    Catalogs     Catalogs,
    HandModel    HandModel,
    OutputTarget Output);

public sealed record Catalogs(
    TuningCatalog          Tunings,
    ChordGrammar           ChordGrammar,
    VoicingCategoryCatalog VoicingCategories);

public sealed record OutputTarget(string? FilePath, bool Pretty);
// FilePath == null → stdout.
```

`Environment.Build(workingDirectory, cliArgs)` is constructed once per CLI
run and does three things:

1. `Catalogs.Load(workingDirectory)` — each of the three catalog loaders
   reads its embedded defaults and overlays a same-named file from
   `workingDirectory` if present. Overlay entries win on name collision;
   otherwise unions. Catalog-specific invariants (longest-token-first for
   `ChordGrammar`, `priority`-list preservation for `VoicingCategoryCatalog`)
   are reasserted after merge. This is the single resolution for E9 — there
   is no per-catalog overlay logic at call sites.
2. Resolves the `HandModel` from `--hand-profile` plus individual
   stretch / thumb / barre override flags.
3. Wraps `--out` and `--pretty` into the `OutputTarget`.

Commands receive the `Environment` and pull what they need. Per-call
inputs that vary inside a single run — chord symbols, `SearchParams` —
stay as explicit arguments. `SearchParams` is **not** part of
`Environment` because it is the unit of per-call variance for
`VoicingSearch` (see §8.2 / D18); `Environment` is the stable run
context.

`Environment` is **not** content-hashable. It is a CLI plumbing
primitive, not a domain identity. Its constituent parts (`HandModel`,
each catalog record type) are content-hashable individually so memo keys
remain stable.

`Environment` lives in `Dadabe.Fretboard` rather than `Dadabe.Core`
because `Catalogs` references `VoicingCategoryCatalog` and the resolved
`HandModel`, both of which depend on fretboard concepts (string count,
fret reach). Putting `Environment` in `Core` would reverse the
established layering. Future non-CLI consumers (TUI, server) still
import `Dadabe.Fretboard` to get voicings, so reusability is preserved.

`OutputTarget` is plain data. No `IOutputSink` interface in v0.1: the
CLI's `JsonEnvelope` (§6 architecture) is the only writer and inspects
`OutputTarget` directly. An I/O abstraction can be added if a non-CLI
consumer needs one — additively, not now.

## 7. Algorithm Sketch — Voicing Generation

1. **Parse and expand** the chord symbol to a `ChordSpec` (pitch classes,
   each labelled with its function *and* its spelled `Note` per the
   stacked-thirds letter walk — see D12 and §4).
2. **Enumerate reachable positions** per string within `[0, --frets]` whose
   pitch class is in the spec.
3. **Combinatorial search** across strings: for each string choose either a
   reachable position or `muted`. Yields candidate voicings, generated in
   **lexicographic ascending order** over the per-string position tuple
   `(pos[0], pos[1], …, pos[n-1])`, where each `pos[i]` is the integer fret
   (0 = open) on string `i`, and muted is encoded as `int.MaxValue`. This
   keeps the iteration nested outer-to-inner from string 0 to string n−1,
   and within each string visits frets `0, 1, 2, …, maxFret, muted` in that
   order — so fuller voicings at low frets come first, voicings with muted
   strings come later (per D5).
4. **Coarse prune** (`Playability.cs`) — cheap geometric checks that let us
   reject quickly before the fingering solver runs:
   - Fret span `≤ --span`.
   - String count within `[--min-strings, --max-strings]`.
5. **Fingering solve** (`FingeringSolver.cs`) — given the candidate's
   positions and the `HandModel`, return a valid `Fingering` (per the
   eight-rule contract in §4) or `null`. The eight rules — coverage,
   open-string, finger uniqueness, barre consistency, barre count, reach
   stretch, thumb rule, mute-source legality — are what the solver must
   enforce. The search strategy (CSP, backtracking, rule-based, …) is a
   v0.1 implementation choice and is not pinned here; the deterministic
   tie-break preference (when multiple valid fingerings exist) is part of
   that choice and must be documented alongside the solver.

   If no valid fingering exists, drop the candidate.
6. **Required tones** — keep the voicing only if it contains the required
   chord tones (3 and 7 for tertian 7ths; 3 and 5 for triads; explicit
   extensions). Sounded pitch classes must be a subset of the chord spec
   (per D4 / permissive inclusion).
7. **Categorize** survivors (triads, shell, drop-2, drop-3, spread) by
   comparing the sounded pitches' interval structure to known templates.
8. **Score** comfort (per D15) as a single float in `[0, 1]`. v0.1 formula:

       comfort = clamp(
         1
         − 0.40 · (span / maxSpan)
         − 0.10 · mutedStrings
         − 0.15 · barreCount
         − 0.05 · (lowestFret / maxFret),
         0, 1)

   Reported on each voicing. Does **not** affect emission order.
9. **Emit** in the lexicographic-tuple order established in step 3.
   `--limit` truncates the prefix; there is no separate sort step (per D5).
   Each emitted position is spelled using the chord-tone's `Note` from
   step 1 (so the same fret position can appear as `D♯3` for one chord and
   `E♭3` for another).

The search is bounded enough by steps 4–6 that brute force over a 6-string,
15-fret board is acceptable for v0.1. The fingering solver is the most
expensive step; optimisation, if needed, focuses there.

Steps 1 (chord expansion), 5 (fingering solve), 7 (classification), and the
overall search (2–7 as a unit) are wrapped in optional memo lookups keyed on
the inputs' content hashes — see §8. The v0.1 CLI does not enable caching;
the wrapping exists so v0.2 features can plug in without disturbing the
algorithm.

## 8. Memoization & Content-Addressing

Future features compose by chaining derivations: a substitution rule yields a
new `ChordSymbol`, which expands to a `ChordSpec`, which drives a voicing
search, whose outputs feed a transition search, and so on. Each step is
expensive enough to benefit from caching — but caching only works across runs
if every domain object has a stable, content-derived identity.

v0.1 ships the full identity + cache substrate. The CLI does **not** enable a
cache by default; the layer is library surface for v0.2 to plug into without
schema upheaval.

### 8.1 Content hashes (D17)

Every memoizable type implements `IContentHashable`:

```csharp
public interface IContentHashable
{
    ContentHash ContentHash { get; }
}

public readonly record struct ContentHash(string Namespace, int Version, string Digest)
{
    public override string ToString() => $"{Namespace}:{Version}:{Digest}";
}
```

`Digest` is SHA-256 of the canonical byte serialization of the type's
identity-defining fields, truncated to 128 bits, lowercase hex (32 chars).
`ToString()` is exactly what appears as the JSON `id` —
e.g. `voicing:1:b5f3a8d2c1e4f6a78b9c0d1e2f3a4b5c`.

Canonical serialization is type-specific and lives on the type itself.
`Memo/Canonical.cs` provides only primitives (big-endian fixed-width ints,
length-prefixed UTF-8 strings, length-prefixed lists) so each type controls
its own layout. For `Voicing` the layout is:

1. ChordSpec's content-hash bytes (16 bytes — the digest).
2. Tuning's content-hash bytes (16 bytes).
3. HandModel's content-hash bytes (16 bytes).
4. Positions in string-index order; each position is
   `(string:u8, fret:i16-bigendian)` with `fret = -1` denoting muted.

Identity-defining inputs per type:

| Type         | Identity inputs                                                                                         | Namespace      |
| ------------ | ------------------------------------------------------------------------------------------------------- | -------------- |
| `Tuning`     | open-string spelled `Pitch`es (letter + accidental + octave) low to high                                | `tuning`       |
| `ChordSpec`  | root spelled `Note` + ordered (function-id, quality-id, spelled `Note`, pitch class) tones              | `chord-spec`   |
| `HandModel`  | resolved parameters in fixed key order                                                                  | `hand-model`   |
| `Voicing`    | chord-spec hash + tuning hash + hand-model hash + ordered (string, fret) positions                      | `voicing`      |
| `Fingering`  | ordered positions + hand-model hash                                                                     | `fingering`    |
| `Transition` | from-voicing hash + to-voicing hash                                                                     | `transition`   |

The guiding principle is **the story dictates the link**: a `Voicing`'s
id is a content hash of everything that contributes to its JSON
serialization, so equal ids ⟺ byte-identical output. Spelling decisions
that the user can see (`F♯maj7` vs `G♭maj7`, `D♯3` vs `E♭3` for the same
fret) flow into identity rather than being layered on top of a
spelling-blind hash.

Three deliberate identity choices worth flagging:

- **`Voicing` identity includes `ChordSpec`.** A voicing renders chord
  tones onto positions; the same physical shape interpreted in two chord
  contexts produces two distinct JSONs (different `DisplayNote`s,
  different function labels). Different stories ⇒ different ids.
- **`Voicing` identity also includes `HandModel`.** The selected
  fingering — finger assignments, barres, mute sources — is part of the
  voicing's JSON, and `HandModel` is what selects it. Different
  storytellers (hands) tell the story differently, so they get distinct
  ids. `Fingering` itself remains a function of `(positions, HandModel)`
  only — it is the same physical reality across chord contexts, so its
  identity does not include `ChordSpec`.
- **`Tuning` and `ChordSpec` identities include spelling, not just
  pitch.** `F♯maj7` and `G♭maj7` collapse under `pc` but project to
  different spelled `Note`s in output (D12), so they must hash
  differently. Same for tuning: a tuning labelled `["Ebb2", ...]` and
  `["D2", ...]` sound the same but render differently in the JSON
  `tuning.strings` array.

Versioning is per namespace. If the canonical form of `Voicing` ever changes,
`voicing`'s version bumps from `1` to `2`; other namespaces' caches remain
valid. Persistent backends must discard entries whose namespace version
doesn't match.

### 8.2 The memo interface (D18)

```csharp
public interface IMemo<TIn, TOut>
    where TIn  : IContentHashable
    where TOut : IContentHashable
{
    bool TryGet(TIn key, out TOut value);
    void Put(TIn key, TOut value);
}
```

Search functions take an optional memo:

```csharp
Voicing[] Search(
    ChordSpec spec, Tuning tuning, HandModel hand, SearchParams p,
    IMemo<VoicingSearchKey, VoicingSet>? cache = null);
```

When `cache` is `null` (the default at the CLI's call site in v0.1) the
function computes from scratch. When provided, it looks up the key's content
hash first and writes back on miss.

Key types wrap a function's inputs and are themselves content-hashable:

```csharp
public sealed record VoicingSearchKey(
    ChordSpec Spec, Tuning Tuning, HandModel HandModel, SearchParams Params)
    : IContentHashable;

public sealed record FingeringKey(
    Position[] Positions, HandModel HandModel)
    : IContentHashable;
```

`SearchParams` carries the non-cosmetic CLI flags — `--frets`, `--span`,
`--min-strings`, `--max-strings`, `--allow-open`, `--allow-barre`,
`--allow-thumb`, `--categories`. It does **not** include `--limit`: the
search caches its full result list and `--limit` is applied at read time, so
runs at different limits hit the same cache entry.

v0.1 ships one implementation, `InMemoryMemo<TIn, TOut>`, a
`ConcurrentDictionary<string, TOut>` keyed on `key.ContentHash.ToString()`.
Persistent backends (filesystem, SQLite) are reserved for v0.2.

### 8.3 Coverage of deferred features

Memo namespaces are pre-allocated in `Memo/Namespaces.cs` as `string`
constants so v0.2 work is additive. Only the first four are wired in v0.1;
the rest are declared and unused.

| Feature                              | Namespace          | Wired |
| ------------------------------------ | ------------------ | ----- |
| Chord expansion                      | `chord-spec`       | v0.1  |
| Voicing enumeration                  | `voicing-search`   | v0.1  |
| Fingering solve                      | `fingering`        | v0.1  |
| Classification                       | `classification`   | v0.1  |
| Transition cost                      | `transition`       | v0.2  |
| Progression voice-leading            | `progression`      | v0.2  |
| Harmonic transformations             | `transform`        | v0.2  |
| Inner-line generation                | `inner-line`       | v0.2  |
| Chord-melody                         | `melody-voicings`  | v0.2  |

Each v0.2 namespace will introduce its own `*Key` record (e.g.
`TransitionKey { From, To }`, `ProgressionKey { Chords[], HandModel }`) — the
v0.1 substrate doesn't constrain the shape, only that they implement
`IContentHashable`.

### 8.4 Cache is never the source of truth

Memoization is an optimization. Correctness must hold without it.

- The v0.1 CLI does not enable a cache — the memo argument defaults to
  `null` at every CLI call site.
- Every golden test runs twice — once with `null` memo, once with
  `InMemoryMemo` — and the JSON output must be byte-identical. This is the
  load-bearing invariant: a memo can change *time*, never *result*.

That invariant lets v0.2 features layer caching aggressively without risk
that they're masking a correctness bug.

## 9. Tech Stack

- **Language / runtime:** C# on .NET 9. Strong types and records pay off for
  a structural domain like this. (.NET 10 LTS lands late 2026 — easy upgrade
  when it ships.)
- **CLI parsing:** `System.CommandLine` (Microsoft, the standard pick for
  modern .NET CLIs).
- **JSON:** `System.Text.Json` with source generators for the response
  envelope and payload types — fast, AOT-friendly, no extra dependency.
- **Schema authoring & validation:** hand-authored JSON Schema files in
  `schemas/`, validated at test time with `JsonSchema.Net` (Greg Dennis).
  Source-of-truth is the schema file; the C# DTOs must conform.
- **Testing:** `xUnit` with `FluentAssertions`. `FsCheck.Xunit` for property
  tests of music-theory invariants (e.g. every voicing of `Cmaj7` contains
  C, E, B; transposition is consistent).
- **Build & format:** `dotnet build` / `dotnet test`. `.editorconfig` for
  style; `dotnet format` in CI. `Directory.Build.props` for shared settings.
- **Packaging:** `dotnet publish` produces the `dadabe` binary;
  framework-dependent by default, with self-contained single-file publish as
  an option for releases.

The repo's existing `package.json`, `cspell.json`, and `.markdownlint.json`
remain — they target the markdown docs only and are independent of the .NET
build.

Alternative considered: Python (rich music ecosystem via `music21`, `mingus`).
Rejected because the project is committed to .NET; the domain model is small
enough that a third-party theory library would be more constraint than help.

## 10. Configuration Catalogs

Three JSON files ship as embedded resources and define what the tool accepts
and emits. All three: carry their own `schemaVersion` (independent of the
envelope `schemaVersion` of §5), are loaded from embedded defaults at
startup, and are overlaid by a same-named file in the working directory if
present (per D22 — overlay file wins on name collision, otherwise unions).
The shipped defaults live in the repo at the paths shown.

Catalog resolution is performed by `Catalogs.Load(workingDirectory)` and
surfaced via the `Environment` aggregate (§6.1 / D22). Each catalog has its
own loader (`TuningCatalog`, `ChordGrammar`, `VoicingCategoryCatalog`) that
performs the embedded + overlay merge and re-asserts any catalog-specific
post-merge invariant. Call sites consume the resolved `Catalogs` record —
they never see filesystem paths.

### 10.1 `Tunings.json` (per D3)

Located at `src/Dadabe.Core/Tunings.json`.

```json
{
  "schemaVersion": "1",
  "tunings": [
    { "name": "DADABE",   "strings": ["D2", "A2", "D3", "A3",  "B3", "E4"] },
    { "name": "STANDARD", "strings": ["E2", "A2", "D3", "G3",  "B3", "E4"] },
    { "name": "DROP_D",   "strings": ["D2", "A2", "D3", "G3",  "B3", "E4"] }
  ]
}
```

Six tunings ship (DADABE, STANDARD, DROP_D, DADGAD, OPEN_G, OPEN_D). Names
are uppercase; `Tuning.ParseSpec` accepts ad-hoc inline specs alongside
named ones.

### 10.2 `ChordGrammar.json` (per D19)

Located at `src/Dadabe.Core/Chord/ChordGrammar.json`. Defines the chord
grammar as data, replacing hand-coded parser logic. The full file is the
source of truth for chord parsing in v0.1; this section documents the
schema.

**Top-level shape:**

```json
{
  "schemaVersion": "1",
  "forms":      [ /* atomic chord patterns matched right after the root */ ],
  "modifiers":  [ /* trailing extensions and alterations */ ],
  "parseRules": { /* root regex + matching policy */ }
}
```

**Form** = an atomic chord pattern (the longest piece of the symbol matched
in one shot after the root). Each form declares:

- `tokens` — accepted spellings (e.g. `["maj7", "M7"]`). The first token is
  the canonical form for output; subsequent tokens are aliases. The empty
  token `""` matches the bare-root case (`C` → C major triad).
- `displayName` — the form name that appears in JSON output's
  `chord.quality` field.
- `tones` — list of `{ function, quality, semitones }`, the full
  chord-tone spec.
  - `function` — scale-degree label used in output (`1`, `3`, `b3`,
    `5`, `b5`, `#5`, `b7`, `7`, `9`, `b9`, `#9`, `11`, `#11`, `13`,
    `b13`). This is what appears in JSON `function` fields.
  - `quality` — interval label (`P1`, `m3`, `M3`, `P4`, `A4`, `P5`,
    `m7`, `M7`, `M9`, `m9`, `A9`, `P11`, `A11`, `M13`, `m13`). This is
    the canonical `QualityLabel` of the `Interval` record (§4); storing
    it explicitly means the grammar — not the parser — owns the
    spelling story.
  - `semitones` — integer distance from the root, 0–24.

  For the standard chord vocabulary the three are mutually derivable,
  but storing them side-by-side keeps the story canonical and survives
  exotic future tones (e.g. a `bb7` distinct from `6` enharmonically)
  without grammar-engine surgery.
- `required` — sublist of `tones`' functions that any emitted voicing MUST
  contain (the [D4] permissive-inclusion rule operates on the full `tones`
  set; required is the floor).

Example (from the shipped catalog):

```json
{ "tokens": ["maj7", "M7"], "displayName": "maj7",
  "tones": [
    { "function": "1", "quality": "P1", "semitones": 0  },
    { "function": "3", "quality": "M3", "semitones": 4  },
    { "function": "5", "quality": "P5", "semitones": 7  },
    { "function": "7", "quality": "M7", "semitones": 11 }
  ],
  "required": ["3", "7"] }
```

**Modifier** = a trailing token that modifies the form's chord-tone set.
Two kinds: `addition` (adds new tones without displacing anything — e.g.
`add9`) and `alteration` (displaces an existing tone and adds an altered
one — e.g. `b5` removes the natural 5 and adds a `b5`). Each modifier
declares:

- `tokens` — accepted spellings (e.g. `["b5"]`).
- `kind` — `"addition"` or `"alteration"`.
- `displaces` — (alteration only) the function name to remove from the spec
  before adding the new tone.
- `addTones` — list of `{ function, quality, semitones }` to add (same
  schema as a form's `tones`).
- `required` — functions added to the required set when this modifier
  applies.

**Parse rules:**

- `rootRegex` — matched at the start of the symbol; captures letter +
  optional accidental (`bb`, `##`, `b`, `#`). The greedy accidental match
  resolves the `Cb9` ambiguity in favour of root `Cb` + form `9`.
- `longestTokenFirst` — within forms (and within modifiers) the parser
  tries longer tokens before shorter ones, so `maj7` beats `m` on
  `Cmaj7` and `dim7` beats `dim` on `Cdim7`.
- `modifierOrder: "any"` — modifiers may appear in any order after the
  form; their semantic effect is order-independent.
- `rejectUnparsedTail: true` — if any input remains after root + form +
  modifiers, the parse fails (exit code 1 per §3).
- `rejectSlash: true` — `/` triggers an immediate error in v0.1 (slash
  chords deferred per D2).

**Required-tones fall out of the config**, with no separate table: it is
the union of the matched form's `required` and every applied modifier's
`required`. Same for spelled-note ordering: tones are spelled by
stacked-thirds letter walk (per D12) against the form's `tones` list.

### 10.3 `VoicingCategories.json` (per D20)

Located at `src/Dadabe.Fretboard/VoicingCategories.json`. Defines the Core 5
categories (triads, shell, drop-2, drop-3, spread) as match templates;
extensible by users adding new categories (e.g. quartal, cluster) without
recompile.

**Top-level shape:**

```json
{
  "schemaVersion": "1",
  "priority":   ["triad", "shell", "drop-2", "drop-3", "spread"],
  "functionClasses": { /* named function-equivalence classes */ },
  "categories": [ /* category definitions, evaluated in priority order */ ]
}
```

Categories are evaluated in `priority` order; the first whose `matches`
list has any matching entry wins. The shipped `spread` category uses
`{ "matchAny": true }` as a fallthrough, so every emitted voicing carries
a non-null `category`.

**Match rule types** (each match entry combines one or more; all rules
inside an entry must hold):

| Rule                         | Semantics                                                                                  |
| ---------------------------- | ------------------------------------------------------------------------------------------ |
| `noteCount`                  | Sounded (non-muted) string count must equal this exactly.                                  |
| `noteCountRange: [min, max]` | Sounded string count must fall in this inclusive range.                                    |
| `allFunctionsIn`             | Every sounded note's function must be a member of this set.                                |
| `requireFunctions`           | For each entry (a function class), at least one sounded note must satisfy it.              |
| `forbidFunctions`            | No sounded note may satisfy any of these classes.                                          |
| `functionSequenceLowToHigh`  | Sounded notes in ascending pitch order must satisfy each class at the corresponding index. |
| `adjacentIntervalMinSemitones` | Every adjacent interval (low to high) between sounded notes must be ≥ this value.        |
| `matchAny: true`             | Fallthrough — always matches.                                                              |

**Function classes:** a pipe-delimited string like `"3|b3"` matches any of
the listed functions. Users can also name classes in `functionClasses` (the
shipped catalog defines `any-third`, `any-fifth`, etc.) and reference them
by name in rules.

**Drop-2 / drop-3 templates** are spelled out as one
`functionSequenceLowToHigh` per inversion of the close-voiced source. For
drop-2 of a 7th chord the four inversions give bass functions 5, 7, 1, 3
respectively; the full sequence at each index is the close-voiced rotation
with the dropped voice moved to the bottom. The shipped catalog encodes all
four; new categories follow the same pattern.

**Spread** is the fallthrough. Anything not matching triad/shell/drop-2/
drop-3 lands here. Refining `spread` into more-specific structural
categories (e.g. quartal, open-triad, drop-2-and-4) is a v0.2 task and
fits additively into this config.

## 11. Testing Strategy

- One xUnit test project per library / app, under `tests/`.
- Golden JSON files per chord/tuning pair in `tests/Dadabe.Cli.Tests/Golden/`.
  Run the CLI via the test host, diff the output. Catches accidental schema
  drift.
- Unit tests for `Dadabe.Core` (pitch math, chord parsing, interval
  arithmetic) — these are pure functions and cheap to cover exhaustively.
- Property tests (`FsCheck.Xunit`) for invariants: every emitted voicing's
  sounded pitch classes must be a subset of the chord spec's pitch classes
  (per D4); every emitted voicing must have a valid `Fingering` (per D6).
- Fingering-solver fixture set: hand-picked known-playable shapes that must
  pass, and known-unplayable shapes (impossible stretches, ungrabbable mutes)
  that must be rejected.

## 12. Milestones

1. **M0 — Scaffolding.** Solution, projects, test projects, NuGet deps,
   `.editorconfig`, `LICENSE`.
2. **M1 — Core theory.** PitchClass / Pitch / Interval / Tuning parser. Chord
   symbol parser. No CLI yet.
3. **M2 — Fretboard.** Reachability, hand model, fingering solver, voicing
   enumeration. Library-only.
4. **M3 — CLI + JSON.** Wire `dadabe voicings`, schemas, golden tests.
5. **M4 — Polish.** `tuning` and `chord` subcommands, CI, release tag.

Detailed task breakdowns live in [todo.md](todo.md). After M4, v0.1 ships and
the harmonic-transformation work from the README becomes the next design doc.
