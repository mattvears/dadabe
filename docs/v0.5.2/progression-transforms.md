# Progression Transformation

Progressions in DADABE may be transformed with the following methodologies.

## Types of Transformations for Progressions

### Transposition

Move every chord by the same number of semitones.

### Diatonic transposition

Move the progression by a number of scale degrees rather than semitones.

### Parallel-mode transformation

Keep the roots but change the harmonic quality according to a different mode.

For example:

```
C major → C minor
F major → F minor
G major → G minor

[C, F, G] → [Cm, Fm, Gm]
```

Or selectively borrow chords:

```
[C, F, G] → [Cm, F, G]
```

### Chord-quality transformation

Keep the root and alter the chord type.

```
[C, F, G]
   ↓
[Cm, Fm, Gm]
```

Or:

```
[C, F, G]
   ↓
[Cmaj7, Fmaj7, G7]
```

You can think of this as transforming `root + quality` while leaving the root sequence intact.


### Chord substitution

Replace a chord with another chord that performs a similar harmonic function.

For example:
```
C → Am
F → Dm
G → Bdim
```
So:
```
[C, F, G]
→
[Am, Dm, G]
```
The progression has a different surface but much of the underlying harmonic function remains.

A particularly important example is tritone substitution:

`G7 → Db7`

because G7 and Db7 share the same tritone.

### Relative transformation

Switch between relative major/minor chords:

```
C ↔ Am
F ↔ Dm
G ↔ Em
```

So:

```
[C, F, G]
→
[Am, Dm, Em]
```

This is different from simply changing major → minor because the root moves as well.

### Inversion

If you're treating the progression as a sequence of pitch classes, you can perform a geometric "mirror" transformation.

For example, around C:
```
C → C
D → Bb
E → Ab
F → G
G → F
A → Eb
```
So the root progression:

`[C, F, G]`

could become:

`[C, G, F]`

depending on the particular inversion axis.

This becomes especially interesting if you're doing algorithmic composition.

### Retrograde

Reverse the progression: `[C, F, G] → [G, F, C]`

Simple, but surprisingly useful.

You can combine it with transposition:

```
[C, F, G]
→ retrograde
[G, F, C]
→ +2 semitones
[A, G, D]
```

### Rotation
Move the starting point while preserving the cycle:

```
[C, F, G]
→ [F, G, C]
→ [G, C, F]
```

This is useful when you're treating a progression as a repeating loop.

### Interval transformation

This is perhaps the most interesting if you're building a programmatic chord-progression transformer.

Instead of thinking: `C F G`

think in terms of intervals between roots:

```
C → F = +5
F → G = +2
```

So the progression is essentially:

```
start = C
intervals = [+5, +2]
```


You can transform those intervals:

`[+5, +2]`

into, say,

`[+5, -2]`

giving:

`C → F → Eb`

Or multiply the intervals by -1:

`[-5, -2]`

giving:

`C → G → F`

This opens up a huge number of transformations while preserving some notion of the original progression's shape.

---

# Build-out

The catalogue above is sound. What follows is the engine it needs, the musical
corrections it wants, the families it is missing, and the one integration that makes this
DADABE's transformer rather than a generic theory toy.

Decisions are numbered from D44 (D37–D43 are `ui-song-mode.md`).

## 0. What is already deployed

Better placed than it first appears:

- **`ITransformation`** — a stub interface in `Dadabe.Core` (`Id`, `DisplayName`,
  `Parameters` bag) with no implementations. The slot was cut for exactly this.
- **`Namespaces.Transform`** — a memo namespace reserved since v0.1 and still unused.
- **`Note` is spelled, not numeric** — `Letter` + `Accidental` (−2..+2), with
  `Note.LetterPlus` walking the stacked-thirds letter cycle (D12). This is the single most
  important asset in the file. Most chord tools store roots as pitch classes 0–11 and
  therefore *cannot* transpose correctly; they guess D♭ vs C♯ from a global preference and
  get it wrong half the time. DADABE already carries what correct spelling requires.
