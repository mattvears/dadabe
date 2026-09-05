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

## v0.5.3 — current release

v0.1 shipped the core voicing engine. v0.2 added next-chord prediction. v0.3 was a
scaffolding polish release. v0.4 made the prediction pipeline operational. v0.5 added
progression voice leading, context-weighted prediction, the Web Awesome UI migration,
and full exposure of hand-model options in the Editor.

v0.5.3 brings:
- **Comfort model fixes**: `HandAnglePenalty` (Rule 1) now correctly penalizes barred inner fingers once per finger, not once per string.
- **Voicing options everywhere**: A shared "Advanced options" panel (frets, span, string constraints, allow-flags, require-root, min-comfort, categories) now appears in Predictions, Songs, and Voicings pages. The Predictions "click a chord to see voicings" flow respects these settings.
- **Voice Lead chart display**: Each voice-lead solution step now renders as a chord diagram, mirroring the song chart layout.
- **Voice Lead song integration**: The Voice Lead page can source chords from an active song section (with the existing header selects) and push a chosen solution back as that section's pinned voicings, enabling seamless hand-curated voice leading within a song.
- **Tuning inheritance in Predictions**: When a song is active, its tuning is used for voicing lookups instead of the local dropdown.

See [CHANGELOG.md](CHANGELOG.md) for details.

### Build and run

**CLI:**

```sh
dotnet build
dotnet run --project src/Dadabe.Cli -- voicings Cmaj7 --tuning DADABE --pretty
```

**Editor (web UI):**

```sh
dotnet run --project src/Dadabe.Editor -- --urls http://localhost:5299
```

Then navigate to `http://localhost:5299`.

For the full picture — running the test suite, schema validation, and how to
point the Editor at a scratch data directory instead of the tracked sample data —
see [docs/development.md](docs/development.md).

Four subcommands ship:

```sh
dadabe voicings <chord> [--tuning ...] [--frets N] [--span N]
                        [--min-strings N] [--max-strings N]
                        [--allow-open] [--allow-barre] [--allow-thumb]
                        [--categories <csv>] [--limit N]
                        [--top-n N] [--entropy F] [--min-comfort F]
                        [--pretty] [--out <path>] [--validate-schema]
dadabe predict  --input <request.json>
                        [--entropy F]
                        [--pretty] [--out <path>] [--validate-schema]
dadabe tuning   <name|spec> [--pretty] [--out <path>] [--validate-schema]
dadabe chord    <symbol>    [--pretty] [--out <path>] [--validate-schema]
```

**Slash chords** (`C/E`, `Cmaj7/G`) are supported by the parser. The bass note
is included in `chord.bassNote` in the JSON output. The `voicings` subcommand
currently treats slash chords identically to their root chord (bass-note
enforcement in voicing search is deferred to v0.5).

**Prediction request** (`dadabe predict`) reads a JSON file matching
`schemas/prediction.schema.json`. The `chord` field is required; `context`,
`filters`, `maxResults`, and `entropy` are optional.
See `docs/v0.4/examples/prediction-request.json` for a full example.

Exit codes: `0` success, `1` bad input, `2` unexpected error, `3` schema
violation (`--validate-schema` detected invalid output). JSON `id` fields are
content hashes — same inputs produce the same id across runs and machines (D17).

### Schemas

Every output envelope validates against `schemas/envelope.schema.json`.
Subcommand payloads are pinned by:

| Command    | Schema |
|------------|--------|
| `voicings` | `schemas/voicings.schema.json` |
| `tuning`   | `schemas/tuning.schema.json`   |
| `chord`    | `schemas/chord.schema.json`    |
| `predict`  | `schemas/prediction-result.schema.json` |

Supplementary schemas (used as input or for data files):

| Schema | Purpose |
|--------|---------|
| `schemas/prediction.schema.json`        | `predict --input` request shape |
| `schemas/progression.schema.json`       | Chord progression |
| `schemas/scale.schema.json`             | Named scale |
| `schemas/mode.schema.json`              | Musical mode |
| `schemas/cadence.schema.json`           | Cadence type |
| `schemas/transformation.schema.json`    | Harmonic transformation (placeholder) |
| `schemas/voicing-category.schema.json`  | VoicingCategories overlay format |

### Library status

`Dadabe.Core` and `Dadabe.Fretboard` are part of the solution but **not
published to NuGet** (D7). No public API guarantees yet; revisit post-v0.4.
