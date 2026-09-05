# Dadabe v0.5.2 — Transform Engine + Song Mode Foundation

## Overview

v0.5.2 implements the parts of `progression-transforms.md` and `ui-song-mode.md` that are
cheap, self-contained, and unblocked. Everything requiring a settled open question, a
reliable key inference path, or a new solver mode moves to `docs/v0.6/design.md`.

The release has two halves that meet in one place:

1. **Transform engine** — the missing symbol-emission primitive (D44), interval-based
   transposition with a real spelling policy (D45), the key-blind transform family, and
   playability ranking (D51). Every transform here is root-motion or grammar-mechanical,
   so none of them need a key and none of them can produce a chord the grammar cannot
   spell.

2. **Song mode foundation** — the Song / Section / Slot model (D37), song-owned tuning
   (D38) and hand constraints (D42), pin provenance (D43), pinning from the voicings tab,
   and voice-led filling of unpinned slots. This is the data model and the two workflows
   that stand on their own; arrangement, transposition, and the songwriting loop wait for
   v0.6.

They meet at **playability ranking**: a transform result is scored by running the already
deployed `VoicingSearch` and `VoiceLeadSolver` over it, on the active tuning. That is the
one capability no other chord tool has, and it is pure integration of shipped code.

The guiding cut: **v0.5.2 ships nothing that needs `KeyInference` to succeed.** Key
inference is displayed as read-only information (§10) but no transform depends on it. That
single rule is what keeps this release cheap, and it is why the whole key-aware transform
family sits in v0.6.

Decisions D37–D53 are specified in the two source documents in this folder; this doc says
which parts land now and how.

---

## 1. Symbol emission (D44)

Every transform is `List<string> → List<string>` over chord symbols, so the pipeline is
parse → transform → **emit**. Emission does not currently exist anywhere in
`Dadabe.Core`. Nothing else in this release can start until it does.

Most of the work is already done by the parser: `ChordParser` stores the matched form's
`DisplayName` in `ChordSymbol.Quality` (`ChordParser.cs:181`) and canonicalises each
modifier to `modifier.Tokens[0]`. So `maj7` not `M7`, `m` not `-`, `add9` not some
variant. Quality and modifier tokens are already canonical; only reassembly is missing.

```csharp
// src/Dadabe.Core/Chord/ChordSymbol.cs
/// <summary>
/// Renders this symbol back to a string the parser accepts. Emission order is
/// canonical (see below); the parser accepts modifiers in any order, so the
/// round-trip identity is over parsed values, not over the original text.
/// </summary>
public string ToSymbol();
```

**Canonical emission order** — `parseRules.modifierOrder` is `any`, so emission must pick
one order and keep it:

```
Root + Quality + Extensions + Alterations + ("/" + Bass)
```

`Extensions` then `Alterations`, each in the order the grammar declares them
(`add9, add11, add13`, then `b5, #5, b9, #9, #11, b13`). Not the order the user typed —
normalising is the point. `C7#9b13` and `C7b13#9` both emit as `C7#9b13`.

### The round-trip property

The correctness bar is a property test over the whole grammar cross-product — 25 forms ×
9 modifiers × the spellable roots — not a handful of examples:

```
parse(format(parse(s))) == parse(s)     for every s the parser accepts
```

Note the shape: `format(parse(s)) == s` is **false** by design (`C-` emits as `Cm`,
`C7b13#9` reorders). The identity holds over parsed values, which is what matters for
chaining transforms.

---

## 2. Transposition and spelling policy (D45)

`Note` is `Letter` + `Accidental` (−2..+2), with `Note.LetterPlus` walking the
stacked-thirds letter cycle (D12). This is the asset that makes correct transposition
possible: tools that store roots as pitch classes 0–11 cannot spell results correctly and
have to guess D♭ vs C♯ from a global preference.

**Transpose by named interval, not by semitone count.** `Interval` is already
`(Semitones, Quality)` where `Quality` is a label like `P5`, `m3`, `A4` — the diatonic
number is the digits in that label, so letter-steps are derivable with no new type:

```csharp
// src/Dadabe.Core/Interval.cs
/// <summary>Letter-steps this interval moves: the digits of Quality, minus 1.
/// P5 → 4, m3 → 2, A4 → 3. Drives spelling-correct transposition (D45).</summary>
public int DiatonicSteps { get; }

// src/Dadabe.Core/Note.cs
/// <summary>Transposes by letter-walk then accidental correction, so the result
/// is spelled rather than guessed. Throws nothing — see overflow policy.</summary>
public Note Transpose(Interval interval);
```