- **`KeyInference`** — 24-key inference with recency weighting, `DiatonicSet(rootPc,
  isMinor)`, and `Classify` returning Diatonic / ValidNonDiatonic / Unrelated. It already
  recognises tritone subs, chromatic mediants, borrowed chords, and secondary dominants —
  the same vocabulary the transformations produce. It can therefore *check the output*.
- **Reference catalogs** — `ModeModel` carries `intervals`, `parentScale`, `degreeIndex`,
  `noteNames`; `ScaleModel` carries `notes`, `intervals`, `modeOf`. Parallel-mode
  transformation needs precisely this and nothing more.
- **`CadenceModel`** — cadence patterns, for asking what a transform did to the ending.
- **`VoicingSearch` + `VoiceLeadSolver`** — §6, the payoff.
- **`ProgressionService`** — slugged JSON CRUD, so a transform result saves as a new
  progression with one call.

## 1. The missing primitive: symbol emission (D44)

Every transformation in this document is a function `List<string> → List<string>` over
chord symbols. The pipeline is therefore parse → transform → **emit**, and *emit does not
exist*. There is no `ToSymbol()` anywhere in `Dadabe.Core`.

Emission is close to free, because `ChordParser` already normalises: it stores the matched
form's `DisplayName` in `ChordSymbol.Quality` (`ChordParser.cs:181`), and `ChordExpander`
looks forms up by that same display name. So quality is already canonical — `maj7`, not
`M7`; `m`, not `-`. What is missing is assembling root + quality + `Extensions` +
`Alterations` + slash bass back into a string.

```csharp
// ChordSymbol
public string ToSymbol();   // "F#m7b5/C"
```

The correctness bar is a round-trip property, and it should be a property test over the
whole grammar cross-product (25 forms × 9 modifiers × 35 spellable roots), not a handful of
examples:

```
parse(format(parse(s))) == parse(s)          for every s the parser accepts
```

Build this first. Everything below is unusable without it, and it is the piece most likely
to be quietly wrong.

### Corollary: the vocabulary is closed

The grammar has 25 forms and 9 modifiers. A transformation that lands outside that set has
no symbol to emit. This is a hard boundary and it constrains the taxonomy:

- Root-motion transforms (transposition, retrograde, rotation, interval, root inversion)
  are always representable — they never touch quality.
- Quality transforms are representable if the target quality is in the grammar. "Make it a
  13♭9♯11" is fine; arbitrary pitch-class-set results are not.
- **True pitch-class-set inversion does not close over the vocabulary.** Inverting the
  *tones* of a chord gives a set that usually has no chord symbol. The spec's "Inversion"
  section sidesteps this by inverting only the roots — which is the right call, and worth
  stating outright rather than leaving as an accident of the example. §5 covers the case
  where users will want the other thing anyway.

## 2. Spelling policy — transpose by interval, not by semitone (D45)

`Note`'s accidental is clamped to ±2 and the constructor **throws** outside that range. So
transposition has two problems the spec does not mention, both of which will produce bugs
or exceptions in production.

**Problem 1 — which spelling?** "Up 1 semitone" from C is D♭ or C♯, and the answer depends
on the destination key, not on a global preference. Transposing `[C, F, G]` up 1 gives
`[Db, Gb, Ab]` (four flats, readable) or `[C#, F#, G#]` (nine sharps, unreadable) — and up
6, the same progression is much better spelled `[F#, B, C#]` than `[Gb, Cb, Db]`.

**The policy:** transpose by a *named interval* — a `(letterSteps, semitones)` pair — not
by a semitone count. `Note.LetterPlus(letter, letterSteps)` gives the new letter; the
accidental is whatever makes the semitone arithmetic come out. A minor 2nd is (1, 1), an
augmented unison is (0, 1); both move one semitone and they spell differently, correctly.
The UI can still offer "+1 semitone" and pick the interval from the resulting key
signature — fewest accidentals wins, ties to flats.

