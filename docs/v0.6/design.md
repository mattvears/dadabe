# Dadabe v0.6 — Key-Aware Transforms + Song Arrangement

## Overview

v0.6 holds everything from `docs/v0.5.2/ui-song-mode.md` and
`docs/v0.5.2/progression-transforms.md` that v0.5.2 deliberately did not ship. Each item
is here for one of four reasons, and every entry below names which:

- **[OQ]** — blocked on an open question that has to be answered before design, not during
  implementation.
- **[KEY]** — depends on `KeyInference` succeeding. v0.5.2's guiding rule was that nothing
  branches on inferred key; this release relaxes that, and pays for it with a degradation
  path everywhere.
- **[DEP]** — depends on v0.5.2 shipping first, or on another item in this release.
- **[EXP]** — genuinely expensive: a new solver mode, a new entity, or a cross-cutting
  display change.

The two anchor decisions for this release are §1 and §2 — both now resolved. Neither is
large to implement; both were large to *decide*, which is exactly why they were not in
v0.5.2.

Decisions continue from D53. New decisions here are numbered D54+.

---

## 1. Strictness policy (D54) — Resolved: `loose` by default

*Transforms open question 3.*

Every key-aware transform has cases it cannot handle cleanly: diatonic transposition of a
borrowed chord, P/L/R on a seventh, parallel mode on a sus chord. Each has a defensible
"skip it and report" and a defensible "approximate it and flag".

v0.5.2 hardcoded strict skip-and-report per transform (D53), which is the safe default and
was enough for eleven key-blind transforms. It does not scale to the key-aware family,
where approximation is often what the user actually wants.

**Decide once, globally**, rather than accreting a policy per transform:

| Mode | Behaviour |
| --- | --- |
| `strict` | Skip anything the transform cannot express exactly; report every skip |
| `loose` | Approximate (nearest diatonic, triad-reduce then re-extend) and flag every approximation |

A single setting on the request, surfaced in the transform panel. The alternative — a
per-transform policy — was rejected because users cannot predict it and because it
multiplies the test matrix by the size of the catalogue.

**Resolved: defaults to `loose`.** Approximation is what most users reaching for a
key-aware transform actually want; `strict` remains available but is not the default,
which reverses the leaning stated earlier in this section.

**This blocked §3 and §4; both are now unblocked on this decision.**

## 2. Out-of-key chord policy (D46) — [KEY] — Resolved

Every key-aware transform must answer: what happens to a chord that is not in the key?

**Resolved: chromatic-transpose, leave quality alone** is the default, on the grounds that
a borrowed chord was usually a deliberate choice and its colour should survive. This must
be visible in the diff rather than silent — a policy the user cannot see produces results
they cannot explain.

Also required here: the **degradation path**. `KeyInference.InferKey` returns null below
50% coverage, and for a modal or chromatic progression that is the correct answer. Every
transform in §3 must then say *"this transform needs a key; set one here"* rather than
falling back to C major. That is a UI affordance (a key override control on the transform
panel) plus a uniform error shape, and it is shared by everything in §3–§4.

---

## 3. Key-aware transform family (D47, D49) — [KEY, DEP on §1, §2]

| `type` | Effect | Needs |
| --- | --- | --- |
| `diatonic-transpose` | Move by scale degrees, not semitones | key + scale |
| `parallel-mode` | Rebuild each chord on its degree in a new mode | key + `ModeModel` |
| `substitute` | Replace within the same functional group | key + function map |
| `negative-harmony` | Invert about the tonic/dominant axis | key |

**`diatonic-transpose`** preserves the *degree pattern*, not the quality — in C major,
`[C, F, G]` up one degree is `[Dm, G, Am]`. The quality changes are the whole point, and
the source spec does not say so; the design should.

**`parallel-mode`** is the one worth the most. It is key-aware in a way `quality-map`
(shipped in v0.5.2) is not, and produces a genuinely different result:

```
[C, F, G] in C major
  → C mixolydian   [C,  F,  Gm]      only ♭7 changes, so only V moves
  → C dorian       [Cm, F,  Gm]
  → C aeolian      [Cm, Fm, Gm]
  → C lydian       [C,  F#dim, G]    ♯4 wrecks IV, which is the sound of lydian
```

