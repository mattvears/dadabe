# Whitepaper: Building Rich Test Data for Modes

## Why mode test data matters

In Dadabe, a **mode** is a reference-catalog record describing a named scale
rotation — e.g. Dorian as the second rotation of the major scale, or Lydian
Dominant as the fourth rotation of melodic minor. Modes live alongside
cadences and scales in the "Reference" editor tab
(`src/Dadabe.Editor/Slices/ReferenceIndex.cshtml:12-13`) and are persisted as
individual JSON files under `data/reference/modes/`
(`src/Dadabe.Editor/Services/DataStore.cs:39`).

Today the catalog is **empty** — `src/Dadabe.Editor/data/reference/modes/`
contains no seed files, and the only mode the test suite exercises is a single
synthetic Dorian round-trip in
`tests/Dadabe.Cli.Tests/ScaleCadenceModeTests.cs:19-21`. That means the schema
and the editing UI have essentially never been stressed with anything beyond
one diatonic example. Rich, varied test data is the only way to discover
whether the schema's optional fields, the form-parsing code, and the
slug-based storage layer actually hold up across the full universe of modes a
guitarist or theory reference would expect: seven diatonic rotations, modes of
melodic/harmonic minor and harmonic major, symmetric/synthetic scales, modes
with no sensible "parent", and modes whose interval sets don't fit neatly into
a 7-note diatonic mould.

## Schema shape summary

`schemas/mode.schema.json` is intentionally loose (`additionalProperties:
false`, draft-07):

| Field | Type | Required? | Notes |
| --- | --- | --- | --- |
| `name` | string | **yes** (`schemas/mode.schema.json:14`) | e.g. "Dorian", "Lydian Dominant" |
| `intervals` | `integer[]` | **yes** (`schemas/mode.schema.json:10,14`) | semitone offsets from the root; no length, range, ordering, or uniqueness constraint |
| `parentScale` | string | optional (`schemas/mode.schema.json:8`) | free-text name of the scale this mode is derived from — described as "if applicable", i.e. legitimately absent for modes with no parent |
| `degreeIndex` | integer | optional (`schemas/mode.schema.json:9`) | "Zero-based degree index into parent scale to form this mode" — only meaningful when `parentScale` is present |
| `noteNames` | `string[]` | optional (`schemas/mode.schema.json:11`) | concrete pitch spellings, independent of `intervals` |
| `description` | string | optional (`schemas/mode.schema.json:12`) | free text |

Compare with `schemas/scale.schema.json`: a **scale** requires `name`,
`notes`, and `intervals`, and has the *inverse* relationship field `modeOf`
(`schemas/scale.schema.json:10`) — a scale optionally points at its parent,
whereas a mode optionally points at its parent **and** records the rotation
index (`degreeIndex`) that produced it. Note the asymmetry: nothing in either
schema enforces that a mode's `parentScale` value actually matches the `name`
of an existing scale record, nor that `degreeIndex` is within range of the
parent's interval count, nor that `intervals.length == noteNames.length`. All
of these are exactly the kind of cross-record / cross-field consistency that
rich test data should probe (and that the application currently does nothing
to validate — see below).

Two more subtleties baked into the schema worth exploiting in test data:

- `intervals` items are plain `integer` with **no minimum/maximum/uniqueness**
  — nothing stops a mode record from declaring intervals like `[0, 1, 1, 2]`
  (a duplicate or non-monotonic set), intervals beyond an octave (`>11`), or
  negative numbers. Good test data should include at least one record that
  pushes on this looseness.
- `parentScale` is a bare string, not a schema reference/enum — it is free
  text that *conventionally* matches a `Scale.name`, but the schema has no way
  to enforce that. Test data should include both "well-formed" parent links
  and at least one dangling/mismatched reference to see how the editor and any
  future cross-catalog tooling behave.

## How the code consumes/produces this data

### DTO vs. stored model — two shapes for the same concept

There are **two different C# representations** of a mode, and rich test data
needs to satisfy both:

