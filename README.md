# READ ME

## DADABE

*Dadabe* is a programmable system for generating, analyzing, transforming, and exploring guitar harmony **in alternate tunings** inspired by Ted Greene's harmonic philosophy, and the DADABE guitar tuning used in many Pavement songs.

The project models the guitar fretboard as a voice-leading and interval system rather than a collection of static chord shapes. It generates playable voicings, substitutions, inner-line movements, and harmonic textures using algorithmic rules inspired by advanced jazz guitar harmony.

Instead of memorizing chord diagrams, the engine explores relationships:

- Intervals
- Tension/resolution
- Inversion movement
- Voice independence
- Harmonic color
- Fretboard geometry

The result is somewhere between:

- A music theory engine
- A chord encyclopedia
- A compositional assistant

## Core Philosophy

When most guitar software asks:

>> “What shape is a Cmaj7?”

This project asks:

>> “What are all the musically meaningful ways to express Cmaj7 on guitar under specific musical constraints?”

## Features

- Given a tuning:
  - Fretboard geometry solver - Map harmonic structures onto playable guitar shapes.
    - Considers:
      - Hand span
      - String sets
      - tunings
      - Muted strings
      - Open strings
      - Finger independence
      - Barres
  - Given a chord, performs harmonic transformations:
    - Transforms chords through:
      - Model interchange
      - Tritone substitution
      - Chromatic planing
      - Diminished substitution
      - Secondary dominants
      - Negative harmony
      - Parallel harmony
  - Given a chord, generate:
    - Triads
    - Shell voicings
    - Drop-2, drop-3 voicings
    - Quartal harmony
    - Cluster voicings
    - Spread voicings
    - Polychords
    - Altered dominants
    - Upper-structure triads
    - Chord-melody harmonization
  - Generate chord charts

- Given a progression: generate chord charts for the progression in alternate tunings
  - Finds minimal motion transitions between chords
  - Inner-line generator - generate moving voices inside static harmony.

## v0.3 — current release

v0.1 shipped the core voicing engine. v0.2 added next-chord prediction
(rules-based, softmax-scored). v0.3 is a polish release: expanded tests,
schema scaffolding for future features, and CLI/output refinements.
See [docs/v0.3/plan.md](docs/v0.3/plan.md) for the full scope and
[CHANGELOG.md](CHANGELOG.md) for release notes.

### Build and run

```sh
dotnet build
dotnet run --project src/Dadabe.Cli -- voicings Cmaj7 --tuning DADABE --pretty
```

Three subcommands ship:

```sh
dadabe voicings <chord> [--tuning ...] [--frets N] [--span N]
                        [--min-strings N] [--max-strings N]
                        [--allow-open] [--allow-barre] [--allow-thumb]
                        [--categories <csv>] [--limit N]
                        [--top-n N] [--entropy F]
                        [--pretty] [--out <path>]
dadabe tuning   <name|spec> [--pretty] [--out <path>]
dadabe chord    <symbol>    [--pretty] [--out <path>]
```

Exit codes: `0` success, `1` bad input (unparseable chord/tuning), `2`
unexpected error. JSON `id` fields are content hashes — the same chord on
the same tuning produces the same id across runs and machines (D17). The
JSON shape is pinned by `schemas/*.schema.json`.

### Schemas

Every output envelope validates against `schemas/envelope.schema.json`.
Subcommand payloads are pinned by:

| Command    | Schema |
|------------|--------|
| `voicings` | `schemas/voicings.schema.json` |
| `tuning`   | `schemas/tuning.schema.json`   |
| `chord`    | `schemas/chord.schema.json`    |

Scaffolding schemas for future features (not yet emitted by the CLI):

| Schema | Purpose |
|--------|---------|
| `schemas/progression.schema.json`       | Chord progression |
| `schemas/prediction.schema.json`        | Next-chord prediction request |
| `schemas/prediction-result.schema.json` | Prediction response |
| `schemas/scale.schema.json`             | Named scale |
| `schemas/mode.schema.json`              | Musical mode |
| `schemas/cadence.schema.json`           | Cadence type |
| `schemas/transformation.schema.json`    | Harmonic transformation (placeholder) |
| `schemas/voicing-category.schema.json`  | VoicingCategories overlay format |

### Library status

`Dadabe.Core` and `Dadabe.Fretboard` are part of the solution but **not
published to NuGet** (D7). No public API guarantees yet; revisit post-v0.3.
