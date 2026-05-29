# Dadabe v0.1 — Pseudocode & Vaguely Formal Specification

Companion to [design.md](design.md). Where the design doc speaks in prose, this
document re-states the same content in pseudo-mathematical notation, with
algorithmic skeletons and proof sketches of the load-bearing invariants. It is
non-normative: if the two disagree, [design.md](design.md) wins.

## 0. Notation & Conventions

Throughout, let:

- ℕ = {0, 1, 2, …}, ℤ = the integers, ℝ = the reals.
- `⟦τ⟧` = the set of inhabitants of type τ.
- `f : A → B` = a total function; `f : A ⇀ B` = partial.
- `A*` = finite sequences over A; `|s|` = the length of `s ∈ A*`.
- `s[i]` = the i-th element of a sequence, 0-indexed.
- `s ⊕ t` = concatenation of sequences.
- `P(A)` = the power set of A.
- `≺_lex` = the standard lexicographic order on tuples, given an order on each
  coordinate.
- `⊢` is read "derives" or "produces."
- All hashes are byte-strings; equality on hashes is byte equality.

A **proposition** (Prop.) is a claim. A **lemma** (Lem.) is a small supporting
fact. A **theorem** (Thm.) is something load-bearing. Proofs marked *(sketch)*
are gestural — they explain the shape of the argument rather than discharging
every case.

---

## 1. Primitives

### 1.1 PitchClass, Note, Pitch

```text
PitchClass    ≡ ℤ/12ℤ                    -- math-only; never user-facing (D12)
Letter        ≡ {A, B, C, D, E, F, G}
Accidental    ≡ ℤ ∩ [−2, +2]             -- ♭♭, ♭, ♮, ♯, 𝄪
Note          ≡ Letter × Accidental
Octave        ≡ ℤ                         -- octave follows the *letter* (D12)
Pitch         ≡ Note × Octave
```

Define:

- `pc : Note → PitchClass`, the natural projection ignoring spelling.
- `midi : Pitch → ℤ`, the standard 12-TET assignment with `A4 ↦ 69`.

> **Prop. 1.1 (Enharmonic collapse).** `pc` is not injective.
>
> *Proof.* Witness: `pc(B, +1) = pc(C, 0) = 0 ∈ ℤ/12ℤ`, yet
> `(B, +1) ≠ (C, 0)` as elements of `Letter × Accidental`. ∎

> **Prop. 1.2 (Letter-indexed octave).** There exists `p ∈ Pitch` with
> `midi(p) = 59` and `octave(p) = 4`.
>
> *Proof.* Take `p = ((C, −1), 4)`, i.e. C♭4. Then `pc(p) = 11`, and
> `midi(p) = 12 · (4 + 1) − 1 = 59`. ∎

### 1.2 Interval

```text
Interval ≡ { semitones : ℤ , quality : QualityLabel }
QualityLabel ⊆ { P1, m2, M2, m3, M3, P4, A4, P5, m7, M7, m9, M9, A9, P11, A11, m13, M13, … }
```

`quality` carries spelling-aware information that `semitones` alone discards
(`♯11` and `♭5` are equal as integers, distinct as intervals). Per §10.2
the chord grammar stores `quality` *next to* `semitones`, not merely
derives it — the storytelling vocabulary is canonical input, not algorithm
output.

### 1.3 Tuning

```text
Tuning ≡ { strings : Pitch* | |strings| ≥ 1 ∧ strings sorted by midi ascending }
```

Indexing convention: `strings[0]` is the *lowest-sounding* string. For DADABE,
`strings = ⟨D2, A2, D3, A3, B3, E4⟩`.

---

## 2. Chord Symbols and Specs

### 2.1 Syntax