**Problem 2 — overflow.** Even with a correct policy, a chain of transforms can drive an
accidental past ±2 (transpose a progression already spelled in C♯ major up another
augmented interval and you reach for B♯♯). Do not throw. Respell enharmonically, keep
going, and **flag the chord in the result** — "F♭♭ respelled as E♭; this key is past what
standard notation handles comfortably" is honest and actionable, where an exception from
deep inside a transform chain is neither.

## 3. Parallel-mode and chord-quality are two different operations (D46, D47)

The spec gives both sections the same example (`[C, F, G] → [Cm, Fm, Gm]`), which hides
that they are not the same operation at all.

**Chord-quality transformation** is blanket and key-blind: map every quality to a target,
or apply a rule ("add a 7th to everything", "strip to triads"). `[C, F, G] → [Cm, Fm, Gm]`
regardless of what key you are in. Crude, and sometimes exactly what you want.

**Parallel-mode transformation** is key-aware: work out each chord's *degree* in the
current key, then rebuild it as the chord on that degree in the new mode. The chords that
change are only the ones whose tones the mode actually alters:

```
[C, F, G] in C major
  → C mixolydian   [C,  F,  Gm]     only ♭7 changes, so only V moves
  → C dorian       [Cm, F,  Gm]     ♭3 and ♭7
  → C aeolian      [Cm, Fm, Gm]     ♭3, ♭6, ♭7
  → C lydian       [C,  F#dim, G]   ♯4 wrecks IV, which is the sound of lydian
```

That is a genuinely different — and far more musical — result than "make everything
minor", and it is the thing a musician means by "put it in mixolydian". It needs the key
(`KeyInference`), the degree, and the target mode's intervals (`ModeModel.Intervals`). All
deployed. The mixolydian and lydian rows above are also good regression tests: exactly one
chord should move in each.

Selective borrowing (`[C, F, G] → [Cm, F, G]`) then falls out as parallel-mode applied to a
chosen subset of positions, which is the right way to model it rather than as its own
transform.

### Out-of-key chords (D46)