The mixolydian and lydian rows are the regression tests: exactly one chord moves in each.
Selective borrowing (`[C, F, G] → [Cm, F, G]`) falls out as `parallel-mode` restricted to a
chosen subset of positions — a `positions` parameter, not its own transform.

Needs `ModeModel.Intervals` from the reference catalog, which means it also inherits
whatever gaps that catalog has. Audit the shipped modes before building.

**`substitute`** — *transforms open question 1.* The source spec gives a table
(`C→Am, F→Dm, G→Bdim`) that only holds for C major. The general rule is *substitute within
the same functional group, sharing at least two common tones*: tonic {I, iii, vi},
subdominant {ii, IV}, dominant {V, vii°}. The rule generalises to any key and mode from
`KeyInference` alone and does not have to be rewritten per key.

**Recommendation: build the rule, not the table.** It is barely more work and the table
approach needs 24 tables. (`tritone-sub` already shipped in v0.5.2 as a key-blind,
quality-gated transform and stays separate — it is dominant-only and does not need this
machinery.)

**`negative-harmony`** is the preset of v0.5.2's key-blind `invert` that users actually
search for: invert about the axis midway between tonic and dominant, mapping `C → Cm`,
`F → Gm`, `G → Fm` in C major. The characteristic result is V becoming a minor iv that
still resolves.

Implement the pragmatic version — invert roots, flip major↔minor — and **say in the UI that
is what it is**. Full tone-inversion produces pitch-class sets with no symbol in the
grammar (v0.5.2 §1), so the honest version reproduces the textbook examples and stays
emittable. Axis stays a parameter, defaulting to the inferred key's tonic/dominant axis.

## 4. Length-changing transforms (D52) — [KEY, DEP on §1, §2]

Every transform in v0.5.2 preserves chord count. The two most common reharmonisations a
working musician performs do not, which is the biggest single gap in the source taxonomy.

**Insertion** — the progression gets longer:

| `type` | Effect |
| --- | --- |
| `secondary-dominants` | Insert V7-of-x before chord x: `[C, F, G] → [C, C7, F, D7, G]` |
| `ii-v-insert` | Expand a target into ii7–V7–target: `[C, …, F] → [C, Gm7, C7, F]` |
| `passing-dim` | Between roots a whole step apart: `[C, Dm] → [C, C#dim7, Dm]` |

`secondary-dominants` is the most common reharmonisation in popular music, and
`KeyInference.IsValidNonDiatonic` already contains the recognition rule — a dom7 resolving
by fifth into a diatonic chord. It validates them today, so generating them is that logic
run backwards.

**Reduction** — the inverse direction, and for a guitar app at least as valuable:

| `type` | Effect |
| --- | --- |
| `collapse-ii-v` | ii–V → V |
| `strip-passing` | Remove passing and secondary chords |

(`reduce`, which strips extensions toward triads, already shipped in v0.5.2 — it is
grammar-mechanical and needs no key.)

### What length-changing breaks — [DEP]

This is why it is not simply "more transforms":

- **The diff preview** (v0.5.2 §7) aligns original and result by index. Length changes
  break that; it needs an alignment view showing insertions and deletions.
- **Song slots** carry `bars`. Inserting a chord has to split a slot's bars or push the
  section longer, and neither is obviously right. **Resolved: split the target slot's
  existing bars** across the inserted chord(s) — section length stays fixed, which keeps
  form (§5) and loop closure (§6) math stable.
- **Voice-lead fill** re-runs over a different chord count, so pinned slots have to be
  re-anchored by identity rather than by index.

Ship §3 before §4.

---

## 5. Song arrangement / form (D40) — [DEP] — Resolved

*Song-mode open question 2, answered: not v0.5.2.*

Sections as shipped are definitions. How a song actually goes is a sequence of references
with repeats:

```
form: [Intro, Verse, Chorus, Verse, Chorus, Bridge, Chorus, Chorus, Outro]
```

Reusing a section rather than duplicating its chords is how charts are written. v0.5.2's
chart iterates sections in definition order and was built so that adding a form list
changes only that iteration.

