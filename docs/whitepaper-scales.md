# Whitepaper: Generating Rich Test Data for Scales

## 1. What "scale" test data represents

In Dadabe, a **scale** record (`schemas/scale.schema.json`) is a named, ordered
pitch collection: a canonical note spelling plus its semitone interval pattern
from the root. It is reference-catalog data — created, edited, listed, and
deleted through the `/reference` editor UI (`ReferenceScaleForm.cshtml`,
`ReferenceScaleList.cshtml`) and persisted as one JSON file per record under
`data/reference/scales/<slug>.json` via `DataStore`
(`src/Dadabe.Editor/Services/DataStore.cs:40`).

**How this differs from "mode" test data**: `mode.schema.json` describes a
*derived* scale-like entity — one that names its `parentScale` and a
`degreeIndex` into that parent (`schemas/mode.schema.json:8-9`), and whose
`name`/`intervals` are required while `noteNames` is optional. A *scale*, by
contrast, is the canonical/parent entity: `notes` is **required** (not
optional, unlike a mode's `noteNames`), and it carries the inverse relationship
field `modeOf` (`schemas/scale.schema.json:10`) — a free-text pointer back to a
parent scale when the scale itself is conceived of as someone else's mode (e.g.
"Dorian" stored as a scale with `modeOf: "Major"`). Rich scale test data should
therefore emphasize **canonical, full note-spelling correctness** and the
**scale-to-scale (`modeOf`) cross-reference graph**, whereas mode test data
emphasizes the **degree-index derivation** relationship. Don't duplicate degree
arithmetic here — focus on note/interval fidelity and the catalog's breadth of
scale *families*.

## 2. Schema shape summary (`schemas/scale.schema.json`)

| Field | Type | Required? | Notes |
|---|---|---|---|
| `name` | `string` | yes (`schemas/scale.schema.json:13`) | Display name, e.g. "Major", "Harmonic Minor", "Whole Tone". Becomes the slug source via `DataStore.ToSlug` (`src/Dadabe.Editor/Services/DataStore.cs:80`). |
| `notes` | `string[]` | **yes** (`schemas/scale.schema.json:8,13`) | "Ordered pitch names in scale (e.g., C, D, E, ...)" — free-form strings, no enum/pattern constraint in the schema itself. |
| `intervals` | `int[]` | **yes** (`schemas/scale.schema.json:9,13`) | "Semitone steps from the root (e.g., 0,2,4,5,7,9,11)" — plain integers, no min/max/uniqueness constraint. |
| `modeOf` | `string` | no | "Optional: parent scale if this is a mode" (`schemas/scale.schema.json:10`) — a loose string reference, not a schema-level relation to another scale record. |
| `description` | `string` | no | Free text shown in theory-notes tooling per `docs/v0.5/design.md:413-417` ("interval pattern and characteristic chord quality"). |

`additionalProperties: false` (`schemas/scale.schema.json:14`) — extra fields
will fail validation, so generated fixtures must stick to exactly these five
keys.

Contrast with `mode.schema.json:6-13`: modes require only `name` + `intervals`
(everything else, including `noteNames`, is optional), reflecting that a mode
can be defined purely by its interval signature while deferring concrete
spelling to its parent scale. Scales, conversely, are expected to be
self-sufficient — both `notes` and `intervals` must be present and (implicitly)
mutually consistent in length and order.

## 3. How the code consumes/produces this data

**It's pure CRUD reference data — not (yet) wired into prediction/expansion logic.**
A scan of `src/Dadabe.Core/Chord/ChordExpander.cs`, `NextChordPredictor.cs`, and
`PredictCommand.cs` shows **no references to `ScaleModel`/`ScaleDto`**. In
`PredictCommand.cs:128-132`, the `byScale` prediction-filter type is explicitly
stubbed out:

```csharp
case "byScale":
case "byMode":
case "custom":
    warnings.Add($"Filter type '{filter.Type}' is not implemented in v0.4; it was ignored.");
    break;
```

and `PredictCommand.cs:154` excludes `byScale`/`byMode`/`custom` from the
metadata `filtersApplied` node entirely. `PredictionFilterDto`
(`src/Dadabe.Cli/Io/PredictionFilterDto.cs:6`) documents `byScale` as a
recognized filter `Type` string, but no downstream code resolves a scale name
to pitch classes — so test data does **not** need to round-trip through
chord-expansion math. (`ScaleDto` itself is explicitly marked
`// Placeholder DTO for scales` at `src/Dadabe.Cli/Io/ScaleDto.cs:3`.)