Algorithm: new letter = `LetterPlus(Letter, interval.DiatonicSteps)`; accidental =
whatever makes the semitone distance come out to `interval.Semitones`. A minor 2nd is
(1 letter-step, 1 semitone) and an augmented unison is (0, 1) — both move one semitone and
they spell differently, correctly.

**Semitone requests still work.** The UI offers "+1 semitone"; the service picks the
interval whose resulting key signature has the fewest accidentals, ties to flats. `[C, F,
G]` up 1 becomes `[Db, Gb, Ab]` (four flats) rather than `[C#, F#, G#]` (seven sharps);
up 6 it becomes `[F#, B, C#]` rather than `[Gb, Cb, Db]`.

### Overflow policy

`Note`'s constructor **throws** outside ±2 accidentals, and a transform chain can reach
B♯♯. Do not let that exception escape. `Note.Transpose` respells enharmonically, keeps
going, and records a `SpellingNote` on the result:

> `Fbb` respelled as `Eb` — this key is past what standard notation handles comfortably.

Surfaced in the transform diff (§7) as a per-chord marker. An exception from three steps
into a chain is neither honest nor actionable; a flagged respelling is both.

---

## 3. Transform engine (D50, partial)

Activates the `ITransformation` stub in `src/Dadabe.Core/ITransformation.cs` and the
`Namespaces.Transform` memo namespace reserved since v0.1.

```csharp
// src/Dadabe.Core/Transform/
public sealed record TransformStep(
    string Type,                                    // "retrograde", "transpose", …
    IReadOnlyDictionary<string, object> Params);

public sealed record TransformResult(
    IReadOnlyList<string> Chords,                   // emitted symbols
    IReadOnlyList<TransformNote> Notes,             // skips, respellings, warnings
    bool Invertible);

/// <param name="Index">Chord index the note applies to, or null for whole-progression.</param>
public sealed record TransformNote(int? Index, string Kind, string Message);

public interface ITransformation   // extends the existing stub
{
    string Id { get; }
    string DisplayName { get; }
    bool IsInvertible { get; }
    TransformResult Apply(IReadOnlyList<ChordSymbol> chords, IReadOnlyDictionary<string, object> parameters);
}
```

`TransformStep` uses `Type` + `Params` to match the wire shape already committed in
`schemas/transformation.schema.json` — that placeholder reserved `{ "type", "params" }`
and there is no reason to diverge from it.

**Chains** are a `List<TransformStep>` applied left to right in a single request. Parse
once at the front, emit once at the back (transform-internal steps pass `ChordSymbol`, not
strings) — this settles transforms open question 2 in favour of symbols at the boundary
without paying re-parse cost per step. Saved, named, reusable chains are v0.6.

**Registry**: a `TransformCatalog` keyed by `Type`, resolved by DI, enumerable so §6 can
run everything at once.

**Invertibility** is declared per transform. Transpose (negate the interval), retrograde,
rotate, invert, and P/L/R are invertible; quality-map, reduce, and tritone-sub are not.
`inverse(transform(x)) == x` is the property test for the invertible half.

---

## 4. Key-blind transforms (D47, D48, D52, D53 — all partial)

Every transform below is either root-motion only or grammar-mechanical. None needs a key,
and none can produce a chord outside the grammar's 25 forms + 9 modifiers.

| `type` | Parameters | Effect | Invertible |
| --- | --- | --- | --- |
| `transpose` | `interval` (e.g. `M2`) or `semitones` | Every root moves; qualities unchanged | yes |
| `retrograde` | — | Reverse the chord order | yes |
| `rotate` | `by` (int) | Rotate the sequence, preserving the cycle | yes |
| `invert` | `axis` (note) | Mirror the root sequence about an axis | yes |
| `interval-negate` | — | Negate the root-motion intervals | yes |
| `interval-reverse` | — | Reverse the motion list, re-derive from the original start | yes |
| `interval-multiply` | `factor` (5 or 7) | Serial M5/M7 on the interval list | no |
| `quality-map` | `to` (grammar quality) | Blanket quality replacement, roots unchanged | no |
| `reduce` | `level` (`triad` \| `seventh`) | Strip extensions toward the core form | no |
| `tritone-sub` | `positions` (optional) | Dominant chords only: root +A4, quality kept | yes |
| `plr` | `op` (`P` \| `L` \| `R`) | Neo-Riemannian operation on triads | yes |