**Resolved: a flat list of section references, each repeat as its own entry** — no `×2`
count syntax. Simplest to implement against v0.5.2's definition-order chart iteration;
rendering "Chorus ×2" is a display-time compression of adjacent identical entries, not a
data-model concern.

What form unlocks, all of which is blocked on it:

- **Seam analysis.** The transition that gets fumbled is rarely inside the verse — it is
  the last chord of the verse into the first chord of the chorus. With a form list those
  seams are enumerable and each one is a `BuildTransition` call. Surface them as their own
  list.
- **Correct charts.** "Chorus ×2" cannot be expressed without it.
- **Whole-song voice leading**, as opposed to per-section.

## 6. Loop closure (D41) — [EXP, DEP on §5]

A repeated section's last chord back to its own first chord is a transition. A solve that
ignores it will produce a verse ending at fret 9 that restarts at fret 1.

Add the wrap edge to the cost function as an opt-in "closes on repeat" solve. This is a
genuine change to `VoiceLeadSolver` rather than a change to what is fed into it — the
layered DAG becomes cyclic, so the k-shortest-paths implementation needs revisiting. That
is what makes it [EXP] where v0.5.2's fill was free.

It is also exactly what a turnaround is, so it is worth doing properly.

## 7. Song transposition and capo (§5.7) — [DEP on v0.5.2 §1–§2]

Two distinct operations that users will conflate, and that must not be conflated in the UI.

- **Transpose** — shift every slot's symbol and re-solve. Uses `ChordSymbol.ToSymbol` and
  `Note.Transpose` from v0.5.2, so the hard part is already built. What is new is the
  consequence: **every pinned voicing is invalidated by definition.** That needs the
  retune/refill flow in §8.
- **Capo** — the pitches move, the shapes do not. A `Tuning` is a list of open pitches, so
  a capo at fret *n* is a tuning transform and everything downstream works unchanged. The
  cost is display: fret numbers become capo-relative across the voicings table, diagrams,
  position map, and chart, and both written and sounding key have to be shown. That
  cross-cutting display change is why it is [EXP] rather than trivial.

Transpose is *"re-learn it in a new key"*; capo is *"play the same shapes higher"*. Anyone
who plays for a singer will be annoyed if these share a control.

## 8. Retune flow (D55) — [DEP] — Resolved

*Song-mode open question 3.*

v0.5.2 locks a song's tuning after creation because changing it silently invalidates every
pinned shape. **Resolved:** an explicit **"retune this song"** action that invalidates all
pins, keeps every chord symbol, and offers the §5.2 fill to repin everything at once.

Shared by §7's transpose, which has the same invalidate-and-refill shape. Build it once.

## 9. Slot-inline voicing search (D56) — Resolved

*Song-mode open question 1.*

v0.5.2 ships pinning from the voicings tab, as the source spec describes. The better
workflow is the inverse: click an unpinned slot and search from there, already scoped to
the song's tuning and `SongHand`, with the slot's neighbours known so candidates rank by
motion cost as well as comfort.

It is a better workflow and a bigger build, and it changes where voicing search *lives* in
the app rather than adding to it. **Resolved: it coexists with the voicings tab** — the tab
stays for general lookup/comparison, and slot-inline search is a second, scoped entry point
for in-song editing.

Subsumes the **alternatives drawer** (§5.3): candidates for a slot, ranked by comfort *and*
motion cost to actual neighbours, swappable in place with the section re-scoring live. The
same chord is good or bad depending on what surrounds it, and that ranking is the
difference between a chord dictionary and an arranging tool.

Also completes v0.5.2 §10's practice targets: *"F#m7b5 at 38% — three alternatives above
70% within 2 frets of its neighbours"* needs this drill-down to be actionable rather than
merely informative.

## 10. Songwriting loop (§5.8) — [DEP on §3]

Predict → accept → pin, in a loop:

1. Section context so far → `NextChordPredictor.Predict`, weighted by the song's key.
2. **Filter candidates to what is playable** in the song's tuning above the comfort floor.
   A prediction you cannot finger in DADABE is noise, and the search to check it is the
   search the app already runs.