1. `Dadabe.Cli.Io.ModeDto` (`src/Dadabe.Cli/Io/ModeDto.cs:4`) — a CLI/schema
   DTO: `record ModeDto(string Name, string? ParentScale, int DegreeIndex,
   int[] Intervals, string[]? NoteNames = null, string? Description = null)`.
   Note `DegreeIndex` here is a **non-nullable `int`**, diverging from the
   schema (where it's optional) and from the editor model (where it's
   `int?`). A DTO round-trip test
   (`tests/Dadabe.Cli.Tests/ScaleCadenceModeTests.cs:19-21,29,36`) only checks
   that `Name` survives serialization — it never asserts on `DegreeIndex`,
   `Intervals`, `NoteNames`, or `Description`, so divergences in those fields
   would go unnoticed today. This is itself a gap that broader test data
   (asserting on every field, including the `int` vs `int?` mismatch) should
   close.
2. `Dadabe.Editor.Services.ModeModel` (`src/Dadabe.Editor/Services/ReferenceService.cs:13-20`)
   — the persisted/edited shape, which adds a **required `Slug`** (via
   `ISlugged`, `src/Dadabe.Editor/Services/ReferenceService.cs:85-88`) and
   makes `DegreeIndex` nullable (`int?`). This is the shape written to/read
   from `data/reference/modes/<slug>.json`
   (`src/Dadabe.Editor/Services/DataStore.cs:62-67`).

Rich test data generation should produce fixtures for **both** shapes, and
specifically include cases that exercise the `DegreeIndex` nullability
mismatch (DTO requires a value; editor model and JSON Schema do not).

### Slug derivation and identity