Notes on the less obvious entries:

**`invert`** mirrors *roots*, not chord tones. True pitch-class-set inversion produces sets
with no symbol in the grammar; the roots-only version is always emittable. This is a
deliberate scope limit, not an oversight — negative harmony, which is the preset users
actually want, needs a key for its axis and is therefore v0.6.

**`interval-multiply`** with factor 7 maps chromatic steps onto the circle of fifths and
produces deeply chromatic results. Ship it labelled as an algorithmic-composition tool, not
as a reharmonisation.

**`reduce`** is mechanical from the grammar: drop `Extensions` and `Alterations`, then map
the form to its core (`maj9`/`maj7` → `maj`, `m11` → `m`, `13`/`9`/`7` → `maj` at
`level: triad`, or → `7` at `level: seventh`). This is the campfire-version transform, and
paired with §5 it answers "reduce this chart until every chord is playable on my tuning."

**`plr`** operates on the triad itself, not on a key, which is why it lands here rather
than with the key-aware family:

| op | example | voice movement |
| --- | --- | --- |
| P | C ↔ Cm | 3rd moves 1 semitone |
| L | C ↔ Em | root moves 1 semitone |
| R | C ↔ Am | 5th moves 2 semitones |

Each holds two of three notes, so PLR output is smooth by construction and comes back from
`VoiceLeadSolver` with near-zero motion cost. Each is its own inverse.

### Chord-shape guards (D53)

Applied uniformly, and reported as `TransformNote`s rather than silently:

- **Thirdless chords** (`5`, `sus2`, `sus4`) — `quality-map` and `plr` skip them. "Make it
  minor" on a power chord is a no-op; on `Csus4` it is a category error. Checked against
  the form's `required` list in the grammar, not special-cased by name.
- **Non-triads under `plr`** — skipped and reported. The v0.5.2 default is strict
  skip-and-report; the global strict/loose setting is v0.6.
- **Non-dominants under `tritone-sub`** — skipped and reported. Gated on the form having
  `3` and `b7` required.
- **Slash chords** — `transpose` and `invert` move the bass with the root; `quality-map`,
  `reduce`, and `plr` keep it; `tritone-sub` drops it and reports, because the bass was
  chosen for the chord being replaced.

---

## 5. Playability ranking (D51)

The reason this is DADABE's transformer and not a theory toy. For each transform result,
run `VoicingSearch` per chord and `VoiceLeadSolver` across the sequence — the exact
pipeline `VoiceLeadRoutes.cs` already runs — and report:

```csharp
public sealed record PlayabilityScore(
    int WorstComfortPct,                 // the hardest single chord
    int TotalDistance,                   // hand travel across the progression
    int MinFret, int MaxFret,            // where on the neck it lives
    IReadOnlyList<string> Unplayable);   // chords with no voicing above the floor
```

Ranking is by `Unplayable.Count` ascending, then `WorstComfortPct` descending, then
`TotalDistance` ascending. A result with an unplayable chord always sorts last and is
labelled, never hidden — "this is a good idea you cannot play on this tuning" is useful
information.

**Apply-all** — running the whole `TransformCatalog` at default parameters and returning
the ranked list is then a loop, and it is the discovery mode: *here are your progression's
transformations, best-playable first, on your tuning.* Under an alternate tuning which
transforms come out comfortable is genuinely unpredictable, so this is discovery rather
than filtering.

Key inference before/after is included as read-only annotation (`"C major → no clear key
centre"`), and degrades honestly when `InferKey` returns null. **No transform branches on
it.**

---

## 6. CLI — `transform` subcommand

```
dadabe transform --chords "C F G"
                 [--progression <slug>]
                 --chain "retrograde,transpose:M2"
                 [--all]
                 [--tuning DADABE]
                 [--min-comfort 0.7]
                 [--out <file.json>] [--pretty] [--validate-schema]
```

`--chords` or `--progression`, one required. `--chain` is a comma-separated step list,
each `type` or `type:arg` (multi-parameter steps use `type:k=v;k=v`). `--all` ignores
`--chain` and runs the whole catalogue, ranked per §5.

Output is `Envelope<TransformResultDto>` with `"command": "transform"`,
`"schemaVersion": "1"`, following `VoiceLeadCommand`'s existing pattern.