3. Accept → append a slot *and* pin the voicing that voice-leads best from the current last
   chord.
4. Predict again.

Show `KeyInference.Classify`'s diatonic / valid-non-diatonic / unrelated badge on each
candidate, so a user offered a borrowed chord can see that is what happened.

Depends on §3 only for the key-override path — prediction already accepts context, so the
playability filter and auto-pin are the new work.

## 11. Analysis read-outs — [KEY]

- **Scale and mode suggestion.** Given the inferred key and the reference catalogs, list
  the scales whose pitch-class set covers a section's chord tones — "Verse: A dorian, A
  natural minor". The most-asked question about any song a guitarist is learning, and both
  halves are on disk. **Resolved: full subset match** — a scale is suggested only if every
  chord tone in the section is contained in its pitch-class set. Stricter and fewer false
  positives, at the cost of one passing chromatic tone excluding an otherwise-obvious
  scale. Still needs the catalog audit, which is the remaining design work.
- **Cadence detection.** Match each section's tail against `CadenceModel.ExampleProgression`
  transposed to the inferred key: "Chorus ends on a deceptive cadence." Also the
  before/after annotation for transforms — retrograde in particular, since `[C,F,G]`
  reversed is a perfectly good progression whose cadence has been destroyed.
- **Plausibility scoring.** Run `NextChordPredictor` across a transform result for a rough
  "does this sound like music" number, as a tiebreak among results that rank equally on
  playability. Most useful for `interval-multiply`, which can produce sequences no one
  would write.

## 12. Saved transform chains (D50, remainder) — [DEP on v0.5.2 §3]

v0.5.2 applies a chain within a request. This adds chains as first-class saved objects:

- **Named recipes** — "retrograde, up a tone, borrow the ♭VI" — saveable, re-runnable
  against a different progression, shareable.
- **Per-step preview**, so a surprising result is attributable to a step rather than to the
  whole chain.
- **Exact undo** for the invertible half of the catalogue, driven by the `IsInvertible`
  flag v0.5.2 already declares.

Straightforward once the engine exists; deferred only because it is additive and nothing
depends on it.

## 13. Transform this section — [DEP on §5, v0.5.2 §3]

Where the two source documents meet: the transform panel scoped to a song section. Transform
a verse into a bridge, keep it if it is playable, let the fill repin the voicings.

v0.5.2 reaches this indirectly — promote a section to a progression, transform it,
instantiate it back — which is enough to prove the workflow. The direct action needs
length-changing transforms (§4) settled first, because that is where slot `bars` handling
gets decided.

## 14. Setlists — [EXP]

Songs ordered into a set, with retune cost between them made visible: *"these four are in
DADABE, these three standard — here is an order with one retune."* Computable from song
tunings alone, and genuinely useful for anyone who gigs.

It is a second entity with its own tab, its own CRUD, and its own nav. Out of scope for a
point release; v0.6.

---

## 15. Still further out

Unchanged from `docs/future.md`:

- Tab, notation, and audio rendering.
- Polychord parsing (`C|G`) — the extension point is stubbed in `ChordParser.TryParse`.
- Voicing categories beyond the Core 5: quartal, cluster, polychord, upper-structure triad,
  altered dominant as a category.
- Inner-line generation inside static harmony; chord-melody harmonisation.
- Persistent memo backends beyond in-memory.
- Relaxing `HandModel.MinStrings` to allow true 2-note dyads — it changes output for every
  chord, so it is not a change to make in passing.

---

## 16. Suggested order

1. **§1 strictness policy** and **§2 out-of-key policy** — decisions, not code, and they
   block everything else.
2. **§3 key-aware transforms** — the largest user-visible win in this release.
3. **§5 song form** — unblocks §6 and §13.
4. **§8 retune flow**, then **§7 transposition and capo**, which reuses it.
5. **§9 slot-inline search** — decide the workflow question first; it changes where voicing
   search lives.
6. **§4 length-changing transforms** — after §3, and after §5 settles slot `bars`.
7. **§6 loop closure**, **§10 songwriting loop**, **§11 analysis**, **§12 saved chains** —
   independent of each other, order by appetite.
8. **§14 setlists** — v0.6.