`ReferenceRoutes.ParseModeForm` (`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:178-195`)
derives the slug from `name` via `DataStore.ToSlug`
(`src/Dadabe.Editor/Services/DataStore.cs:80-82`), which lower-cases, trims,
and collapses runs of non-`[a-z0-9]` characters to single hyphens (e.g. "Lydian
Dominant" → `lydian-dominant`, "Altered (Super Locrian)" → `altered-super-locrian`).
Crucially, **the slug is fixed at creation** — the form input for `name` is
marked `readonly` when editing (`src/Dadabe.Editor/Slices/ReferenceModeForm.cshtml:19-20`),
so the route handler reuses `existingSlug` on PUT
(`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:76-78`). Test data should include:

- Names that collide after slugification (e.g. "Mixolydian ♭6" and
  "Mixolydian b6" both → `mixolydian-6`) to probe overwrite/collision behavior
  in `DataStore.Save` (`src/Dadabe.Editor/Services/DataStore.cs:62-67`, which
  silently overwrites same-named files).
- Names with diacritics, accidumental symbols (♭ ♯ °), parentheses, and
  Unicode (e.g. "Locrian ♮2", " Down-Under Mixolydian") to exercise the regex
  `[^a-z0-9]+` collapsing.
- Empty/whitespace-only names, which `ParseModeForm` rejects with "Name is
  required." (`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:183`).

### Form parsing — comma-separated lists, nullable numerics

`ParseModeForm` reconstructs `Intervals` and `NoteNames` from comma-separated
strings via `ParseIntList`/`ParseStringList`
(`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:214-223`). Note the asymmetry:

- `ParseIntList` **silently drops** any token that doesn't parse as an `int`
  (`.Where(x => x.HasValue)`, line 217) — e.g. `"0, 2, x, 5"` silently becomes
  `[0, 2, 5]`. Rich test input strings should include malformed numeric tokens,
  extra whitespace, trailing commas, and negative numbers to verify this
  silent-drop behavior is acceptable (or to surface it as a bug).
- `ParseStringList` does **not** filter — every comma-separated token survives
  as a note name, including empty strings produced by consecutive commas being
  removed by `RemoveEmptyEntries`. Test strings like `"C, , E"` vs `"C,E"`
  exercise this.
- `degreeIndex` uses `int.TryParse` and falls back to `null`
  (`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:192`) — so a non-numeric or
  empty `degreeIndex` field silently becomes "no degree index" rather than an
  error. Test data should include blank, zero, negative, and out-of-range
  `degreeIndex` values (e.g. `degreeIndex: 7` against a 7-note parent scale,
  which is one past the last valid rotation).

### Persistence and listing

`ReferenceService` (`src/Dadabe.Editor/Services/ReferenceService.cs:53-64`)
is a thin pass-through to `DataStore.LoadAll`/`Load`/`Save`/`Delete`. Records
are sorted by `Slug` when listed (`src/Dadabe.Editor/Services/DataStore.cs:50`),
and serialized with `WriteIndented = true` plus
`DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`
(`src/Dadabe.Editor/Services/ReferenceService.cs:32-36`) — meaning optional
`null` fields (`ParentScale`, `DegreeIndex`, `NoteNames`, `Description`) are
**omitted** from the on-disk JSON entirely, not written as `null`. Rich test
fixtures should include both "fully populated" and "minimal" mode records
(only `name`+`intervals`+`slug`) to verify round-tripping through this
omit-on-null serialization, and to verify `LoadAll` tolerates a mix of sparse
and dense files in the same directory.

### Display

`ReferenceModeList.cshtml:23-24` renders `ParentScale ?? "—"` and
`string.Join(", ", m.Intervals)`. An empty `Intervals` array (technically legal
per the schema — it's just `array` with no `minItems`) would render as an
empty string in the table; a mode record with a very large interval list (e.g.
12-tone chromatic, `[0,1,2,3,4,5,6,7,8,9,10,11]`) would stress the table's
layout. Both are worth covering.

## Recipe for a rich mode dataset

To genuinely stress the schema, the DTOs, the parsing/slugging code, and the
editor UI, vary along these dimensions and combine them:

1. **The seven classic diatonic modes of the major scale** — Ionian, Dorian,
   Phrygian, Lydian, Mixolydian, Aeolian, Locrian — each with `parentScale:
   "Major"` (or "C Major" — see naming-convention point below) and the correct
   `degreeIndex` 0–6, correct 7-element `intervals`, and matching `noteNames`.
   This is the "happy path" baseline; the existing test
   (`tests/Dadabe.Cli.Tests/ScaleCadenceModeTests.cs:19-21`) only covers
   Dorian, so add the other six.
2. **Modes of melodic minor** (the "jazz minor" rotations) — e.g. Dorian ♭2,
   Lydian Augmented, Lydian Dominant, Mixolydian ♭6, Locrian ♮2 (a.k.a. Half-
   diminished), Altered/Super Locrian. These have `parentScale: "Melodic
   Minor"` and exercise names containing accidental symbols and slashes.
3. **Modes of harmonic minor and harmonic major** — e.g. "Phrygian Dominant"
   (5th mode of harmonic minor), "Ionian ♯5", "Locrian ♮6" (modes of harmonic
   major). These parent scales have a **non-symmetric interval gap** (an
   augmented second), so their derived modes have unusual two-and-a-half-step
   jumps in `intervals` (e.g. `[0, 1, 4, 5, 7, 8, 11]`) — good for stressing
   any future "is this a sane scale?" validation.
4. **Modes with no meaningful parent** — symmetric/synthetic scales such as
   Whole Tone (`intervals: [0,2,4,6,8,10]`, 6 notes) or Octatonic/Diminished
   (`intervals: [0,2,3,5,6,8,9,11]`, 8 notes) where `parentScale` and
   `degreeIndex` are legitimately **absent** (the schema marks them optional
   for exactly this reason — `schemas/mode.schema.json:8-9`). Also include
   pentatonic-derived modes (5-note `intervals`) to vary array length away
   from the 7-note assumption baked into the form's placeholder text
   (`src/Dadabe.Editor/Slices/ReferenceModeForm.cshtml:32,36`).
5. **Root/spelling variation in `noteNames`** — the same interval pattern
   spelled with sharps vs. flats (e.g. Dorian on D: `["D","E","F","G","A","B","C"]`
   vs. an enharmonic respelling on C♯: `["C#","D#","E","F#","G#","A#","B"]` vs.
   `["Db","Eb","Fb",...]`), plus at least one record that **omits** `noteNames`
   entirely (legal per schema) to confirm the `?? "—"`-style optionals and
   `JsonIgnoreCondition.WhenWritingNull` path are exercised both ways.
6. **`degreeIndex` edge values** — `0` (first-degree / same as parent), the
   maximum legal value (`parent note count - 1`), an out-of-range value (e.g.
   `7` for a 7-note parent), and `null`/absent. Combine with a `parentScale`
   string that does **not** match any existing `Scale.name` in the catalog, to
   probe the (currently absent) cross-record consistency checking.
7. **Slug-collision and slug-character stress** — names that normalize to the
   same slug via `DataStore.ToSlug` (`src/Dadabe.Editor/Services/DataStore.cs:80-82`),
   names with mixed case, leading/trailing whitespace, accidental symbols (♭
   ♯ ° Δ), and very long names.
8. **Malformed/loose `intervals`** — duplicate values, descending or
   non-monotonic order, values `>11` or negative, and an empty array — all
   technically schema-valid (no `minItems`/`uniqueItems`/range constraints at
   `schemas/mode.schema.json:10`) but musically nonsensical; useful for
   confirming the system doesn't silently corrupt or crash on them.
9. **Minimal vs. maximal records** — at least one record with only the two
   required fields (`name`, `intervals`) and at least one with every optional
   field populated, to exercise `additionalProperties: false`
   (`schemas/mode.schema.json:15` — adding any unexpected key must fail
   validation) and the omit-on-null persistence path.
10. **Naming-convention variants for `parentScale`** — since it's free text,
    include both bare scale-family names ("Major", "Melodic Minor") and
    rooted names ("C Major", "A Melodic Minor") to see whether downstream
    consumers (or future cross-linking) assume one convention over the other.

## Example test cases

### 1. Classic diatonic mode — happy path, full population

```json
{
  "name": "Dorian",
  "parentScale": "Major",
  "degreeIndex": 1,
  "intervals": [0, 2, 3, 5, 7, 9, 10],
  "noteNames": ["D", "E", "F", "G", "A", "B", "C"],
  "description": "Second mode of the major scale; minor scale with a raised 6th (natural 6)."
}
```

### 2. Melodic-minor mode — accidentals in the name, exotic interval gap

```json
{
  "name": "Lydian Dominant (Mode IV of Melodic Minor)",
  "parentScale": "Melodic Minor",
  "degreeIndex": 3,
  "intervals": [0, 2, 4, 6, 7, 9, 10],
  "noteNames": ["G", "A", "B", "C#", "D", "E", "F"],
  "description": "Lydian with a flat 7th; the 'acoustic scale'; common over dominant 7#11 chords."
}
```

### 3. Harmonic-minor-derived mode — augmented-second interval jump, minimal optional fields

```json
{
  "name": "Phrygian Dominant",
  "parentScale": "Harmonic Minor",
  "degreeIndex": 4,
  "intervals": [0, 1, 4, 5, 7, 8, 10]
}
```

*(Omits `noteNames` and `description` — legal per schema; tests the
omit-on-null round trip and the `?? "—"` display fallback.)*

### 4. Synthetic/symmetric mode — no parent, non-7-note structure, stresses array-length assumptions

```json
{
  "name": "Whole Tone",
  "intervals": [0, 2, 4, 6, 8, 10],
  "noteNames": ["C", "D", "E", "F#", "G#", "A#"],
  "description": "Hexatonic symmetric scale; every adjacent interval is a whole step. No conventional 'parent scale' or degree relationship — both fields intentionally omitted."
}
```

*(Omits `parentScale` and `degreeIndex` entirely — exercises the "if
applicable" escape hatch in `schemas/mode.schema.json:8-9`, a 6-element
`intervals`/`noteNames` pair instead of the assumed 7, and the schema's lack
of any enum constraining `name` to a known mode-name vocabulary.)*

## Summary of what to assert once the data exists

For each generated fixture, validate against `schemas/mode.schema.json`
*and* round-trip through both `ModeDto` (`src/Dadabe.Cli/Io/ModeDto.cs:4`) and
`ModeModel` (`src/Dadabe.Editor/Services/ReferenceService.cs:13-20`),
confirming: required fields survive, optional fields round-trip as both
present and absent, `DegreeIndex` behaves correctly across its three different
nullability representations (schema-optional, DTO-required-`int`,
model-nullable-`int?`), slugs are stable and collision-safe, and the
form-parsing helpers (`ParseIntList`/`ParseStringList`,
`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:214-223`) handle malformed
comma-separated input the way the rest of the suite expects.
