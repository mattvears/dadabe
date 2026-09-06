# Dadabe v0.6 — Key-Aware Transforms

## Overview

**Shipped.** v0.6 holds §1–§3 of what was originally scoped for this release from
`docs/v0.5.2/ui-song-mode.md` and `docs/v0.5.2/progression-transforms.md`: the strictness
policy, the out-of-key chord policy, and the key-aware transform family
(`diatonic-transpose`, `parallel-mode`, `substitute`, `negative-harmony`). Everything else
originally scoped here — song arrangement/form, length-changing transforms, retune/capo,
slot-inline search, and the rest — moved to **`docs/v0.6.2/design.md`** unchanged, simply
renumbered from this document's old §4–§16 to that document's §1–§13. (`docs/v0.6.1/` is a
separate, unrelated set of bug notes and prompt scratch files, not a design continuation.)

Each item below is here for one of four reasons, and every entry names which:

- **[OQ]** — blocked on an open question that has to be answered before design, not during
  implementation. (All resolved as of this release.)
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
| `loose` | Approximate (nearest diatonic, triad-reduce and reassign quality) and flag every approximation |

A single setting on the request, surfaced in the transform panel. The alternative — a
per-transform policy — was rejected because users cannot predict it and because it
multiplies the test matrix by the size of the catalogue.

**Resolved: defaults to `loose`.** Approximation is what most users reaching for a
key-aware transform actually want; `strict` remains available but is not the default,
which reverses the leaning stated earlier in this section.

**This blocked §3 and §4; both are now unblocked on this decision.**

### Extensions and alterations under strict/loose

The `strict`/`loose` split only ever concerns the *triad quality itself* — see
[Extensions vs. alterations, in `ChordSymbol`](../chord-grammar.md#extensions-vs-alterations-in-chordsymbol)
for the underlying distinction. Two cases, handled differently by construction:

- **`ChordSymbol.Extensions`/`.Alterations`** (`add9`, `add11`, `add13`, `b5`, `#5`, `b9`,
  `#9`, `#11`, `b13`, `sus4`) **always survive untouched**, in both modes. A chord like
  `Cadd9` transposes cleanly with no note at all — its `Quality` (`""`) is already a plain
  triad, so it's outside the strict/loose branch entirely; `add9` just rides along on the
  `with { Root = ... }`/`with { Quality = ... }` copy.
- **A seventh or extension baked into `Quality` itself** (`maj7`, `m7`, `9`, `11`, `13`,
  `dim7`, `m7b5`, `mMaj7`, `6`) is what strict/loose actually governs. `strict` skips the
  chord and reports it; `loose` does **not** reconstruct an equivalent seventh on the new
  triad — despite "re-extend" suggesting otherwise, the implementation (`ChordApproximation.
  TryApplyTriadQuality`) triad-reduces and reassigns the new bare triad quality outright
  (`Cmaj7` → `Cm`, not `Cm7` or `CmMaj7`), flagged with an `"approximated"` note so the
  simplification is visible rather than silent.

Put concretely: `E13sus4add9`'s `add9` extension is preserved by every key-aware transform
regardless of strictness, but the `13`-ness of its `Quality` is the part that gets
triad-reduced away in `loose` mode (or skipped in `strict`).

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

---

## Continued in v0.6.2

Everything past this point — length-changing transforms, song arrangement/form, loop
closure, transposition/capo, retune flow, slot-inline search, the songwriting loop,
analysis read-outs, saved transform chains, transform-this-section, and setlists — moved to
**`docs/v0.6.2/design.md`** §1–§13, unchanged apart from renumbering and updated
cross-references to this document's now-shipped §1–§3.