```csharp
// src/Dadabe.Cli/Io/Dtos.cs
public sealed record TransformResultDto(
    [property: JsonPropertyName("source")]      IReadOnlyList<string> Source,
    [property: JsonPropertyName("results")]     IReadOnlyList<TransformVariantDto> Results);

public sealed record TransformVariantDto(
    [property: JsonPropertyName("chain")]       IReadOnlyList<TransformStepDto> Chain,
    [property: JsonPropertyName("chords")]      IReadOnlyList<string> Chords,
    [property: JsonPropertyName("playability"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                                                PlayabilityDto? Playability,
    [property: JsonPropertyName("keyBefore"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                                                string? KeyBefore,
    [property: JsonPropertyName("keyAfter"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                                                string? KeyAfter,
    [property: JsonPropertyName("notes")]       IReadOnlyList<TransformNoteDto> Notes);

public sealed record TransformStepDto(
    [property: JsonPropertyName("type")]        string Type,
    [property: JsonPropertyName("params")]      IReadOnlyDictionary<string, object> Params);

public sealed record PlayabilityDto(
    [property: JsonPropertyName("worstComfort")]  int WorstComfort,
    [property: JsonPropertyName("totalDistance")] int TotalDistance,
    [property: JsonPropertyName("minFret")]       int MinFret,
    [property: JsonPropertyName("maxFret")]       int MaxFret,
    [property: JsonPropertyName("unplayable")]    IReadOnlyList<string> Unplayable);

public sealed record TransformNoteDto(
    [property: JsonPropertyName("index"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
                                                int? Index,
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("message")]     string Message);
```

`schemas/transform-result.schema.json` is new. `schemas/transformation.schema.json` is
tightened from its placeholder state: `type` constrained to the registered transform ids,
`params` left open.

---

## 7. Editor — transform panel

A *Transform* action on each row of `/progressions` opens a panel (`<wa-dialog>`)
containing:

- **Chain builder** — a list of steps; each step is a `<wa-select>` of transform types plus
  a parameter control that swaps on the selected type. Add / remove / reorder.
- **Diff preview** — two rows, original above and result below, aligned by index, changed
  chords highlighted. Length-changing transforms are not in this release, so index
  alignment always holds.
- **Annotations** — key before/after, the §5 playability line, and every `TransformNote`
  (skipped chords, respellings) rendered inline against the chord it applies to.
- **Actions** — *Save as new progression* (`ProgressionService.Create`, with provenance) or
  *Replace*.

An *Explore* button runs `--all` and renders the ranked list, each row a one-line summary
with a *Use* action that loads it into the panel.

### Provenance

`ProgressionModel` gains one optional field:

```csharp
[property: JsonPropertyName("derivedFrom")] DerivedFrom? DerivedFrom

public sealed record DerivedFrom(string SourceSlug, IReadOnlyList<TransformStep> Chain);
```

Optional and `WhenWritingNull`-ignored, so existing progression files are untouched. "Where
did this come from?" gets an answer, and re-running the chain after editing the source is
one click.

---

## 8. Songs — data model and CRUD (D37, D38, D42, D43)

One JSON file per song in `data/songs/<slug>.json`; sections and slots nest inside rather
than getting their own directories, because a song is edited as a unit and is the thing a
user hands to someone else.

```csharp
// src/Dadabe.Editor/Services/SongService.cs
public sealed record SongModel(
    string Slug,
    string Name,
    string? Tuning,                       // D38 — catalog name or comma spec
    string? Key,                          // user override; inference is display-only
    int? Tempo,
    SongHand? Hand,                       // D42
    List<SongSection> Sections) : ISlugged;

public sealed record SongSection(string Id, string Name, List<SongSlot> Slots);

/// <param name="Voicing">Pinned shape, or null for a chord whose fingering is unchosen.</param>
public sealed record SongSlot(
    string Symbol,
    int Bars,
    PinnedVoicing? Voicing,
    PinProvenance? PinnedUnder);

/// Positions, not a search-result index — result ordering is not stable across
/// model versions or search parameters.
public sealed record PinnedVoicing(
    List<PinnedPosition> Positions, int ComfortPct, string Structure);

public sealed record PinnedPosition(int String, int? Fret, bool Muted, bool Open);

public sealed record SongHand(
    int? Frets, int? Span, int? MinStrings, int? MaxStrings,
    bool? AllowOpen, bool? AllowBarre, bool? AllowThumb, List<string>? Categories);

public sealed record PinProvenance(int ModelVersion, string Tuning, string HandHash);
```

