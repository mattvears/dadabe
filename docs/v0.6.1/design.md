# Dadabe v0.6.1 — Design doc: chord inversions & open-position comfort modeling

## Overview

Two independent asks landed as scratch prompts in this directory
(`inversions-prompt.md`, `hand-model-prompt.md`) rather than as design work, so this
document is the design pass over both before either gets built. (`inversions-prompt.md`
says "v0.5.4 design document" — there is no such file; v0.5.4 was never cut, and the
prompt's own title line and the sibling `hand-model-prompt.md` both say v0.6.1. Treated
as a slip and filed here, next to `bugs.md`, where the rest of this release's notes live.)

1. **Relative chord inversions (D58)** — today a voicing's bass note is either
   whatever the search happens to find, or an *absolute* pitch typed via slash-chord
   syntax (`C/E`). Nothing lets a caller ask for "the first inversion" without already
   knowing which letter that is, and nothing labels which inversion a voicing turned
   out to be once found.
2. **Open-position comfort & resonance modeling (D59, extends D54)** — the D54
   biomechanics penalties (v0.5.3) score how hard a shape is to *fret*; they don't
   credit what a shape gets for free by using open strings, and they don't model
   acoustics at all. This section audits which of the six criteria in
   `hand-model-prompt.md` are already covered, which are covered but under-weighted,
   and which need a new, separate score rather than more terms jammed into `Comfort`.

Decisions continue from D57 (`chord-grammar.md` — `sus4` as a modifier). New decisions
here are D58 and D59.

---

## 1. Relative chord inversions (D58)

### 1.1 What already exists

More of this is already in place than the prompt assumes:

- **`FretPosition.Function`** labels every sounded string with its chord-tone function
  (`"1"`, `"3"`, `"b7"`, …), assigned by `Reachability.Compute`.
- **`Voicing.BassNote`** already finds the lowest sounding pitch.
- **`ChordSpec.Bass`** already constrains a search to a specific bass pitch class
  (`VoicingSearch.LowestSoundingPitchClassIs`) — this is how slash chords work today.
- **`VoicingCategories.json`**'s `drop-2`/`drop-3` templates already encode inversion
  structurally: each is "one template per inversion (bass = 5, 7, 1, 3 …)" via
  `functionSequenceLowToHigh`. The concept of "which chord tone sits in the bass"
  already drives classification — it just isn't exposed as data.

What's missing is exactly the two things the prompt asks for:

- **Input**: a way to request "the Nth inversion" *relatively* — without the caller
  pre-computing which absolute note that is. `ChordSpec.Bass` only accepts an absolute
  `Note`, sourced from typed slash-chord syntax.
- **Output**: nothing computes or labels *which* inversion a found voicing is in. A
  plain `Cmaj7` search today can return a voicing with `E` in the bass, and nothing on
  `Voicing` says "that's a first inversion."

### 1.2 What "relative inversion" means here

Standard stacked-thirds numbering, relative to the chord's own tone stack rather than
to any absolute note: inversion 0 = root in the bass, 1 = the chord's 3rd in the bass,
2 = the 5th, 3 = the 7th (only reachable on a four-note-or-larger form). The ordinal
indexes into the chord's *natural* tone order — its `Tones` sorted by scale degree,
**excluding** any tone carrying the `ChordSpec.BassFunction` label (D17's "foreign
slash bass" tone, e.g. the `D` in `C/D`, isn't part of the stack being inverted).

`ChordExpander.ScaleDegreeOf` already parses a function string to a degree — the
ordering this needs already exists, just not exposed for reuse outside `ChordExpander`.

### 1.3 Data model: a new ordering helper, shared by input and output

Both the search-time constraint (1.4) and the always-on label (1.5) need the same
"natural tone order" — so it belongs on `ChordSpec`, not duplicated in both places:

```csharp
// ChordSpec.cs
/// <summary>
/// This chord's tones in stacked-thirds order (root, 3rd, 5th, 7th, 9th, …),
/// excluding a foreign slash bass (BassFunction) — that tone isn't part of the
/// stack an inversion is relative to. Backs both the relative-inversion search
/// constraint and the always-on Voicing.Inversion label (D58).
/// </summary>
public ImmutableArray<ChordTone> NaturalToneOrder =>
    Tones
        .Where(t => t.Function != BassFunction)
        .OrderBy(t => ChordExpander.ScaleDegreeOf(t.Function))
        .ToImmutableArray();
```

(`ChordExpander.ScaleDegreeOf` moves from `internal` to `internal` visible via
`InternalsVisibleTo` it already has for `Dadabe.Core` — no access change needed since
`ChordSpec` and `ChordExpander` are both in `Dadabe.Core.Chord`.)

### 1.4 Input: requesting an inversion

**Rejected: grammar syntax** (e.g. `Cmaj7^1`). It would have to coexist with slash-chord
syntax (`C/E`), which already occupies "say what's in the bass" — two notations for
the same slot invites `Cmaj7/E^1` ambiguity for no real gain, and it complicates
`ChordSymbol`'s round-trip identity (D12) for a feature that is a *search* preference,
not a property of the chord itself. A `Cmaj7` and "a `Cmaj7` played in first inversion"
are the same chord; the inversion is a fact about the voicing, not the symbol.

**Resolved: a `SearchParams` field**, following the precedent already shipped for
`RequireRoot` (2026-09-04: CLI flag → `SearchParams` → `VoicingSearch` → Editor
checkbox):

```csharp
// SearchParams.cs
public sealed record SearchParams(
    int MaxFret,
    int MaxSpan,
    int MinStrings,
    int MaxStrings,
    bool AllowOpen,
    bool AllowBarre,
    bool AllowThumb,
    ImmutableArray<string> Categories,
    bool RequireRoot = false,
    int? RequireInversion = null) : IContentHashable   // <-- new
```

`RequireInversion` joins `RequireRoot` in the `ContentHash` (one more conditional byte
block — `null` hashes as absent, `0..N` as `U8(1)` + the ordinal, matching how `Bass`
is appended to `ChordSpec.ContentHash` today).

Validation: `RequireInversion` must be `< spec.NaturalToneOrder.Length`, and it is a
request error (not silently zero results) to combine it with an explicit slash-chord
`spec.Bass` — the two are different ways of pinning the same slot and one has to lose;
better to reject than silently pick a winner. `VoicingsCommand.Run` and the Editor route
both check this before calling `VoicingSearch.Search`.

### 1.5 Search-time enforcement

`VoicingSearch.TryEmit` already has exactly this shape of check for the absolute case
(`LowestSoundingPitchClassIs`, keyed on pitch class). The relative case is the same
check keyed on **function** instead, which is actually simpler because `FretPosition`
already carries `Function`:

```csharp
// VoicingSearch.cs, alongside the existing spec.Bass check in TryEmit
if (p.RequireInversion is { } inv)
{
    var targetFunction = spec.NaturalToneOrder[inv].Function;
    if (!LowestSoundingFunctionIs(positions, targetFunction)) { return; }
}

private static bool LowestSoundingFunctionIs(ImmutableArray<FretPosition> positions, string function)
{
    var lowestMidi = int.MaxValue;
    string? lowestFn = null;
    foreach (var p in positions)
    {
        if (p.Muted || p.SoundingPitch is not { } pitch) { continue; }
        if (pitch.Midi < lowestMidi) { lowestMidi = pitch.Midi; lowestFn = p.Function; }
    }
    return lowestFn == function;
}
```

No change to `Reachability`, `Classifier`, or `Playability` — the constraint slots in
next to the existing bass check, at the same point in the pipeline, using data that's
already computed.

### 1.6 Output: always-on tracking, independent of whether one was requested

This is the part the prompt calls out as a hard requirement ("always calculated,
tracked, and displayed"), and it's the cheap half of this feature: `Voicing` already
carries `ChordSpec` and `Positions`, so the inversion label is a pure derived property
— no constructor change, no `VoicingSearch.TryEmit` change, no `ContentHash` impact,
following the same pattern as `BassNote`/`TopNote`/`Span` (computed, not stored):

```csharp
// Voicing.cs
/// <summary>
/// Ordinal into ChordSpec.NaturalToneOrder of the function sounding in the
/// bass: 0 = root position, 1 = first inversion, 2 = second, 3 = third.
/// Null when there is no sounded bass (all muted) or the bass is a foreign
/// slash tone (BassFunction) rather than a member of the chord's own stack —
/// that case is reported via InversionLabel instead (D58).
/// </summary>
public int? Inversion
{
    get
    {
        var bassFn = Sounded.OrderBy(p => p.SoundingPitch!.Value.Midi)
            .FirstOrDefault(p => p.SoundingPitch is not null)?.Function;
        if (bassFn is null || bassFn == ChordSpec.BassFunction) { return null; }
        var order = ChordSpec.NaturalToneOrder;
        for (var i = 0; i < order.Length; i++)
        {
            if (order[i].Function == bassFn) { return i; }
        }
        return null;  // bass function not in the natural stack at all (shouldn't happen)
    }
}

private static readonly string[] OrdinalNames =
    ["root position", "1st inversion", "2nd inversion", "3rd inversion", "4th inversion", "5th inversion"];

/// <summary>Human-readable form of <see cref="Inversion"/> for display (D58).</summary>
public string InversionLabel
{
    get
    {
        var bassFn = Sounded.OrderBy(p => p.SoundingPitch!.Value.Midi)
            .FirstOrDefault(p => p.SoundingPitch is not null)?.Function;
        if (bassFn is null) { return "—"; }
        if (bassFn == ChordSpec.BassFunction) { return $"slash ({BassNote})"; }
        var inv = Inversion;
        return inv is { } i && i < OrdinalNames.Length ? OrdinalNames[i] : $"inversion {inv}";
    }
}
```

### 1.7 Scoring-system impact

The honest assessment: **inversion is a classification/labeling concern, not a new
comfort penalty.** Physical difficulty is already fully explained by span, finger
assignment, and barre placement (D15/D54) — none of that changes because a 5th
happens to be in the bass instead of the root. Adding an inversion-specific term to
`ComputeComfort` would double-count difficulty that the existing rules already price
in via the actual fret/finger geometry of whatever shape realizes that inversion.

Two real (smaller) interactions, both handled by composition rather than new code:

- **`RequireRoot` + `RequireInversion`** compose without conflict — requiring the root
  present (anywhere) and requiring the 5th in the bass are independent constraints
  (`requiredFunctions.Add("1")` and the bass-function check run separately in
  `TryEmit`).
- **Voice leading** (`VoiceLeadSolver`) is not changed by this design. A smooth bass
  line that prefers a particular inversion at each step is real and desirable, but is
  new solver behavior, not a data-model gap — flagged as future work, not in scope
  here.

### 1.8 UI and output surface

Same three layers `RequireRoot` already touches:

| Layer | Change |
| --- | --- |
| `Program.cs` | New `--inversion <n>` CLI option (`Option<int?>`), threaded into `SearchParams.RequireInversion` next to `requireRootOption`, on both the `voicings` and `voice-lead` commands. |
| `Mappings.cs` `ToDto` | `VoicingDto` gains `Inversion` (`int?`) and `InversionLabel` (`string`), read straight off the new `Voicing` properties. |
| `schemas/voicings.schema.json` | `$defs.voicing.properties` gains `"inversion": {"type": ["integer","null"], "minimum": 0}` and `"inversionLabel": {"type": "string"}`, both added to `required` — "always calculated" means the schema should not treat them as optional. |
| `Dadabe.Editor/Slices/VoicingOptionsFields.cshtml` | New control alongside the existing checkboxes: `<wa-select label="Inversion" name="inversion">` with options `Any` (default, `RequireInversion` unset), `Root`, `1st`, `2nd`, `3rd`. |
| `Dadabe.Editor/Routes/VoicingRoutes.cs` | Parse `inversion` the same way `requireRoot` is parsed today; pass through to `SearchParams`. |
| `Models.cs` | `VoicingRow` and `VoicingFragmentRow` each gain an `InversionLabel` (`string`) field. |
| `VoicingsResult.cshtml` / `VoicingFragment.cshtml` | New `Inversion` column next to `Structure`, rendered plainly (no glossary lookup needed — the label is already human-readable, unlike `Structure`'s category codes). |

### 1.9 Open questions

- **[OQ]** Extended chords (9th/11th/13th forms) have more than four tones in
  `NaturalToneOrder`, so inversions 4/5/6 exist mechanically but are rarely called
  that in practice (nobody says "the fourth inversion of C13"). Recommend capping the
  UI's dropdown at 3rd and leaving `--inversion 4` etc. reachable only via the CLI/API
  for the rare caller who wants it — `OrdinalNames` above already covers up to 5th
  defensively so the API never returns a raw `$"inversion {n}"` fallback for a normal
  seventh-chord search.
- **[OQ]** Whether a requested-but-unreachable inversion (e.g. `--inversion 3` on a
  triad, which has no 7th to invert to) should be a hard validation error (matches
  1.4's slash-bass conflict handling) or an empty result set with a warning (matches
  the existing slash-bass-unreachable pattern in `VoicingsCommand.Run`, which emits a
  warning rather than erroring). **Recommend the warning path**, for consistency with
  that existing precedent — but the ordinal-out-of-range case (`--inversion 3` on a
  triad, which will never have a 3rd tone in `NaturalToneOrder`) should still be a
  request-validation error, since it's a caller mistake rather than "no voicing found
  it," whereas the reachable-but-not-on-this-tuning case should warn.

---

## 2. Open-position ("cowboy chord") comfort & resonance modeling (D59, extends D54)

### 2.1 What D54 already covers

`Voicing.ComputeComfort` (v0.5.3, D54) is a deduction-only model starting at `1.0`:
span, interior/isolated mutes, barre cost, lowest-fret, plus the four biomechanics
rules (hand-angle, tendon-interdependence, arch, abduction). Auditing it against each
criterion in `hand-model-prompt.md`:

| Criterion | Status |
| --- | --- |
| Neutral wrist angle, frets 1–3 | **Partially covered.** `- 0.05 * (lowestFret/maxFret)` rewards low frets, but linearly and weakly, and it only looks at the *lowest fretted* note — it does not recognize "everything in this shape sits in the neutral zone" as its own thing. |
| Ulnar deviation / tendon strain, high-fret barres | **Covered but under-weighted.** `BarrePenalty`'s `high` term already scales barre cost up past `EasyBarreFret` (5). `HighBarreSurcharge` (0.05) is small relative to `NutBarreSurcharge` (0.18) — the model currently worries far more about barring at the nut than barring in an awkward high position, which is backwards for sustained ulnar deviation. |
| "Zero-resistance" nut clamping | **Implicitly true, not rewarded.** An open string is simply absent from `Fingering.Assignments` — it already costs nothing. But "costs nothing" and "is credited for the isometric effort a fretted equivalent would have cost" are different things, and the prompt is asking for the latter. |
| Open-string sustain/energy | **Not covered.** `Comfort` has no acoustic dimension at all. |
| Sympathetic resonance / harmonic clarity | **Not covered, and not fully representable.** See 2.5. |

### 2.2 Comfort adjustments (biomechanics — extends D54, same function)

Three changes to `ComputeComfort`/`BarrePenalty`, all deductions or bonuses on the
existing `Comfort` scale — these stay biomechanical, so they belong in the existing
function rather than a new one:

```csharp
// Voicing.cs — new constants alongside the existing D54 ones
/// <summary>Flat bonus when every fretted note sits in the neutral-wrist zone (frets 1-3).</summary>
private const double NeutralZoneBonus = 0.05;

/// <summary>Bonus per open string, crediting the nut's clamping force against isometric index-finger strain.</summary>
private const double OpenStringBonusPerString = 0.015;

/// <summary>Cap on the total open-string bonus — reward the advantage without letting an all-open chord dominate comparisons against genuinely harder shapes elsewhere in a result set.</summary>
private const double OpenStringBonusCap = 0.06;
```

1. **Neutral-zone bonus.** In `ComputeComfort`, when `highestFret > 0 && highestFret <=
   3`, add `NeutralZoneBonus` outright, rather than relying solely on the existing
   linear `lowestFret` term. This is what makes a first-position `E`, `A`, or `C` shape
   score distinctly better than the linear span/fret terms alone would credit — the
   claim in the prompt is specifically that this zone is *qualitatively* different
   (straight wrist), not just quantitatively lower on a continuous scale.
2. **Open-string bonus.** Add `Math.Min(OpenStringBonusPerString * OpenStrings,
   OpenStringBonusCap)`. This is the explicit form of "the nut absorbs the clamping
   force" — today that advantage only shows up as an *absence* of penalty; this makes
   it a present, comparison-visible reward, which is the actual ask in the prompt
   ("verify comfort scoring **rewards**...", not "verify it doesn't penalize").
3. **Rebalance the barre-height surcharge.** `HighBarreSurcharge` (0.05) is smaller
   than the nut surcharge for a reason that no longer fully holds once ulnar deviation
   is taken seriously as its own failure mode distinct from nut-tightness. Recommend
   raising it to **0.10** and, more importantly, scaling it by span the same way
   `WideBarreSurcharge` scales by width — a high partial barre is routine, a high
   *wide* barre under sustained flexor tension is the case the prompt is actually
   flagging:
   ```csharp
   var high = HighBarreSurcharge * Math.Clamp(
       (barre.Fret - EasyBarreFret) / (double)Math.Max(1, maxFret - EasyBarreFret), 0.0, 1.0)
       * (1.0 + 0.5 * Math.Clamp((width - 2) / 4.0, 0.0, 1.0));
   ```

All three are additive/small and clamp-compatible with the existing `Math.Clamp(raw,
0.0, 1.0)` at the end of `ComputeComfort` — no change to that clamp or to the function
signature (bonuses just make `raw` larger before the same clamp applies).

### 2.3 A new, separate score: `Resonance` — don't fold acoustics into `Comfort`

The string-physics/acoustics half of the prompt is a **different axis** than comfort:
`Comfort` answers "can my hand do this comfortably," and sustain/resonance answers
"does this sound good." Conflating them would mean two chords with identical fretting
difficulty — one all barre-chord clones with heavy string damping, one open and
ringing — get the same score for reasons that have nothing to do with each other,
which makes `Comfort` less legible for *both* purposes.

`Voicing` already has exactly this separation as precedent: `Structure` (what shape
this is) and `Functions` (what role it plays) are deliberately independent fields,
with the doc comment noting `Functions` is "empty in v1; v2 will populate" rather than
being crammed into `Structure`. `Resonance` should follow the same pattern — a new,
independent, derived property, not more terms inside `ComputeComfort`:

```csharp
// Voicing.cs
/// <summary>
/// Acoustic-quality score (D59), independent of Comfort: rewards open-string
/// sustain and wide bass-to-treble spread. Distinct from Comfort — a voicing
/// can be physically awkward and acoustically rich, or easy and thin, and the
/// two facts should stay separately visible rather than merged into one
/// number neither describes well.
/// </summary>
public double Resonance
{
    get
    {
        var stringCount = Positions.Length;
        if (stringCount == 0) { return 0.0; }

        var openRatio = (double)OpenStrings / stringCount;
        var openTerm = OpenStringSustainWeight * openRatio;

        var spreadTerm = 0.0;
        if (BassNote is { } bass && TopNote is { } top && top.Midi > bass.Midi)
        {
            var semitoneSpread = top.Midi - bass.Midi;
            spreadTerm = SpreadWeight * Math.Clamp((semitoneSpread - 12) / 24.0, 0.0, 1.0);
        }

        return Math.Clamp(openTerm + spreadTerm, 0.0, 1.0);
    }
}

private const double OpenStringSustainWeight = 0.6;
private const double SpreadWeight = 0.4;
```

Both terms are pure functions of `Positions` — already stored — so like `Inversion`,
this needs **no constructor change and no `VoicingSearch` change**; it's a derived
property from day one.

### 2.4 Data model / API / UI changes for `Resonance`

| Layer | Change |
| --- | --- |
| `Mappings.cs` `ToDto` | `VoicingDto` gains `Resonance` (`double`, rounded to 4 places, matching how `Comfort` is mapped). |
| `schemas/voicings.schema.json` | `$defs.voicing` gains required `"resonance": {"type": "number", "minimum": 0, "maximum": 1}`. |
| `Dadabe.Cli/Commands/VoicingsCommand.cs` | Follows the existing `minComfort` pattern exactly: a `minResonance` post-search filter parameter (not a `SearchParams` field — like `minComfort`, it's applied after the memoized search, so it doesn't churn the memo key). |
| `Models.cs` | `VoicingRow`/`VoicingFragmentRow` gain `ResonancePct` (`int`), mirroring `ComfortPct`. |
| `VoicingOptionsFields.cshtml` | New `wa-input` "Min resonance %" next to the existing "Min comfort %" field. |
| `VoicingsResult.cshtml` / `VoicingFragment.cshtml` | New `Resonance` column next to `Comfort`. |

### 2.5 Open questions / future work

- **[OQ] Sympathetic resonance from unstruck strings is not representable today.**
  `FretPosition` is binary — `Muted` or sounding a fret/open — there is no third state
  for "not part of the chord, not deliberately deadened, left free to ring
  sympathetically." A closed-voicing barre chord high on the neck with the low strings
  simply not struck (not muted, not sounded) is exactly the case real guitarists mean
  by sympathetic resonance, and the current model has no slot to represent it — every
  string is accounted for as one or the other. `Resonance` above approximates the
  effect via `OpenStrings` ratio and pitch spread rather than modeling true
  sympathetic ringing, which would need a `FretPosition` third state (`Free`?) plus
  reachability changes to populate it, and is out of scope for this release. Flagged
  the same way `Functions` was in D24/E8 — a real gap, deliberately deferred rather
  than silently ignored.
- **[OQ]** Exact constants above (`NeutralZoneBonus`, `OpenStringBonusPerString`,
  weights in `Resonance`) are first-pass estimates in the same spirit as D54's
  original constants — they should be tuned against a corpus of known-good open
  voicings (E, A, C, D, G major/minor in standard tuning) the way D54's four rules
  were validated, not treated as final.

---

## 3. Step-by-step implementation plan

1. **`ChordSpec.NaturalToneOrder`** (§1.3) — add the property; unit tests over a
   triad, a seventh chord, and a chord with a foreign slash bass (confirm the foreign
   tone is excluded).
2. **`SearchParams.RequireInversion`** (§1.4) — add the field, default `null`, extend
   `ContentHash`; update `SearchParams.Default`.
3. **`VoicingSearch` inversion constraint** (§1.5) — add `LowestSoundingFunctionIs` and
   the `TryEmit` check; validate `RequireInversion < NaturalToneOrder.Length` and the
   mutual-exclusion with `spec.Bass` at the call site (`VoicingsCommand.Run` /
   `VoiceLeadCommand`), matching where `spec.Bass`-unreachable warnings already live.
4. **`Voicing.Inversion` / `InversionLabel`** (§1.6) — derived properties, no
   constructor change. Unit tests: root position, each inversion of a seventh chord,
   an all-muted voicing (`Inversion` is `null`), a slash-bass voicing (label reads
   `"slash (D)"` style).
5. **`Voicing.Resonance`** (§2.3) — derived property; unit tests on a fully-open chord
   (high), a closed barre-chord voicing (low), and a wide-spread rootless shell.
6. **`Voicing.ComputeComfort` adjustments** (§2.2) — neutral-zone bonus, open-string
   bonus, rebalanced `HighBarreSurcharge`; update/extend existing D54 comfort unit
   tests, since the rebalance changes expected values for existing barre-chord fixtures
   — re-run and re-baseline `tests/Dadabe.Fretboard.Tests` before merging.
7. **CLI**: `--inversion` option on `voicings` and `voice-lead` (`Program.cs`); wire to
   `SearchParams.RequireInversion`. `minResonance` option on `voicings`, applied as a
   post-search filter in `VoicingsCommand.Run` next to `minComfort`.
8. **`Mappings.cs`**: `VoicingDto` gains `Inversion`, `InversionLabel`, `Resonance`.
9. **`schemas/voicings.schema.json`**: add the four new required properties
   (`inversion`, `inversionLabel`, `resonance` — `minResonance`/`inversion` request
   params are `input`, not `data`, and don't need a schema change since `input` is
   typed as an open `object`). Run schema validation (`--validate-schema`) against a
   sample voicings response before merging.
10. **Editor**: `VoicingRoutes.cs` parses `inversion`/`minResonance` from the form;
    `SongHandResolver`/`SongService` are untouched (both are per-song *hand-model*
    overrides, not per-search params — inversion and resonance are request-scoped,
    not song-scoped).
11. **Editor UI**: `VoicingOptionsFields.cshtml` gets the inversion `wa-select` and
    "Min resonance %" input; `Models.cs` row types gain the new fields;
    `VoicingsResult.cshtml`/`VoicingFragment.cshtml` get the two new table columns.
12. **Manual verification**: run the Editor, search a seventh chord with each
    `--inversion`/dropdown value and confirm the returned voicings' `InversionLabel`
    matches what was requested; search a first-position open chord (`E`, `A`, `C`,
    `D`, `G`) and confirm `Resonance` and the neutral-zone `Comfort` bonus both read
    high relative to an equivalent barre voicing of the same chord further up the neck.
13. **Docs**: fold this document's decisions into `docs/chord-grammar.md` (a short
    cross-reference note, the way D57 references the strict/loose split) and close out
    `inversions-prompt.md`/`hand-model-prompt.md` as consumed once both ship.