Every key-aware transform — diatonic transposition, parallel mode, functional substitution,
relative — has to answer: *what happens to a chord that is not in the key?* The available
policies are chromatic-transpose it and leave it alone (preserves the outsider's colour),
snap it to the nearest diatonic chord (loses the colour), or leave it untouched entirely
(safest, and usually right for a borrowed chord that was a deliberate choice). **Pick
"chromatic-transpose, leave quality alone" as the default and make it visible in the diff**
— a silent policy here produces results the user cannot explain.

Also note `KeyInference.InferKey` returns null below 50% coverage. Every key-aware
transform must degrade honestly when the key is unknown, not fall back to C major. For a
modal or chromatic progression the right answer is "these transforms need a key; here is
where to set one manually", not a confidently wrong result.

### Chords with no third (D53)

`C5` and the `sus` forms have no third; `dim`/`aug` have no perfect fifth. Quality and
parallel-mode transforms must **skip** them rather than mangle them — "make it minor" on a
power chord is a no-op, and on `Csus4` it is a category error. The grammar makes this
checkable without special-casing: a form's `required` list says what it is made of. Slash
chords need a decision too (`ChordSpec.Bass` is deployed and the parser accepts them):
transposition moves the bass with the root; quality changes keep it; substitution should
drop it, because the bass was chosen for the chord being replaced.

## 4. Relative transformation is one of three (D48)

The spec's relative transformation is **R** of the neo-Riemannian **P / L / R** group, and
the other two are worth having because they complete a family the spec is already half
inside:

| | operation | example | voice movement |
|---|---|---|---|
| **P** | parallel — same root, flip quality | C ↔ Cm | 3rd moves 1 semitone |
| **R** | relative — the spec's transform | C ↔ Am | 5th moves 2 semitones |
| **L** | leading-tone exchange | C ↔ Em | root moves 1 semitone |

Three properties make these worth implementing over an ad-hoc "relative" rule:

1. **Each moves exactly one voice, by one or two semitones.** Two of the three notes are
   held. That is maximally smooth voice leading *by construction* — so PLR output feeds
   `VoiceLeadSolver` and comes back with near-zero motion cost. The formalism and the
   fretboard engine agree with each other.
2. **They compose.** `LR` applied repeatedly walks the hexatonic cycle; `PR` walks
   relative-major/minor chains. Chaining two or three operations is where the interesting
   results are, and §7 makes chaining a first-class feature anyway.
3. **Each is its own inverse.** P(P(x)) = x. Free undo, and an obvious property test.

The constraint to state plainly: **PLR is defined on consonant triads only.** Seventh
chords need an extended definition and there is no single standard one. Apply to the
triad and re-attach the 7th if you like, but say in the UI that you are doing that.

## 5. Inversion, and the transform people actually want (D48)

The spec's inversion — mirror the root sequence about an axis — is correct and cheap.
The thing to add is that **negative harmony is a preset of it**, because that is the term
users will search for and the reason they came.

Negative harmony is inversion about the axis midway between the tonic and the dominant
(between E♭ and E in C major). It maps each chord to its "shadow": in C major,
`C → Cm`, `F → Gm`, `G → Fm`, `Am → Eb`. The characteristic result is that V becomes a
minor iv that still resolves — the same functional pull with the opposite colour.

Two honest caveats:

- **Applied to roots only, it is a root-motion transform** and the qualities come along
  unchanged, which is not what the theory describes. Full negative harmony inverts the
  chord *tones*, and the resulting sets frequently have no symbol in the grammar (§1). The
  pragmatic implementation — invert roots, flip major↔minor — reproduces the well-known
  textbook examples and stays inside the vocabulary. Implement that, and say that is what
  it is.
- Axis choice is a parameter, not a constant. Default to the tonic/dominant axis of the
  inferred key; expose the axis for the algorithmic-composition use the spec mentions.

## 6. The transformation that only DADABE can do (D51)

Everything above exists in a hundred theory apps. Here is what does not.

**A transformation is worthless if you cannot play the result.** Reharmonising `[C, F, G]`
into something with three altered dominants is a fine idea on paper and useless if it needs
four barres and a 6-fret stretch in DADABE tuning. The engine already deployed answers this
directly: for each transform result, run `VoicingSearch` on every chord and
`VoiceLeadSolver` across the sequence, and report:

- **worst comfort** — the hardest single chord, the thing that breaks in performance
- **total motion** — how far the hand travels over the progression
- **fret span** — where on the neck it lives, and whether it fits the position you are in
- **unplayable chords** — any chord with no voicing above the comfort floor, named

Then **rank the results by playability**. That turns a wall of twelve equally-plausible
theoretical variants into an ordered list: *here are the reharmonisations of your verse,
best-playable first, on your tuning, for your hands.* No other chord tool can produce that
ranking, because no other chord tool knows what your neck is tuned to.

It also inverts the workflow in a useful way. Rather than picking a transform and hoping,
the user runs the whole catalogue at once — "show me everything" — and reads off the
results that are both interesting and playable. Under an alternate tuning, which transforms
are comfortable is genuinely unpredictable, so this is discovery, not just filtering.

Two cheaper checks belong alongside it:

- **Key and cadence, before and after.** Re-run `KeyInference` on the output and re-detect
  the cadence. "Was C major, now C mixolydian" and "the perfect cadence became a plagal
  one" are one-line summaries of what a transform actually did. This matters most for
  retrograde: `[C, F, G]` reversed is `[G, F, C]`, which is a perfectly good progression
  whose cadence has been destroyed — it now *ends* resolved having started on the dominant.
  The spec calls retrograde "simple, but surprisingly useful"; it is more useful still when
  the app tells you what happened to the ending.
- **Plausibility.** `NextChordPredictor` scores chord-to-chord transitions. Running it
  across a transform result gives a rough "does this sound like music" number, which is a
  reasonable tiebreak among results that are equally playable — particularly for the
  interval transforms in §8, which can produce sequences no one would write.

## 7. Transforms compose; make the chain the object (D50)

The spec already composes retrograde with transposition. Take that seriously and the design
falls out of the existing stub:

```csharp
public sealed record TransformStep(string Id, IReadOnlyDictionary<string, object> Parameters);
// e.g. ("retrograde", {}) → ("transpose", {interval: "M2"}) → ("parallel-mode", {mode: "mixolydian"})
```

`ITransformation` is already `Id` + `DisplayName` + `Parameters` — a chain is a list of
those, and `Namespaces.Transform` is the reserved memo namespace for hashing one. The stub
was designed for this; activating it is the natural v0.5.2 move.

What the chain buys:

- **Named recipes.** "Retrograde, then up a tone, then borrow the ♭VI" is saveable,
  re-runnable against a different progression, and shareable.
- **Preview at each step**, so a surprising result is attributable to a step rather than to
  the whole chain.
- **Provenance.** A progression saved from a transform records the source slug and the
  chain. Six months later, "where did this come from?" has an answer, and re-running the
  chain after editing the source is one click. `ProgressionModel` gains one optional field.
- **Exact undo for the invertible ones.** Transposition (negate the interval), retrograde,
  rotation, inversion, and P/L/R are all invertible; quality and substitution transforms
  are lossy. Mark each transform with which it is — it drives undo, and
  `inverse(transform(x)) == x` is the property test for half the catalogue.

## 8. Interval transformation, constrained (D49 in part)

The spec is right that this is the most generative family, and right that it needs
constraining to stay musical. The variants worth shipping, in descending order of how often
they produce something usable:

- **Negate** — `[+5, +2] → [-5, -2]`. Root-motion inversion. Usually musical, because
  descending fifths and ascending fourths are both idiomatic.
- **Diatonic re-anchor** — preserve the interval *contour* (up, down, by how many scale
  steps) but re-derive each root from the key's scale. Keeps the shape, guarantees you stay
  in key. This is the one that will produce usable results most consistently, and it is
  really diatonic transposition applied per-interval.
- **Reverse the interval list** — distinct from retrograde: retrograde reverses the chords,
  this reverses the *motion* and re-derives roots from the original start.
- **Multiply mod 12** — the serial M5/M7 operations. `×7` maps chromatic steps onto the
  circle of fifths, so `[+5, +2] → [-1, +2]`. Deeply chromatic, rarely idiomatic, and
  exactly the "algorithmic composition" case the spec asks for. Ship it labelled as such.

All four are root-motion only, so all four are always emittable (§1). The playability
ranking in §6 is what makes an unconstrained generator tolerable — generate widely, sort by
what you can actually play.

## 9. The family the taxonomy is missing entirely (D52)

Every transformation in the spec preserves the chord count. The two most common
reharmonisations a working musician performs do not, and their absence is the biggest gap
in the catalogue.

**Insertion** — the progression gets longer:

- **Secondary dominants.** Insert V7-of-x before chord x: `[C, F, G] → [C, C7, F, D7, G]`.
  This is *the* most common reharmonisation in popular music, and
  `KeyInference.IsValidNonDiatonic` already contains the recognition rule (a dom7 resolving
  by fifth into a diatonic chord) — it validates them today, so generating them is the same
  logic run backwards.
- **ii–V insertion.** Expand a target into ii7–V7–target: `[C, …, F] → [C, Gm7, C7, F]`.
  Jazz's single most common move.
- **Passing diminished.** Between roots a whole step apart: `[C, Dm] → [C, C#dim7, Dm]`.

**Reduction** — the progression gets simpler, and for a *guitar* app this is at least as
valuable as making things fancier:

- **Strip extensions to triads.** `Cmaj9 → C`, `F#m11 → F#m`. The grammar makes this
  mechanical, and it pairs directly with the comfort model: "reduce this chart until every
  chord is above 70% comfort on my tuning" is a real request with a real answer. Call it
  what it is — the campfire version — and expect it to be used constantly.
- **Collapse ii–V into V**, and drop passing chords. The inverse of insertion, so the two
  make a nice invertible-ish pair.

Both families need the transform signature to be `List<string> → List<string>` with no
length guarantee — which it already is, but the taxonomy should say so, because
length-changing transforms interact with everything downstream: bars per chord in Song
mode, form length, and the voice-lead solve.

## 10. Surface

**Progressions tab.** A *Transform* action on any saved progression opens a panel: pick a
transform, set parameters, see a live preview. The preview is a two-row diff — original
above, result below, changed chords highlighted — with key before/after, cadence
before/after, and the §6 playability line. Then *save as new* (`ProgressionService.Create`
with provenance) or *replace*.

**"Show me everything"** — run the whole catalogue at default parameters and present the
results as a ranked gallery, sorted by playability. This is the discovery mode, and it is
where a user who does not know what a Neo-Riemannian L operation is will find out that they
like the sound of one.

**Song mode.** *Transform this section* is the same panel scoped to a section, which is
where the two v0.5.2 docs meet: transform a verse into a bridge, keep it if it is playable,
let §5.2 of `ui-song-mode.md` fill the voicings. The variations browser is the songwriting
loop's other half — prediction extends a progression forwards, transformation varies one
sideways.

**CLI.** `dadabe transform --progression <slug> --chain "retrograde,transpose:M2"`,
following the existing command pattern (`ChordCommand`, `VoiceLeadCommand`, …) with an
`Envelope<TransformResultDto>` output. Scriptable batch transformation is the natural home
for the algorithmic-composition use the spec keeps gesturing at.

## 11. Suggested order

1. **`ChordSymbol.ToSymbol()` + round-trip property test** (D44). Nothing works without it.
2. **Transposition done properly** (D45) — named intervals, spelling policy, overflow
   handling. The most-used transform, and the one whose correctness is subtlest.
3. **Root-motion family** — retrograde, rotation, inversion, interval transforms. All cheap
   once (1) exists, none of them key-aware, all always-emittable.
4. **Playability ranking** (D51). Do this before the key-aware transforms — it is what makes
   even the simple ones worth using, and it is pure integration of deployed code.
5. **Key-aware family** — diatonic transposition, parallel mode, functional substitution,
   P/L/R. Needs the out-of-key policy (D46) and the thirdless-chord rules (D53) settled
   first.
6. **Chains and provenance** (D50).
7. **Insertion and reduction** (D52). Reduction before insertion: it is simpler, and
   "reduce until playable" is the request this app is uniquely positioned to answer.

## 12. Open questions

1. **Is functional substitution a rule engine or a table?** The spec's `C→Am, F→Dm, G→Bdim`
   is a table for C major. The general rule is *substitute within the same functional group,
   sharing at least two common tones* — tonic {I, iii, vi}, subdominant {ii, IV}, dominant
   {V, vii°} — which generalises to any key and mode from `KeyInference` alone. The rule
   engine is barely more work and does not have to be rewritten per key. Recommend the rule.
   Tritone substitution stays a separate rule regardless: it is dominant-only and requires a
   dom7 quality.
2. **Do transformations operate on symbols or on specs?** Symbols (`List<string>`) keeps
   transforms composable, serialisable, and CLI-friendly, at the cost of re-parsing between
   chain steps. Specs are richer but cannot represent everything as a symbol on the way out
   (§1). Recommend symbols, with parse-once/emit-once at the chain boundary rather than per
   step.
3. **How much does a transform preserve when it cannot?** Diatonic transposition of a
   borrowed chord, PLR on a seventh, parallel mode on a sus chord — each has a defensible
   "skip it" and a defensible "approximate it". A single global strictness setting (*strict*
   = skip and report, *loose* = approximate and flag) is probably better than a policy per
   transform, but it is a real fork and worth deciding once, up front, rather than
   accreting.