`DataStore` gains `SongsDir => Path.Combine(Root, "songs")` and includes it in
`EnsureDirectories`.

**The slot is the load-bearing shape.** A slot with `Voicing == null` is a chord you know
but have not fingered. That single nullable field is what makes §9's fill, §10's practice
targets, and the unfinished-song badge possible, and it is why a section is not simply a
list of pinned voicings.

### Active song and section (D37)

`PageShell` gains an active-song `<wa-select>` beside the existing active-tuning select,
and an active-section select that renders only when the active song has > 0 sections.
Selection persists in a cookie, matching how the active tuning already works. With no song
selected the editor behaves exactly as it does today — this is the compatibility guarantee
for the whole feature.

### Song owns its tuning (D38)

Selecting an active song **sets the active tuning** to the song's tuning. If the user then
changes the active tuning while a song is active, show a `<wa-callout variant="warning">`:
every pinned shape in the song is meaningless in another tuning. The tuning select on the
voicings form goes read-only while a song is active, with an explicit unlock.

This is the guard rail that makes pinning safe. Without it users will pin standard-tuning
shapes into a DADABE song and not find out until they pick up the guitar. Changing a
saved song's tuning outright is v0.6 (it needs the retune/refill flow).

### Song hand constraints (D42)

`SongHand` mirrors the advanced options already on the voicings form (D36). Every search
launched from song context uses them, so "no barres" is set once as an arrangement
decision rather than re-entered per search. Null fields fall back to `HandModel.Default`.

### Pin provenance (D43)

Each pin records `{ modelVersion, tuning, handHash }`. Positions stay playable forever, but
the comfort *number* was computed under a particular model — and the comfort model was
rebalanced in v0.4.1 (barre and mute penalties; C went from 29 to 142 voicings above the
70% threshold), so a stored 82% from before that change is not comparable to 82% today.
When provenance does not match current, badge the slot and offer a one-click re-score.

Cheap now, impossible to reconstruct later. This is the item most likely to be regretted if
deferred.

---

## 9. Pinning and filling (D37 §5.2)

### Pin from the voicings tab

Each row of the voicings result gains a pin button, enabled when a song (and section, if
any exist) is active. `POST /api/songs/{slug}/sections/{id}/slots` appends a slot carrying
the chord symbol, the voicing positions, and provenance. Re-pinning a slot replaces its
voicing, which is also how a user swaps a shape they have changed their mind about.

This follows the workflow as written in `ui-song-mode.md`. Searching inline from a slot is
the better long-term workflow and is open question 1 — v0.6.

### Fill unpinned slots by voice leading

The best feature in the set, and it needs no solver change. `VoiceLeadSolver.Solve` already
takes an array of candidate arrays per chord:

- **unpinned slot** → the full `VoicingSearch` result, filtered by the song's min-comfort
  and `SongHand`
- **pinned slot** → an array of exactly one candidate

A single-element layer is a fixed node in the DAG, so the solver routes around it
automatically. The user pins the two shapes they love, presses *Fill*, and the solver
chooses everything between them at minimum motion without touching the commitments.

`POST /api/songs/{slug}/sections/{id}/fill` returns the section with previously-unpinned
slots now pinned. `solutions` (1–5) offers alternates to audition, reusing the k-shortest
paths the solver already computes. Re-runnable at any time — raise min-comfort because the
tune is fast, and refill.

If a chord has no voicing above the floor, the fill fails naming that chord, exactly as
`VoiceLeadRoutes` already does.

---

## 10. Song read-outs

All aggregations over data already computed. No new theory, no new search.

- **Key inference** — `KeyInference.InferKey` over the whole song and per section, shown as
  *"Key: A minor (inferred)"* with the user override taking precedence. Where it returns
  null, say *"no clear key centre"* — that is a true and useful statement about a modal
  tune, not a failure. Display only; nothing branches on it.
- **Position map** — min/max fret per section from the pinned positions, rendered as a
  strip: *Verse: frets 0–4 · Chorus: 5–9 · Bridge: 2–7*. Position changes are what you
  rehearse, and nothing in the app currently says whether you have a song that stays in
  first position or one that jumps to the 12th twice.
