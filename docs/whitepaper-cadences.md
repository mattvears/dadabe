# Whitepaper: Generating Rich Test Data for Cadences

## Why this matters

A "cadence" record in Dadabe is a small reference-catalog entry describing a
named cadential pattern — e.g. an authentic (V→I) or plagal (IV→I) close — along
with an example chord progression that illustrates it. These records live in
the editor's reference-data catalog (`src/Dadabe.Editor/data/reference/cadences/`,
provisioned by `src/Dadabe.Editor/Services/DataStore.cs:38`) and are surfaced
through the Reference UI for browsing, creating, and editing
(`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:16-52`).

Today the cadence catalog is a **standalone reference list** — it is not yet
consumed by `NextChordPredictor` or `KeyInference` (neither file mentions
"cadence"), and `SchemaValidationTests.cs` does not validate it either. That
makes the cadence catalog *exactly* the kind of area where rich, well-formed
seed/test data matters most: it is the contract that any future
harmonic-analysis feature (e.g. "does this progression resolve like an
authentic cadence?") will be built against, and it is the only thing today's
editor round-trips through `DataStore.LoadAll<T>` / `Save<T>`
(`src/Dadabe.Editor/Services/DataStore.cs:43-67`). Bad or narrow seed data here
will silently propagate into whatever consumes it next. Generating data that
exercises every field, every enum value, every optional combination, and a
range of musically real (and musically weird) progressions gives the schema,
the storage layer, and the eventual harmonic-analysis consumer a much better
chance of being built — and tested — correctly from day one.

## Schema shape summary

Source: `schemas/cadence.schema.json`

| Field | Type / constraint | Required? | Notes |
| --- | --- | --- | --- |
| `type` | `string`, **enum**: `authentic`, `plagal`, `deceptive`, `half` (`schemas/cadence.schema.json:7`) | **yes** (`schemas/cadence.schema.json:12`) | Closed enum — only these four cadence families are schema-legal. Anything else fails validation. |
| `exampleProgression` | `array` of `string` (`schemas/cadence.schema.json:9`) | **yes** | No `minItems`/`maxItems`/`uniqueItems` constraint — empty arrays, single-chord arrays, and very long arrays are all schema-legal. Items are free-form strings (chord symbols *or* Roman numerals — see below). |
| `resolution` | `string`, free text, e.g. `"V->I"` (`schemas/cadence.schema.json:8`) | optional | Described as "expected harmonic resolution"; no enum or pattern — any string passes, including empty string, unicode arrows (`→`), or multi-step chains. |
| `description` | `string` (`schemas/cadence.schema.json:10`) | optional | Free text annotation. |
| — | `additionalProperties: false` (`schemas/cadence.schema.json:13`) | — | Extra keys (e.g. a stray `slug` in a payload meant for the schema) will fail validation — important because the *editor's* `CadenceModel` adds a `slug` field that is **not** part of the public schema. |

Compare with the envelope (`schemas/envelope.schema.json:7`): the envelope's
`command` enum currently only allows `voicings`, `tuning`, `chord`
(`schemas/envelope.schema.json:13`) — there is no `cadence`/`reference` command
yet, confirming cadences are not (yet) emitted through the CLI's envelope
pipeline. Test data for cadences should therefore be validated as bare
documents against `cadence.schema.json`, not wrapped in an envelope.

## How the code consumes/produces this data

**DTO vs. editor model — two shapes for the "same" thing.**
The CLI-side DTO is a thin placeholder record:
`src/Dadabe.Cli/Io/CadenceDto.cs:4` —
`record CadenceDto(string Type, string[] ExampleProgression, string? Resolution = null, string? Description = null)`.
The editor-side model adds a `Slug`:
`src/Dadabe.Editor/Services/ReferenceService.cs:6-11` —
`record CadenceModel(string Slug, string Type, string? Resolution, List<string> ExampleProgression, string? Description)`.
Rich test data should include payloads that exercise **both** shapes: schema-pure documents (no `slug`, matching `CadenceDto`/`cadence.schema.json` exactly) *and* editor-catalog documents (with `slug`, matching what `DataStore.LoadAll<CadenceModel>` will actually deserialize from disk). A document with `slug` validated against the bare schema should be expected to **fail** (`additionalProperties: false`).

**Storage is file-per-record, slugged, alphabetically ordered.**
`DataStore.LoadAll<T>` (`src/Dadabe.Editor/Services/DataStore.cs:43-52`) globs `*.json` out of `CadencesDir` (`src/Dadabe.Editor/Services/DataStore.cs:38`), deserializes each into `CadenceModel`, and orders by `Slug`. This means:

- Test data should include **multiple records with deliberately chosen slugs** to verify ordering (e.g. `authentic-aab123`, `plagal-9f0e21`, `deceptive-…`, `half-…`) — sorting is lexical on the slug string, not on `type` or insertion order.
- A malformed JSON file in the directory will throw during `JsonSerializer.Deserialize<T>(...)!` (note the `!` — null-forgiving, no null-check) — worth a "corrupt file in directory" test case at the integration level.
- `LoadAll` returns `[]` if the directory doesn't exist (`src/Dadabe.Editor/Services/DataStore.cs:45-46`), so an empty-catalog scenario is also part of the contract (the UI explicitly renders `<p class="empty-state">No cadences yet.</p>` — `src/Dadabe.Editor/Slices/ReferenceCadenceList.cshtml:12`).

**Slugs are server-generated from `type`, not user-supplied.**
`ParseCadenceForm` (`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:161-176`) builds new slugs as `type + "-" + Guid.NewGuid().ToString("N")[..6]` run through `DataStore.ToSlug` (`src/Dadabe.Editor/Services/DataStore.cs:80-82`, which lower-cases and replaces runs of non-`[a-z0-9]` with `-`). This means **every legitimate generated slug starts with one of the four enum values** (`authentic-…`, `plagal-…`, `deceptive-…`, `half-…`) followed by a 6-char hex suffix. Test data that includes slugs violating this convention (e.g. `my-custom-cadence`, slugs with uppercase, slugs colliding across types) is useful for exercising the *load* path even though the *create* path can't produce them — useful for testing hand-edited/imported catalog files.

**Required-field validation lives in the route handler, not the schema.**
`ParseCadenceForm` rejects empty `type` with `"Type is required."` (`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:166`), and `SaveCadence` separately rejects empty `Slug` with `"Slug is required."` (`src/Dadabe.Editor/Services/ReferenceService.cs:45-46`). Note the **type enum is enforced only by the `<wa-select>` UI** (`src/Dadabe.Editor/Slices/ReferenceCadenceForm.cshtml:18`, hard-coded `["authentic","plagal","deceptive","half"]`) — a raw POST with an arbitrary `type` string (e.g. `"phrygian"` or `"plagal "` with trailing space) will pass `ParseCadenceForm`'s whitespace check and be persisted, producing a catalog entry that **violates `cadence.schema.json`'s enum**. This drift between UI-enforced and schema-enforced constraints is a prime target for test data: craft entries with off-enum `type` values and confirm how downstream schema validation (or lack thereof) handles them.

**The `chords` form field becomes `exampleProgression`, and order/blanks matter.**
Form parsing filters blank entries but preserves order: `form["chords"].Where(c => !string.IsNullOrWhiteSpace(c))…ToList()` (`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:169`). The dynamic add/remove UI (`addChord()` in `src/Dadabe.Editor/Slices/ReferenceCadenceForm.cshtml:61-71`) lets a user submit zero, one, or many chord inputs, including ones left blank — so test data should cover **0-chord, 1-chord, and N-chord** progressions, and should verify that blank/whitespace-only entries are dropped while order of the remaining entries is preserved.

**Free-text fields accept arbitrary strings — including the conventions actually used elsewhere in the codebase.**
`resolution` is illustrated in the schema as `"V->I"` (ASCII arrow, `schemas/cadence.schema.json:8`) but the form placeholder uses a unicode arrow `"e.g. V→I"` (`src/Dadabe.Editor/Slices/ReferenceCadenceForm.cshtml:26`) and the existing round-trip test uses ASCII (`tests/Dadabe.Cli.Tests/ScaleCadenceModeTests.cs:17`, `"V->I"`). Good test data should include **both notations** (and perhaps multi-hop resolutions like `"ii-V-I"` or `"iv→V→i"`) to make sure nothing assumes one convention. Likewise `exampleProgression` items appear as **Roman numerals** in the existing test fixture (`["V", "I"]`, `tests/Dadabe.Cli.Tests/ScaleCadenceModeTests.cs:16`) but as **literal chord symbols** in the editor placeholder text (`"e.g. G7"`, `src/Dadabe.Editor/Slices/ReferenceCadenceForm.cshtml:40`/`:67`) and in the list display which simply joins them with commas (`src/Dadabe.Editor/Slices/ReferenceCadenceList.cshtml:24`). Since the schema places no constraint distinguishing the two, **both styles are schema-legal and both appear "in the wild" in this codebase** — rich test data must include progressions expressed as Roman-numeral functions, as absolute chord symbols, and (for stress-testing) mixtures of the two within one array.

## Recipe for building a rich test dataset

Vary along these independent dimensions, and combine them so the cross-product
covers the schema's real degrees of freedom:

1. **Cadence type — cover the full enum, plus deliberate violations**
   - One of each: `authentic`, `plagal`, `deceptive`, `half` (`schemas/cadence.schema.json:7`).
   - At least one **off-enum** value (e.g. `"phrygian"`, `"Picardy"`, `"Authentic"` with different casing, `"plagal "` with trailing whitespace) to test schema-validation rejection vs. the editor's looser persistence path (`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:166` only checks for blank).

2. **`exampleProgression` length and notation**
   - Empty array `[]` (schema-legal; UI can produce it if all chord rows are removed).
   - Single-element array (e.g. a half cadence ending mid-phrase: `["V"]`).
   - Classic 2-chord resolutions (`["V","I"]`, `["IV","I"]`, `["V","vi"]`, `["iv","V"]`).
   - Longer chains (4-6 chords) showing the full cadential approach, e.g. `["ii","V7","I","vi"]` or a full ii–V–I–vi turnaround.
   - Very long / pathological arrays (10+ entries) to probe rendering (`string.Join(", ", …)` in `src/Dadabe.Editor/Slices/ReferenceCadenceList.cshtml:24`) and storage round-tripping.
   - Mixed notation within one array: Roman numerals (`"V7"`, `"viio6"`) vs. absolute chord symbols (`"G7"`, `"Cmaj7"`, `"Dm7b5"`) vs. slash chords (`"G/B"`) — since both conventions independently appear in the codebase (test fixture vs. UI placeholder), exercises that nothing assumes one over the other.
   - Whitespace/blank entries mixed in (`["V", "  ", "I"]`) to confirm the form-parsing filter behavior (`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:169`) is matched by whatever validates persisted catalog files directly.

3. **Optional-field combinations** — generate the full 2×2×2 matrix of presence/absence for `resolution` and `description` (both present, both absent, only one present each way), since `JsonIgnoreCondition.WhenWritingNull` (`src/Dadabe.Editor/Services/ReferenceService.cs:35` / `src/Dadabe.Editor/Services/DataStore.cs:11`) means absent vs. `null` vs. empty-string serialize differently on disk.
   - `resolution` notational variants: ASCII arrow (`"V->I"`), unicode arrow (`"V→I"`), multi-hop (`"ii→V→I"`), descriptive prose (`"dominant resolves up a fourth to tonic"`), empty string `""`.
   - `description` ranging from absent, to terse (`"Standard authentic cadence"`, matching `tests/Dadabe.Cli.Tests/ScaleCadenceModeTests.cs:17`), to long-form prose with modal/contextual nuance.

4. **Modal / tonal context flavor (even though the schema has no explicit `mode`/`key` field)** — bake context *into* the example progressions and descriptions, since that's the only place it can currently live:
   - Tonal/major-key authentic cadence: `["G7","Cmaj7"]`, description noting "V7→I in C major".
   - Minor-key / harmonic-minor flavor: `["E7","Am"]` (V7→i), description noting raised leading tone.
   - Modal cadences that don't fit classical tonal function (e.g. Dorian ♭VII–I "backdoor" plagal-like motion: `["Bb","C"]`/`["bVII","I"]`), explicitly described as modal so a reader/consumer can distinguish "plagal" in the classical sense from a modal cadential gesture mis-tagged as `plagal`.
   - Phrygian half-cadence flavor (`iv6–V`) tagged as `half`, to probe whether `type` enum values can stretch to cover modal phenomena.

5. **Classic named cadences vs. invented/edge-case ones**
   - Canonical textbook examples for each of the four types (perfect authentic V–I, plagal "Amen" IV–I, deceptive V–vi, half ending on V).
   - A "compound"/ambiguous case — e.g. a progression that could be read as either deceptive or half depending on context (`["V7","IV"]`) — to test how consumers might disambiguate using `description`/`resolution` as tie-breakers.
   - An invented/extended cadence using extended harmony (`["V7#9","Imaj9"]`, `["Dm7b5","G7alt","Cm(maj7)"]`) to probe whether free-text chord symbols with alterations round-trip and render correctly.

6. **Slug variations for the storage/listing layer**
   - Well-formed generated slugs (`authentic-3f9a21`) matching the real generation convention (`src/Dadabe.Editor/Routes/ReferenceRoutes.cs:168`).
   - Hand-authored/imported slugs that don't follow the `type-hash` convention, to test `LoadAll`'s alphabetical ordering (`src/Dadabe.Editor/Services/DataStore.cs:50`) independent of `type`.
   - Slug/​type mismatches (slug says `plagal-…` but `type` is `"deceptive"`) — schema-legal (slug isn't in the public schema) but a useful "data integrity" probe for the editor catalog.

7. **Catalog-level scenarios** (multi-file, integration-level)
   - Empty catalog directory → renders `"No cadences yet."` (`src/Dadabe.Editor/Slices/ReferenceCadenceList.cshtml:12`).
   - One file per enum `type` plus one off-enum, to verify list rendering and sort order together.
   - A deliberately corrupt/malformed JSON file alongside valid ones, to probe the unguarded `Deserialize<T>(...)!` in `LoadAll` (`src/Dadabe.Editor/Services/DataStore.cs:49`).

## Example test cases

**1. Canonical authentic cadence (full optional fields, ASCII resolution arrow, Roman numerals — matches the existing unit-test convention):**

```json
{
  "type": "authentic",
  "resolution": "V->I",
  "exampleProgression": ["V7", "I"],
  "description": "Perfect authentic cadence: dominant seventh resolves to tonic in root position."
}
```

**2. Plagal cadence using absolute chord symbols and a unicode resolution arrow (matches the editor's placeholder convention), minimal optional fields:**

```json
{
  "type": "plagal",
  "resolution": "IV→I",
  "exampleProgression": ["F", "C"],
  "description": "The 'Amen' cadence in C major."
}
```

**3. Deceptive cadence with extended/altered harmony, minor-key flavor, no `description` (tests optional-field absence and jazz-style chord symbols):**

```json
{
  "type": "deceptive",
  "resolution": "V7->vi",
  "exampleProgression": ["G7alt", "Am9"]
}
```

**4. Half cadence, edge case: single-chord progression, modal (Phrygian) context folded into free text, no `resolution` (tests minimal required-only shape plus the "modal cadence tagged with a classical type" ambiguity):**

```json
{
  "type": "half",
  "exampleProgression": ["E"],
  "description": "Phrygian half cadence (iv6-V) ending suspended on the dominant; context implies E Phrygian over an A pedal."
}
```

**5. Off-enum / negative test case — intentionally schema-invalid `type`, to verify validators reject it even though the editor's form-handler would accept it (only checks for blank, `src/Dadabe.Editor/Routes/ReferenceRoutes.cs:166`):**

```json
{
  "type": "picardy",
  "resolution": "i->I",
  "exampleProgression": ["Cm", "C"],
  "description": "Picardy third: minor-key piece resolves to a major tonic chord — not in the schema's closed enum, should fail validation."
}
```