Let `Σ` be the ASCII alphabet. The chord grammar `G` (per
[design.md §10.2](design.md#102-chordgrammarjson-per-d19)) defines a regular
language `L(G) ⊆ Σ*` of well-formed symbols.

```text
parse : Σ* ⇀ ChordSymbol
parse(s) = ⊥  ⟺  s ∉ L(G)
```

A `ChordSymbol` is a 5-tuple:

```text
ChordSymbol ≡ {
  root        : Note ,
  quality     : FormId ,
  extensions  : ModId* ,
  alterations : ModId* ,
  bass        : Note?               -- ⊥ in v0.1 (slash chords deferred)
}
```

### 2.2 Expansion to ChordSpec

The stacked-thirds letter walk `W` (D12) is the function that, given a root
`(L, a)` and an interval list `I = ⟨i₁, …, i_k⟩`, produces a spelled note
sequence:

```text
W : Note × Interval* → Note*
W((L, a), ⟨i₁, …, i_k⟩) = ⟨n₁, …, n_k⟩
  where n_j = ((L + 2(degree(i_j) − 1)) mod 7,  a_j)
        and a_j chosen s.t. midi-distance(n_j.note, (L, a)) ≡ semitones(i_j) (mod 12)
```

In English: letters step in thirds; accidentals are then adjusted to land on
the right pitch class.

```text
expand : ChordSymbol → ChordSpec

ChordSpec ≡ {
  root  : Note ,
  tones : { function : FunctionId , quality : QualityLabel , note : Note , pc : PitchClass }*
}
```

Each tone is **quadruply over-specified** — `function`, `quality`,
`note`, and `pc` are mutually derivable for the standard vocabulary —
but the redundancy is the point: every projection of the story
(scale-degree label, interval label, spelled note, sounding pitch
class) is materialised explicitly so no later stage has to guess.

> **Prop. 2.1 (Spelling distinguishes enharmonic chords).**
> `expand(F♯maj7).tones ≠ expand(G♭maj7).tones` (as sequences of `Note`),
> even though their projections through `pc` are equal as multisets.
>
> *Proof.* `W((F, +1), ⟨P1, M3, P5, M7⟩) = ⟨F♯, A♯, C♯, E♯⟩` whereas
> `W((G, −1), ⟨P1, M3, P5, M7⟩) = ⟨G♭, B♭, D♭, F⟩`. ∎

> **Prop. 2.2 (Inclusion of required tones).** For every `ChordSpec` produced
> by `expand`, `required ⊆ functions(tones)`.
>
> *Proof.* By construction of the grammar: `required` is the union of the
> form's `required` and each applied modifier's `required`; each modifier
> either adds its required functions to `tones` (`addition`) or displaces
> a function while adding another, preserving membership of the required
> function in `tones` (`alteration`). ∎

---

## 3. The Fretboard

### 3.1 FretLayout

Let `T : Tuning` with `|T.strings| = n` and let `F ∈ ℕ` be the maximum fret
considered (CLI `--frets`, default 15).

```text
Layout(T, F) ≡ { (s, f) : s ∈ [0, n), f ∈ [0, F] }

pitchOf : Layout × Tuning → Pitch
pitchOf((s, f), T) = T.strings[s] transposed up f semitones, with
                     spelling carried from T.strings[s]'s open-string
                     name (a default; rebound per chord context by
                     spellInContext below).

spellInContext : Pitch × ChordSpec ⇀ Note
spellInContext(p, C) = the unique tone.note ∈ C.tones with tone.pc = pc(p),
                       if any; ⊥ otherwise.
```

`pitchOf` is chord-context-free: it just locates a sounding pitch on
the board with a *default* spelling. The chord context's spelling
preference is layered on by `spellInContext` at projection time
(§3.2). This is the precise mechanism that lets the same fret render
as `D♯3` for `B7` and `E♭3` for `C7`.

### 3.2 Reachability

Given a `ChordSpec C`:

```text
Reach(C, T, F) ≡ {
  (s, f, ν, φ) :
    (s, f) ∈ Layout(T, F)  ∧
    spellInContext(pitchOf((s,f), T), C) = some tone.note ≠ ⊥  ∧
    ν      = ( spellInContext(pitchOf((s,f), T), C),  octave(pitchOf((s,f), T)) )  ∧
    φ      = the tone's function
}
```

`Reach(C, T, F)` is the set of `FretPosition`s the chord's pitch classes
project onto, each labelled with its function and chord-context spelling.
`Reach` is the choke-point through which the chord's story rebinds every
spelling on the fretboard.

> **Lem. 3.1 (Reach is bounded).**
> `|Reach(C, T, F)| ≤ n · (F + 1)`.
>
> *Proof.* Reach is a subset of `Layout(T, F)`, which has exactly `n · (F+1)`
> elements. ∎

---

## 4. Voicing Search

### 4.1 Per-string choice

For each string `s ∈ [0, n)`, define the choice domain:

```text
Choices(s, C, T, F) ≡ { f ∈ ℕ | (s, f, _, _) ∈ Reach(C, T, F) } ∪ { ⊤ }
```

where `⊤` denotes *muted*. The total search space is the cartesian product:

```text
Π ≡ Choices(0) × Choices(1) × ⋯ × Choices(n−1)
```

### 4.2 Lexicographic order

Order each `Choices(s)` by the natural ordering on `ℕ`, augmented with
`f < ⊤` for every `f ∈ ℕ` (i.e. muted sorts *after* all fretted positions).
Order `Π` lexicographically from string 0 (outermost) to string n−1
(innermost). Call this order `≺_lex`.

> **Thm. 4.1 (Deterministic emission order, D5).** For fixed
> `(C, T, F, HandModel, SearchParams)`, the sequence of voicings emitted by
> the algorithm is `≺_lex`-monotonically increasing.
>
> *Proof (sketch).* The search enumerates `Π` in `≺_lex` order by nested
> iteration outer-to-inner over string indices, and within each string the
> iteration visits `0, 1, 2, …, F, ⊤` in that order. Pruning steps (§4.3)
> and the fingering filter (§5) are pure predicates: removing elements of a
> sorted sequence preserves the order on the remaining elements. The final
> `--limit` operator is a prefix truncation, again order-preserving. ∎

> **Cor. 4.2 (`--limit` is stable).** Two runs with the same inputs and
> `--limit = k` emit the same `k` voicings in the same order.

### 4.3 Coarse playability

A candidate `π ∈ Π` is *geometrically admissible* iff:

```text
admissible(π, hand) ≡
    span(π)        ≤ hand.maxSpan                          (P1)
  ∧ |sounded(π)|   ∈ [hand.minStrings, hand.maxStrings]    (P2)
```

where `span(π) = max(fretted(π)) − min(fretted(π))` if `fretted(π) ≠ ∅`,
else 0, and `sounded(π) = { s : π[s] ≠ ⊤ }`.

### 4.4 Required-tone test

```text
covers(π, C) ≡  { φ : (s, π[s], _, φ) ∈ Reach(C, T, F), π[s] ≠ ⊤ }  ⊇  C.required
subsetOK(π, C) ≡  { pc(pitchOf((s, π[s]), T)) : π[s] ≠ ⊤ }  ⊆  pcSet(C)
```

`subsetOK` is the *permissive inclusion* of D4: any subset of the chord's
pitch classes is welcome, but nothing outside it is.

---

## 5. Fingering Validity

A `Fingering` is a triple `(A, B, M)` where:

```text
A ⊆ { (s, f, g) : s ∈ [0, n), f ∈ ℕ, g ∈ Finger ∪ {⊥} }   -- assignments
B ⊆ { (g, f, ℓ, h) : g ∈ Finger, f ∈ ℕ, 0 ≤ ℓ ≤ h < n }    -- barres
M ⊆ { (s, σ) : s ∈ [0, n), σ ∈ MuteSource }                -- mutes
Finger      ≡ {1=Index, 2=Middle, 3=Ring, 4=Pinky, 5=Thumb}
MuteSource  ≡ {AdjacentUnderside, BarreExtended, ThumbWrap, OuterHand, Unfretted}
```

A `Fingering` is **valid for HandModel H** iff all eight predicates hold.
Let `strings(A) = { s : (s, _, _) ∈ A }`, similarly `strings(M)`.

```text
(V1) Coverage:           strings(A) ⊎ strings(M) = [0, n)            (disjoint union)
(V2) Open-string:        ∀(s, f, g) ∈ A . (f = 0 ⟺ g = ⊥)
(V3) FingerUniq:         ∀ g ∈ {1,2,3,4} . |{ f : (_, f, g) ∈ A }| ≤ 1
                         UNLESS g is the head of some β ∈ B, in which case
                         g may appear on every string in β at frets = β.f.
(V4) BarreShape:         ∀ β=(g, f, ℓ, h) ∈ B .
                         { s : (s, f, g) ∈ A } = [ℓ, h]
(V5) BarreCount:         |B| ≤ H.maxBarres
(V6) Reach:              ∀ g₁, g₂ ∈ {1,2,3,4}, g₁ < g₂, with
                         (_, f₁, g₁), (_, f₂, g₂) ∈ A .
                         |f₁ − f₂| ≤ H.stretch[(g₁, g₂)]
(V7) ThumbRule:          (_, f, 5) ∈ A  ⟹  H.thumb.allowed ∧ s = 0 ∧ f ≤ H.thumb.maxFret
(V8) MuteLegal:          ∀ (s, σ) ∈ M . muteLegal(s, σ, A, B)
```

The `muteLegal` predicate unfolds per source:

```text
muteLegal(s, AdjacentUnderside, A, B) ≡ ∃ (s', f', g') ∈ A . f' > 0 ∧ |s' − s| = 1
muteLegal(s, BarreExtended,     A, B) ≡ ∃ (g, f, ℓ, h) ∈ B  .  s ∈ [ℓ, h]
muteLegal(s, ThumbWrap,         A, B) ≡ s = 0 ∧ ∃ (0, _, 5) ∈ A
muteLegal(s, OuterHand,         A, B) ≡ s = min(unmuted) ∨ s = max(unmuted)
muteLegal(s, Unfretted,         A, B) ≡ ¬ (other source applies) ∧ s ∉ footprint(A, B)
```

Define:

```text
Solve : Position* × HandModel ⇀ Fingering
Solve(π, H) = some valid (A, B, M) for H realising π,         if any exists
            = ⊥,                                              otherwise
```

The internal search strategy of `Solve` is not pinned by this spec; only
the determinism of its tie-break ordering is, so that content hashes
remain stable (see §7).

> **Lem. 5.1 (Validity is decidable in PTIME for fixed n).** Each of (V1)–(V8)
> is a conjunction of polynomially many constraints over a finite domain
> bounded by `n + F + |A| + |B|`.
>
> *Proof (sketch).* Each predicate enumerates a finite set; the largest is
> (V6), which is `O(|A|²)`. ∎

---

## 6. The Voicing Pipeline

The full search is the composition:

```text
search : ChordSymbol × Environment × SearchParams → Voicing*
search(s, E, p) ≡
    let C  = expand(parse(s))                                        (1)
    let R  = Reach(C, E.tuning, p.frets)                             (2)
    let Π' = enumerate Π in ≺_lex order, with R restricting Choices  (3)
    Π'' = { π ∈ Π' : admissible(π, E.handModel) }                    (4)
    Φ   = { (π, ϕ) : π ∈ Π'',  ϕ = Solve(π, E.handModel),  ϕ ≠ ⊥ }   (5)
    Φ'  = { (π, ϕ) ∈ Φ : covers(π, C) ∧ subsetOK(π, C) }             (6)
    Φ'' = { (π, ϕ, κ) : (π, ϕ) ∈ Φ',  κ = classify(π, C) }           (7)
    return prefix(map(voicingOf, Φ''), p.limit)                      (8)
```

Step numbers correspond 1-to-1 with [design.md §7](design.md#7-algorithm-sketch--voicing-generation).

> **Thm. 6.1 (Soundness of permissive inclusion, D4).** For every
> `v ∈ search(s, E, p)`, the multiset
> `{ pc(pitchOf(pos, E.tuning)) : pos ∈ v.positions, ¬pos.muted }`
> is a subset of `pcSet(expand(parse(s)))`.
>
> *Proof.* The constraint is precisely `subsetOK` (step 6), which is checked
> on every survivor before emission. ∎

> **Thm. 6.2 (Completeness up to playability).** Let `π ∈ Π` satisfy
> `admissible(π, E.handModel) ∧ Solve(π, E.handModel) ≠ ⊥ ∧ covers(π, C) ∧
> subsetOK(π, C)`. Then the voicing derived from `π` appears in
> `search(s, E, p)` provided `p.limit` is large enough to admit it under
> `≺_lex`.
>
> *Proof.* The enumeration in step (3) is exhaustive over `Π`; steps (4)–(6)
> drop only failing candidates; step (8) is prefix-truncation. So any `π`
> meeting the conjunction appears unless its `≺_lex` rank is ≥ `p.limit`. ∎

### 6.1 Comfort score

```text
comfort(v) = clamp_{[0,1]}(
    1
  − 0.40 · (span(v) / hand.maxSpan)
  − 0.10 · |mutedStrings(v)|
  − 0.15 · |barres(v)|
  − 0.05 · (lowestFret(v) / hand.maxFret) )
```

> **Prop. 6.3 (Comfort does not reorder).** Permuting the output by descending
> `comfort` is **not** performed by the algorithm; consumers may do it
> themselves. Emission order is `≺_lex` per Thm. 4.1.

---

## 7. Content Hashing & Memoization

### 7.1 Canonical bytes

For each identity-bearing type τ, there is a canonical serialization
`⟨·⟩_τ : ⟦τ⟧ → Σ*` whose layout is fixed per
[design.md §8.1](design.md#81-content-hashes-d17).

```text
hash : Σ* → Σ³² (lowercase hex)
hash(b) ≡ first 128 bits of SHA-256(b), rendered as 32 hex chars
```

The `ContentHash` of a value `x : τ` is the triple
`(namespace(τ), version(τ), hash(⟨x⟩_τ))`.

> **Lem. 7.1 (Equality lifts).** For `x, y : τ`,
> `⟨x⟩_τ = ⟨y⟩_τ  ⟹  ContentHash(x) = ContentHash(y)`.
>
> *Proof.* `hash` is a function. ∎

> **Lem. 7.2 (Namespace separation).** `ContentHash`es from distinct
> namespaces are distinct, even if their digests collide on the SHA-256
> truncation.
>
> *Proof.* `namespace` is a coordinate of the `ContentHash` record; record
> equality requires componentwise equality. ∎

### 7.2 The memo & its prime invariant

Let `f : A → B` be a referentially transparent function with `A, B`
content-hashable. A memo `μ : A → B?` is *sound for f* iff
`∀ x ∈ A . μ(x) ≠ ⊥ ⟹ μ(x) = f(x)`.

> **Thm. 7.3 (Memo correctness, D18 invariant).** Let `search_μ` denote the
> pipeline of §6 wrapped with a sound memo `μ`. Then for all inputs,
> `search_μ(s, E, p) ≡ search(s, E, p)` byte-for-byte in JSON output.
>
> *Proof (sketch).* Each memoized step `f` is replaced by
> `λ x. if μ(x) = ⊥ then let y = f(x) in μ ← μ[x ↦ y]; y else μ(x)`. By
> soundness of μ, the returned `y` equals `f(x)` in either branch.
> Composition of memo-wrapped functions is `=` to composition of the bare
> functions, pointwise. JSON serialization is a function of the values; equal
> values serialize identically. ∎

> **Cor. 7.4 (Golden tests are bipartite).** Every golden test executes
> twice — once with `μ = ⊥` and once with `μ = InMemoryMemo` — and the
> JSON output must agree byte-for-byte. A violation refutes the soundness
> of μ.

### 7.3 Identity carving-up

Identity is **story-dictated**: every coordinate that contributes to a
type's JSON serialization participates in its content hash. Per
[design.md §8.1](design.md#81-content-hashes-d17):

```text
Tuning.id     depends on  open-string spelled Pitches (Note × Octave)
ChordSpec.id  depends on  root.note,  tones (function, quality, note, pc)
Voicing.id    depends on  chord_spec.hash,  tuning.hash,  hand_model.hash,  positions
Fingering.id  depends on  positions,        hand_model.hash
Transition.id depends on  from.hash,        to.hash
```

`Fingering` is the lone outlier — and deliberately so — because its JSON
contains only physical assignments (finger, fret, barre, mute source);
nothing in it depends on the chord's story. Many `Voicing`s (one per
chord context) can therefore share a `Fingering`, which is correct.

> **Prop. 7.5 (Story-dependent voicing identity).** Let `s₁ ≠ s₂` be two
> chord symbols whose `expand`-ed `ChordSpec`s differ in any
> identity-bearing field (root note, any tone's function, quality, spelled
> note, or pitch class). Then for fixed tuning `T`, hand `H`, and position
> tuple `π`, the resulting voicings satisfy
> `id(v₁) ≠ id(v₂)`.
>
> *Proof.* The canonical layout `⟨·⟩_Voicing` begins with `ChordSpec.hash`.
> By assumption `⟨C₁⟩_{ChordSpec} ≠ ⟨C₂⟩_{ChordSpec}` byte-wise, so by
> Lem. 7.1 (and collision resistance of truncated SHA-256, treated
> axiomatically) `C₁.hash ≠ C₂.hash`. The remaining bytes of
> `⟨v₁⟩_Voicing` and `⟨v₂⟩_Voicing` are equal by hypothesis, so the byte
> strings differ exactly at the chord-spec prefix, hence their hashes
> differ. ∎

> **Prop. 7.6 (Enharmonic distinction).** `id(expand(parse("F#maj7"))) ≠
> id(expand(parse("Gbmaj7")))`.
>
> *Proof.* `expand("F#maj7").root = (F, +1)`, whereas
> `expand("Gbmaj7").root = (G, −1)`. Both contribute to
> `⟨·⟩_{ChordSpec}` as distinct (letter, accidental) byte pairs.
> Lem. 7.1 + collision resistance. ∎

> **Prop. 7.7 (Hand-distinguished voicings).** Two `HandModel`s that yield
> distinct valid `Fingering`s for the same positions produce distinct
> `voicing.id`s, even with the same chord and tuning.
>
> *Proof.* `⟨·⟩_Voicing` includes `HandModel.hash`. Distinct hand-model
> identity bytes propagate through. ∎

> **Thm. 7.8 (Voicing.id ⇔ JSON output).** Fix a `Voicing`'s
> serialization function `J : Voicing → Σ*` (the JSON writer). Then
> for any two voicings `v₁, v₂` emitted by `search`:
>
>     id(v₁) = id(v₂)  ⟺  J(v₁) = J(v₂)
>
> (⇐) trivially. (⇒) by collision resistance of truncated SHA-256.
>
> *Proof (sketch).* Every field appearing in `J(v)` is a deterministic
> function of `(ChordSpec, Tuning, HandModel, positions)`:
>
> - `positions[*].note` and `positions[*].function` — from
>   `Reach(C, T, F)` (§3.2), which depends on `(C, T)` and the
>   position itself.
> - `positions[*].fret`, `string` — the positions themselves.
> - `bassNote`, `topNote`, `span`, `lowestFret`, `highestFret`,
>   `openStrings`, `mutedStrings` — geometric derivations from positions
>   spelled via `Reach`.
> - `category`, `comfort` — pure functions of the foregoing.
> - `fingering.*` — output of `Solve(positions, H)` (§5); deterministic
>   given `(positions, H)`.
>
> All four coordinates participate in `⟨·⟩_Voicing`, so equal hashes ⇒
> equal canonical bytes ⇒ equal `(C, T, H, π)` ⇒ equal `J(v)`. ∎

> **Cor. 7.9 (No silent JSON divergence).** Within a single
> `schemaVersion`, no two distinct JSON serializations of `Voicing`
> can share an id. A consumer pinning `voicing.id` as a cache key
> across runs is therefore safe.

---

## 8. Termination & Complexity

Let `n = |T.strings|`, `F = p.frets`, `k = |C.tones|`.

> **Thm. 8.1 (Termination).** `search` halts on every well-formed input.
>
> *Proof.* (1) `parse` is regular-language recognition over a finite string.
> (2) `expand` is a finite walk of length `k` per the form's `tones`. (3) The
> enumeration of `Π` is over a finite cartesian product of size
> ≤ `(F + 2)^n`. (4)–(7) are predicates evaluated in finite time per element
> (Lem. 5.1). (8) is a prefix truncation. ∎

> **Thm. 8.2 (Worst-case complexity).** Naively, `search` runs in
> `O((F + 2)^n · poly(n, k))` time. For `n = 6`, `F = 15` this is
> `O(17^6) ≈ 24 · 10⁶` candidates — tractable for a CLI invocation, and
> in practice reduced by orders of magnitude after `admissible` (P1, P2).
>
> *Proof.* Direct count of `Π` plus the polynomial-time check on each
> element. ∎

> **Conj. 8.3 (Solver dominates).** Under the v0.1 default `HandModel`,
> `Solve` accounts for ≥ 80% of wall-clock time on typical chord queries.
>
> *Evidence.* Steps (3)–(4) are arithmetic; step (5) backtracks a CSP. We
> have not measured. ∎

---

## 9. Glossary of Symbols

| Symbol     | Meaning                                                    |
| ---------- | ---------------------------------------------------------- |
| `T`        | Tuning                                                     |
| `C`        | ChordSpec                                                  |
| `H`        | HandModel                                                  |
| `n`        | string count `|T.strings|`                                 |
| `F`        | max fret considered (CLI `--frets`)                        |
| `Π`        | cartesian product of per-string choices                    |
| `π`        | one element of `Π`; a candidate voicing as fret tuple      |
| `≺_lex`    | lexicographic order on `Π` (D5)                            |
| `R`        | `Reach(C, T, F)`, reachable labelled positions             |
| `A, B, M`  | assignments, barres, mutes of a Fingering                  |
| `μ`        | a memo                                                     |
| `⟨·⟩_τ`    | canonical byte serialization for type τ                    |
| `⊤`        | the *muted* token in per-string choice                     |
| `⊥`        | absence (no fingering exists / parse fails / memo miss)    |
| `pcSet(C)` | `{ tone.pc : tone ∈ C.tones }`                             |

---

## 10. What This Document Is Not

- It is **not** an implementation guide. The internal strategy of `Solve` is
  unconstrained beyond producing one of the valid fingerings deterministically.
- It is **not** a soundness proof of SHA-256. Collision resistance is taken
  as axiomatic (Prop. 7.6).
- It is **not** a re-derivation of music theory. The stacked-thirds walk `W`
  is taken as given by tradition; we only require it be functional.
- The proofs above are *vaguely* formal: enough to make the invariants legible,
  not enough to mechanize. Anyone wishing to discharge them in Lean or Coq is
  encouraged but unsupervised.