- **Practice targets** — pinned voicings sorted by comfort ascending. The weakest link is
  what breaks in performance, and the song gets one honest headline number ("hardest
  chord: 38%") rather than a meaningless average. The *"here are three alternatives"*
  drill-down is v0.6.
- **Completion** — count of unpinned slots per section, so an unfinished song looks
  unfinished.

### Transitions by which strings move

`VoiceLeadSolver.BuildTransition` returns a `VoiceMove` list with per-string from/to frets,
and today only the scalar `TransitionDistance` is rendered. A total distance of 4 tells a
guitarist nothing; *"only the B string moves, 2 frets"* and *"common tones on strings 1, 2,
5 — hold them"* is how the move is actually taught.

`VoiceLeadStepRow` gains the move list, and `VoiceLeadingResult.cshtml` renders it. Flag
the good cases explicitly: no motion (a pivot), one finger changes, common tones held.
Applies to both the voice-lead page and the song section view.

---

## 11. Chord diagram rendering

`VoicingRow.Diagram` (`ChordDiagram` / `ChordDiagramString` in `Slices/Models.cs`) is built
on every voicings request today and never rendered — the v0.5 Web Awesome migration pruned
the `.vcd*` rules from `app.css` and the results table shows ASCII only.

Reinstate rendering as a CSS-grid partial: strings as columns, frets as rows, muted/open
markers above the nut, a start-fret label when `StartFret > 1`. No JS, no canvas, no
external assets. Used by the voicings results table (collapsible, ASCII stays the default
compact view) and by the song chart (§12), where it is the primary display.

Cheap, self-contained, zero dependencies, and it retires a piece of scaffolding that has
been computed-and-discarded since v0.4.1.

---

## 12. Song chart, export, and progression interop

### The chart

`GET /songs/{slug}/chart` renders sections in order, each slot showing chord symbol, bars,
and its diagram, with a print stylesheet (`@media print`) so it goes on a music stand.
Header carries key, tuning, tempo, and the per-section position ranges from §10.

Sections render in definition order. Arrangement — repeats, `Chorus ×2` — is v0.6, and
the chart is built so that adding a form list later changes only the section iteration.

### Export and import

- **Song JSON** — `GET /api/songs/{slug}/export` returns the file as-is; import accepts one
  back. The `DataStore` shape is already the wire format.
- **Plain text** — chords over bars, for pasting into a message.

### Progression interop

Both directions, both cheap:

- **Paste a chart into a section** — `Am7 D7 Gmaj7 Cmaj7` → four unpinned slots, using the
  same whitespace split and parse path as the voice-lead form.
- **Instantiate a saved progression as a section** — the library already ships
  `twelve-bar-blues`, `pachelbel-canon`, `jazz-ii-v-i`, `andalusian-cadence` and more, so
  these become section templates for free.
- **Promote a section to a saved progression** — `ProgressionService.Create` from the
  section's symbols, so a part worked out in one song is reusable in the next.

This makes `ui-song-mode.md`'s "each section is a progression" concrete and bidirectional,
and it means transforms (§7) reach song sections via a save-and-transform round trip in
this release, before the direct *transform this section* action arrives in v0.6.

---

## 13. Tests

**Core — symbol emission and spelling**

- `ChordSymbolEmitTests` — the round-trip property over the full grammar cross-product:
  `parse(format(parse(s))) == parse(s)`. Explicit cases for canonical reordering (`C7b13#9`
  → `C7#9b13`), alias normalisation (`C-` → `Cm`, `CM7` → `Cmaj7`), and slash chords.
- `NoteTransposeTests` — spelling correctness: `C` +m2 → `Db`, `C` +A1 → `C#`, `[C,F,G]` +1
  semitone → `[Db,Gb,Ab]`, +6 → `[F#,B,C#]`. Overflow respells and emits a note instead of
  throwing.
- `IntervalTests` — `DiatonicSteps` for every quality label in `DefaultQuality`.

**Core — transforms**

- `TransformCatalogTests` — every registered transform round-trips through `ToSymbol`;
  every declared-invertible transform satisfies `inverse(transform(x)) == x`.
- `PlrTests` — P/L/R on major and minor triads against the table in §4; non-triads skipped
  with a `TransformNote`; each operation is its own inverse.
- `TransformGuardTests` — `quality-map` and `plr` skip `C5` and `Csus4`; `tritone-sub`
  skips non-dominants; slash-chord bass handling per transform.
- `ReduceTests` — `Cmaj9 → C`, `F#m11 → F#m`, `G13 → G` at `level: triad` and `G13 → G7` at
  `level: seventh`.
- `TransformChainTests` — retrograde + transpose composes as the source doc's example
  (`[C,F,G] → [A,G,D]` at +2).

**Fretboard / integration**

- `PlayabilityScoreTests` — worst comfort, total distance, and fret span on a known
  progression and tuning; a chord with no voicing above the floor lands in `Unplayable`
  and sorts the result last.

**Editor**

- `SongServiceTests` — CRUD; a slot with a null voicing survives a save/load round trip;
  provenance is recorded on pin.
- `SongFillTests` — a section with two pinned and two unpinned slots fills only the
  unpinned ones and leaves the pinned positions byte-identical.
- Integration: `GET /songs` 200; `POST` a slot; `POST` fill returns the section fragment;
  `GET /songs/{slug}/chart` 200.

**CLI**

- `TransformCommandTests` — `--chain` parses and applies in order; `--all` returns the
  catalogue ranked; envelope validates against `transform-result.schema.json`.

---

## 14. Checklist

### Foundation

- [ ] `ChordSymbol.ToSymbol()` with canonical emission order (D44)
- [ ] Round-trip property test over the grammar cross-product
- [ ] `Interval.DiatonicSteps` (D45)
- [ ] `Note.Transpose(Interval)` with enharmonic-respell overflow policy (D45)
- [ ] Semitone → best-spelled-interval selection

### Transform engine

- [ ] `TransformStep`, `TransformResult`, `TransformNote` in `src/Dadabe.Core/Transform/`
- [ ] Extend `ITransformation`; `TransformCatalog` registry + DI
- [ ] The eleven key-blind transforms in §4
- [ ] Chord-shape guards: thirdless, non-triad, non-dominant, slash (D53)
- [ ] `PlayabilityScore` + ranking (D51)
- [ ] Apply-all across the catalogue

### CLI

- [ ] `TransformCommand.cs`; wire `transform` in `Program.cs`
- [ ] Transform DTOs in `Dtos.cs`
- [ ] `schemas/transform-result.schema.json`; tighten `schemas/transformation.schema.json`

### Editor — transforms

- [ ] Transform panel: chain builder, diff preview, annotations
- [ ] Explore (apply-all) ranked list
- [ ] `DerivedFrom` provenance on `ProgressionModel`

### Editor — songs

- [ ] `SongModel` / `SongSection` / `SongSlot` / `PinnedVoicing` / `PinProvenance` (D37, D43)
- [ ] `DataStore.SongsDir`; `SongService` CRUD
- [ ] Active-song and active-section selects in `PageShell` (cookie-persisted)
- [ ] Song tuning: set on select, warn on mismatch, lock the voicings tuning select (D38)
- [ ] `SongHand` plumbed into song-context searches (D42)
- [ ] Pin action on voicing rows
- [ ] Fill unpinned slots via `VoiceLeadSolver` with pinned slots as single-candidate layers
- [ ] Key display, position map, practice targets, completion counts
- [ ] Per-string transition rendering on voice-lead and song views
- [ ] Chord diagram partial + `.vcd` CSS reinstated
- [ ] Song chart with print stylesheet
- [ ] Export: song JSON, plain text
- [ ] Progression interop: paste, instantiate, promote

### Docs

- [ ] `CHANGELOG.md` v0.5.2 entry
- [ ] `docs/todo.md`, `docs/design.md`, `docs/future.md` updated

---

## 15. Non-goals

Everything here is v0.6 or later; see `docs/v0.6/design.md` for why each is deferred.

- **No key-aware transforms.** Diatonic transposition, parallel-mode, functional
  substitution, negative harmony, and secondary-dominant / ii–V / passing-diminished
  insertion all wait. Nothing in v0.5.2 branches on `KeyInference`.
- **No length-changing transforms.** Insertion and its inverse need harmonic-rhythm
  handling the model does not have; the diff preview assumes index alignment.
- **No saved transform chains.** Chains apply within a request; named recipes are v0.6.
- **No song arrangement.** No form list, no repeats, no seam analysis, no loop closure.
- **No song transposition or capo.** Both need the retune/refill flow.
- **No inline search from a slot**, no alternatives drawer, no songwriting predict-and-pin
  loop.
- **No cadence detection**, no scale/mode suggestion.
- **No setlists.**
- Unchanged from v0.5: no tab, notation, or audio rendering; no polychord parsing; no new
  voicing categories; no persistent memo backends beyond in-memory.

---

## 16. Files to create / modify

| File | Action |
| --- | --- |
| `src/Dadabe.Core/Chord/ChordSymbol.cs` | Update — `ToSymbol()` |
| `src/Dadabe.Core/Interval.cs` | Update — `DiatonicSteps` |
| `src/Dadabe.Core/Note.cs` | Update — `Transpose(Interval)`, overflow respell |
| `src/Dadabe.Core/ITransformation.cs` | Update — activate the stub |
| `src/Dadabe.Core/Transform/TransformTypes.cs` | Create — step, result, note records |
| `src/Dadabe.Core/Transform/TransformCatalog.cs` | Create — registry |
| `src/Dadabe.Core/Transform/RootMotion.cs` | Create — transpose, retrograde, rotate, invert, interval-* |
| `src/Dadabe.Core/Transform/QualityTransforms.cs` | Create — quality-map, reduce, tritone-sub, plr |
| `src/Dadabe.Fretboard/PlayabilityScore.cs` | Create — scoring + ranking |
| `src/Dadabe.Cli/Commands/TransformCommand.cs` | Create |
| `src/Dadabe.Cli/Commands/Mappings.cs` | Update — transform mappings |
| `src/Dadabe.Cli/Io/Dtos.cs` | Update — transform DTOs |
| `src/Dadabe.Cli/Program.cs` | Update — `transform` subcommand |
| `schemas/transform-result.schema.json` | Create |
| `schemas/transformation.schema.json` | Update — tighten from placeholder |
| `schemas/progression.schema.json` | Update — optional `derivedFrom` |
| `src/Dadabe.Editor/Services/DataStore.cs` | Update — `SongsDir` |
| `src/Dadabe.Editor/Services/SongService.cs` | Create |
| `src/Dadabe.Editor/Services/ProgressionService.cs` | Update — `DerivedFrom` |
| `src/Dadabe.Editor/Services/TransformService.cs` | Create |
| `src/Dadabe.Editor/Routes/SongRoutes.cs` | Create |
| `src/Dadabe.Editor/Routes/TransformRoutes.cs` | Create |
| `src/Dadabe.Editor/Routes/VoicingRoutes.cs` | Update — pin action, song-context hand model |
| `src/Dadabe.Editor/Routes/VoiceLeadRoutes.cs` | Update — per-string moves in the result |
| `src/Dadabe.Editor/Routes/ProgressionRoutes.cs` | Update — transform action |
| `src/Dadabe.Editor/Slices/Models.cs` | Update — song and transform view models |
| `src/Dadabe.Editor/Slices/SongsIndex.cshtml` | Create |
| `src/Dadabe.Editor/Slices/SongsList.cshtml` | Create |
| `src/Dadabe.Editor/Slices/SongsForm.cshtml` | Create |
| `src/Dadabe.Editor/Slices/SongSection.cshtml` | Create |
| `src/Dadabe.Editor/Slices/SongChart.cshtml` | Create |
| `src/Dadabe.Editor/Slices/ChordDiagram.cshtml` | Create — shared partial |
| `src/Dadabe.Editor/Slices/TransformPanel.cshtml` | Create |
| `src/Dadabe.Editor/Slices/TransformResult.cshtml` | Create |
| `src/Dadabe.Editor/Slices/VoicingsResult.cshtml` | Update — pin button, diagram |
| `src/Dadabe.Editor/Slices/VoiceLeadingResult.cshtml` | Update — per-string moves |
| `src/Dadabe.Editor/PageShell.cs` | Update — song/section selects, Songs nav entry |
| `src/Dadabe.Editor/wwwroot/app.css` | Update — reinstate `.vcd*`, add print styles |
| `src/Dadabe.Editor/wwwroot/app.js` | Update — active song/section binding |
| `tests/Dadabe.Core.Tests/Chord/ChordSymbolEmitTests.cs` | Create |
| `tests/Dadabe.Core.Tests/NoteTransposeTests.cs` | Create |
| `tests/Dadabe.Core.Tests/Transform/TransformCatalogTests.cs` | Create |
| `tests/Dadabe.Core.Tests/Transform/PlrTests.cs` | Create |
| `tests/Dadabe.Core.Tests/Transform/TransformGuardTests.cs` | Create |
| `tests/Dadabe.Fretboard.Tests/PlayabilityScoreTests.cs` | Create |
| `tests/Dadabe.Editor.Tests/SongServiceTests.cs` | Create |
| `tests/Dadabe.Editor.Tests/SongFillTests.cs` | Create |
| `tests/Dadabe.Cli.Tests/TransformCommandTests.cs` | Create |
| `CHANGELOG.md` | Add v0.5.2 entry |