The actual consumption surface is the **Reference editor**:

- **Storage round-trip**: `ReferenceService.SaveScale`/`GetScale`/`ListScales`/`DeleteScale`
  (`src/Dadabe.Editor/Services/ReferenceService.cs:67-78`) wrap
  `DataStore.Save<ScaleModel>`/`Load`/`LoadAll`/`Delete`
  (`src/Dadabe.Editor/Services/DataStore.cs:43-75`), which serialize with
  `JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }`
  (`src/Dadabe.Editor/Services/DataStore.cs:8-12`) — meaning `null` optional
  fields (`modeOf`, `description`) are *omitted* from persisted JSON, not
  written as `null`. Test fixtures that assert exact persisted shape need to
  account for this.
- **Slug derivation**: `DataStore.ToSlug` (`src/Dadabe.Editor/Services/DataStore.cs:80-82`)
  lower-cases, trims, and collapses runs of non-`[a-z0-9]` characters to single
  hyphens, e.g. `"Harmonic Minor"` → `harmonic-minor`, `"C# Phrygian Dominant"`
  → `c-phrygian-dominant`. Names that collapse to the *same* slug (e.g. "Whole
  Tone" vs "Whole-Tone" vs "whole tone!!") are a real collision risk worth
  testing — `LoadAll` (`DataStore.cs:48-51`) would then overwrite one file with
  another on save.
- **Form parsing/round-trip**: `ParseScaleForm` (`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:197-212`)
  reads comma-separated `notes`/`intervals` strings from form posts via
  `ParseStringList`/`ParseIntList` (`ReferenceRoutes.cs:214-223`).
  `ParseIntList` **silently drops** any token that doesn't `int.TryParse`
  (`ReferenceRoutes.cs:216-217`), and `ParseStringList` simply trims and
  removes empty entries (`ReferenceRoutes.cs:222-223`) with **no validation
  that note strings are real pitch names** — so `"C, X, Bb♭♭, , 7"` would
  silently become `["C", "X", "Bb♭♭", "7"]`. Rich fixtures should include
  inputs designed to probe this leniency (and any future tightening).
- **List/edit rendering**: `ReferenceScaleList.cshtml:23-24` joins `Notes` and
  `Intervals` back into comma-separated display strings; `ReferenceScaleForm.cshtml:8-9`
  does the same for the edit dialog's pre-filled values — so round-tripping
  `string.Join(", ", notes)` → parse → `string.Join` should be idempotent for
  well-formed data.
- **`modeOf` is a bare string, not a foreign key**: nothing in
  `ReferenceService`/`ReferenceRoutes` resolves `ModeOf` against the scales
  catalog — it's stored and displayed as-is (`ReferenceScaleForm.cshtml:31-32`
  shows it as a plain text input). Test data can legitimately include `modeOf`
  values that don't match any existing scale's `name`/slug ("dangling
  reference"), self-references (`modeOf == name`), or reference chains
  (A `modeOf` B `modeOf` C).
- **Theory-notes rendering (planned, v0.5)**: `docs/v0.5/design.md:413-417`
  describes a future read-only "Scales" theory tab driven entirely by the
  existing `description` field — reinforcing that `description` should contain
  meaningful prose like "Whole tone: symmetric, no leading tone — dreamlike,
  Impressionist sound" for realistic fixtures.
- **Note model exists but isn't enforced on scales**: `src/Dadabe.Core/Note.cs:78-116`
  defines a rich `Note.TryParse` that accepts letters A–G with `#`/`b`/`♯`/`♭`/`⨯`
  accidentals constrained to `MinAccidental = -2`/`MaxAccidental = +2`
  (`Note.cs:29-30`, double-flat through double-sharp, e.g. `Fbb`, `B##`/`B⨯`).
  **`ScaleModel.Notes` is plain `List<string>`** with no parsing through
  `Note` — so scale fixtures are free to (and *should*, for richness) include
  spellings at the edges of or beyond what `Note.TryParse` would accept (triple
  sharps, lowercase letters, Unicode accidentals, enharmonic respellings like
  `Fb` vs `E`) to see how the looser scale layer behaves relative to the
  stricter `Note`/`PitchClass` layer used elsewhere in `Dadabe.Core`.

## 4. Recipe for building a rich scale test dataset

Vary along these independent dimensions; combine them so fixtures stress
multiple behaviors at once.

### 4.1 Note count / scale cardinality
- **Pentatonic (5 notes)**: Major Pentatonic `[0,2,4,7,9]`, Minor Pentatonic `[0,3,5,7,10]`.
- **Hexatonic (6 notes)**: Whole Tone `[0,2,4,6,8,10]`, Blues (6-note variant) `[0,3,5,6,7,10]`, Augmented `[0,3,4,7,8,11]`.
- **Heptatonic / diatonic (7 notes)**: Major (Ionian) `[0,2,4,5,7,9,11]`, Natural Minor (Aeolian) `[0,2,3,5,7,8,10]`, Harmonic Minor `[0,2,3,5,7,8,11]`, Melodic Minor (ascending) `[0,2,3,5,7,9,11]`.
- **Octatonic (8 notes)**: Diminished (whole-half) `[0,2,3,5,6,8,9,11]` and (half-whole) `[0,1,3,4,6,7,9,10]`.
- **Edge sizes**: a 4-note fixture (e.g. a "diminished seventh arpeggio as scale": `[0,3,6,9]`) and a chromatic 12-note fixture `[0,1,2,3,4,5,6,7,8,9,10,11]` — both legal per the schema (no length constraint) and good stress for any future code that assumes "7 notes."

### 4.2 Interval-pattern shapes
- **Consecutive semitones** (minimal gaps): chromatic scale, or any scale segment with `[0,1,2,...]` runs — tests rendering/parsing of dense interval lists.
- **Large gaps (augmented seconds, 3-semitone steps)**: Harmonic Minor's `7→8→11` gap, Hungarian Minor `[0,2,3,6,7,8,11]`, Phrygian Dominant `[0,1,4,5,7,8,10]` — all contain a 3-semitone leap, an interesting "exotic" marker.
- **Symmetric / repeating patterns**: Whole Tone (every step = 2), Diminished (alternating 2/1 or 1/2), Augmented (alternating 3/1) — these have *multiple* valid root reframings and are the best stress-cases for any logic that assumes a unique tonic.
- **Asymmetric heptatonic**: any standard major-scale mode, for contrast with the symmetric set.

### 4.3 Scale "families" / naming breadth
Cover at least: major/minor diatonic family, melodic & harmonic minor and their modes-as-scales (e.g. "Lydian Dominant" stored as its own scale), pentatonic & blues, whole-tone & augmented (symmetric), octatonic/diminished (symmetric), and "exotic"/non-Western-labeled sets such as Hungarian Minor, Persian, Enigmatic (`[0,1,4,6,8,10,11]` — deliberately irregular), Hirajoshi (Japanese pentatonic, `[0,2,3,7,8]`), and Bebop scales (8-note chromatic-passing-tone variants of Major/Dominant). This breadth exercises `name`/slug diversity (spaces, hyphens, numerals like "Bebop Dominant") and gives `description` fixtures real music-theory prose to carry.

### 4.4 Root notes & enharmonic spelling
Vary the `notes` root across naturals, sharps, flats, and theoretically-correct-but-unusual spellings:
- Natural roots: `C`, `G`, `D`...
- Sharp/flat roots that are common: `F#`/`Gb` major scales (enharmonic pairs — generate *both* spellings of the same `intervals` pattern as separate fixtures to test whether downstream code/UI treats them as distinct catalog entries).
- Theoretically "correct" but rare spellings that exercise the edges of `Note`'s `MinAccidental`/`MaxAccidental = ±2` range (`src/Dadabe.Core/Note.cs:29-30`): a `Cb` major scale (`Cb, Db, Eb, Fb, Gb, Ab, Bb`, using `Fb`/`Cb` rather than `E`/`B`), or a theoretical `G#` major scale requiring `Fx`/`F##` (double sharp) — since `Note.TryParse` supports `⨯`/double accidentals (`Note.cs:108-110`) but `ScaleModel.Notes` doesn't enforce parsing, both "valid Note" and "invalid/out-of-range Note-string" fixtures are meaningful.
- A fixture whose `notes` mixes letter cases or uses Unicode accidentals (`♯`, `♭`) to probe the unvalidated `ParseStringList` path (`ReferenceRoutes.cs:222-223`).

### 4.5 The `modeOf` relationship graph
- A scale with `modeOf` pointing to an existing catalog scale's `name` (e.g. `"Dorian"` with `modeOf: "Major"`).
- A scale with `modeOf` pointing to a **non-existent** name (dangling reference) — valid per schema, useful for testing absence-of-FK-validation.
- A **self-referential** `modeOf == name` fixture.
- A **chain**: scale A → `modeOf` B → `modeOf` C, to probe any future graph-walking logic.
- A scale with `modeOf` omitted entirely (the common case for "root" scale families like Major and Melodic Minor).

### 4.6 Optional-field / persistence-shape coverage
- One fixture with both optionals present (`modeOf` + `description`), one with neither (to confirm `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` (`DataStore.cs:11`) omits the keys on save), and one with only `description` (no `modeOf`) — verifying partial-optional persistence round-trips.
- A `description` containing the kind of theory prose the v0.5 "Theory notes" tab expects (`docs/v0.5/design.md:413`): interval-pattern summary + characteristic chord-quality callout, e.g. `"Whole Tone: six whole steps, no perfect fifth or leading tone — augmented/dominant #11 colour."`

### 4.7 Slug-collision and name-format stress
- Two scale `name`s that normalize to the same slug via `DataStore.ToSlug` (`DataStore.cs:80-82`), e.g. `"Whole Tone"` and `"Whole-Tone!"` (both → `whole-tone`), to probe overwrite-on-save behavior in `LoadAll`/`Save`.
- Names with embedded numerals (`"8-Tone Spanish"`), apostrophes (`"Today's Blues Scale"`), and non-ASCII letters (`"Über-Lydian"`) — `ToSlug` strips anything outside `[a-z0-9]`, so these collapse interestingly and are good slug-generation fixtures.

### 4.8 Length-mismatch / consistency stress (schema permits, code may not expect)
- A fixture where `notes.Length != intervals.Length` (e.g. 7 notes but only 5 intervals) — legal per the schema (no cross-field constraint), good for probing any future code that zips the two arrays.
- A fixture where `intervals` is **not sorted ascending** or doesn't start at `0` — again schema-legal, and a useful probe for assumptions baked into rendering or future scale-degree math.

## 5. Concrete example test cases

### 5.1 Symmetric scale (Whole Tone) — tests symmetric/no-unique-root logic
```json
{
  "name": "Whole Tone",
  "notes": ["C", "D", "E", "F#", "G#", "A#"],
  "intervals": [0, 2, 4, 6, 8, 10],
  "description": "Symmetric hexatonic scale built entirely from whole steps; no perfect fourth/fifth or leading tone — dreamlike, Impressionist colour (e.g. Debussy)."
}
```

### 5.2 Octatonic / symmetric diminished, with `modeOf` chain — tests large dataset + cross-reference + 8-note cardinality
```json
{
  "name": "Half-Whole Diminished",
  "notes": ["C", "Db", "Eb", "E", "F#", "G", "A", "Bb"],
  "intervals": [0, 1, 3, 4, 6, 7, 9, 10],
  "modeOf": "Whole-Half Diminished",
  "description": "Octatonic symmetric scale alternating half- and whole-steps; the dominant-scale rotation of the whole-half diminished scale, used over altered V7(b9,#9,#11) chords."
}
```

### 5.3 Pentatonic with enharmonic-edge spelling — tests note-spelling fidelity & accidental edges
```json
{
  "name": "Gb Major Pentatonic",
  "notes": ["Gb", "Ab", "Bb", "Db", "Eb"],
  "intervals": [0, 2, 4, 7, 9],
  "modeOf": "Major Pentatonic",
  "description": "Major pentatonic transposed to Gb; flat spelling chosen over the enharmonic F# pentatonic to match guitar fretboard flat-key conventions."
}
```

### 5.4 Exotic 7-note scale with large interval gap and dangling `modeOf` — tests asymmetric/exotic family + unresolved cross-reference
```json
{
  "name": "Hungarian Minor",
  "notes": ["C", "D", "Eb", "F#", "G", "Ab", "B"],
  "intervals": [0, 2, 3, 6, 7, 8, 11],
  "modeOf": "Double Harmonic Major",
  "description": "Harmonic minor with a raised 4th; contains two augmented-second (3-semitone) gaps, giving a distinctive 'Gypsy' or Eastern-European sound."
}
```

(Note: 5.4's `modeOf` deliberately references a scale name that may not exist in
the same fixture batch — exercising the "dangling reference" case from §4.5,
since `modeOf` is stored verbatim with no FK validation, per
`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:210` and
`src/Dadabe.Editor/Services/ReferenceService.cs:71-77`.)
