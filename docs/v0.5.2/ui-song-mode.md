# UI Song Mode

## The basics

The *Songs* tab in the DADABE editor gives users a place to store collections of voicings
for later reference. The editor shows an "active song" dropdown like the active tuning
dropdown. With no song selected, the editor behaves as it does today.

### The basic workflow

- A user goes to the song tab, creates a new song and gives it a name.
- The user goes to the voicings tab, and generates voicings (as per typical workflow)
- The user selects a voicing pins it to a song

## Song sections

A song may have sections, added in the *Songs* tab.

- If there are > 0 sections in a song, an "active section" dropdown is shown along with the "active song" dropdown.
- Each section with > 0 chords is a progression, and can be used anywhere other progressions are usable.
- When viewing a song, if a section has > 0 chords, a predication is displayed for the next chord.

### Sections workflow

- A user goes to the song tab, creates a new song and gives it a name.
- The user adds a named section to the song (e.g., "Verse", "Chorus", "Bridge")
- The user goes to the voicings tab, and generates voicings (as per typical workflow)
- The user selects a voicing pins it to the active song in the active section.

---

# Build-out

Everything below is reachable with the feature set already shipped: `ChordParser` /
`ChordExpander`, `VoicingSearch` (comfort, structure, `ChordDiagram`), `HandModel` /
`SearchParams`, `VoiceLeadSolver` (`TotalFretDistance`, `BuildTransition`, k-shortest
paths), `NextChordPredictor`, `KeyInference`, `TuningCatalog` + saved tunings, the
reference catalogs (cadences / modes / scales), and the slugged-JSON `DataStore`.
No new theory engine is required. Where a workflow needs something genuinely new, it is
called out.

## 1. A chord slot is not a pinned voicing (D37)

This is the load-bearing refinement. The doc above treats a section as a bag of pinned
voicings, but a musician learns a song in two passes:

1. **The chart** — "the verse is Am7 / D7 / Gmaj7 / Cmaj7". Known from a lead sheet, from
   the record, from the person who wrote it. No fingerings yet.
2. **The shapes** — "…and *here* is where I play each of those on this neck, in this
   tuning."

In an alternate tuning the second pass is the entire value of the tool, and it happens
chord by chord over days. So a section is an ordered list of **slots**:

```
Slot
  symbol       "Am7"          required — the chart
  bars         1              default 1
  voicing      Voicing | null the pinned shape, or unset
  pinnedUnder  {…}            provenance, see D43
```

A slot with `voicing == null` is a chord you know but haven't fingered. That single
nullable field is what makes §5.2 (fill the gaps), §5.6 (practice targets), and the
"unfinished song" badge possible. Pin the *positions*, not a search-result index —
result ordering is not stable across model versions or search params.

```
Song
  slug, name
  tuning       one tuning per song (D38)
  key          inferred, overridable (D39)
  tempo        as on ProgressionModel
  hand         song-level HandModel / SearchParams overrides (D42)
  sections[]   definitions
  form[]       arrangement (D40)

Section
  id, name     "Verse", "Chorus"
  slots[]
```

`Song` is one JSON file in `data/songs/<slug>.json` — sections and slots nest inside it
rather than getting their own directories. A song is edited as a unit and is the thing a
user wants to hand to someone else (§5.9).

## 2. A song has a tuning (D38)

Tuning is currently app-level state (the active-tuning dropdown). For a song it is a
property: you do not play half of a song in DADABE and half in standard. Consequences:

- Selecting an active song **sets the active tuning** to the song's tuning.
- If the user then changes the active tuning while a song is active, warn — every pinned
  shape in the song is meaningless in another tuning.
- Every voicing search launched from a song context is pinned to that tuning; the tuning
  selector on the voicings form goes read-only (with an "unlock" escape) while a song is
  active.

This is also the guard rail that makes pinning safe. Without it, users will pin standard-
tuning shapes into a DADABE song and not find out until they pick up the guitar.

## 3. Key and mode (D39)

Run `KeyInference.InferKey` across the flattened chord list of the whole song, and
separately per section. Display it as song metadata: *"Key: A minor (inferred)"*. Let the
user override it; store the override.

Two things fall out immediately, both from data already on disk:

- **Scale / mode suggestion for soloing.** Given the inferred key and the reference
  `ScaleModel` / `ModeModel` catalogs (intervals, note names, `modeOf`, `parentScale`),
  list the scales whose pitch-class set covers the section's chord tones — "Verse: A
  dorian, A natural minor". This is the single most-asked question about any song a
  guitarist is learning, and both halves already exist. Rendering scale shapes on the
  neck is deferred; listing notes and degrees is useful on its own.
- **Per-section key.** Sections often move — a bridge that lifts to the relative major is
  the point of the bridge. Showing "Verse: A minor · Chorus: C major" is free and is real
  musical information about the song.

Where `InferKey` returns null (below 50% coverage) say so plainly — "no clear key centre"
is a true and useful statement about a modal or chromatic tune, not a failure.

## 4. Form: sections are definitions, the arrangement is a playlist (D40)

Sections as written above are *definitions*. How a song actually goes is a sequence of
references to them:

```
form: [Intro, Verse, Chorus, Verse, Chorus, Bridge, Chorus ×2, Outro]
```

Reusing a section rather than duplicating its chords is how musicians think and how charts
are written. It also unlocks the transitions that actually get fumbled:

- **Seam predictions and seam voice leading.** The hard transition in a song is rarely
  inside the verse — it is the last chord of the verse into the first chord of the chorus.
  With a form list, those seams are enumerable, and each one is a `BuildTransition` call.
  Surface them as their own list: "Verse → Chorus: 7 frets of motion."
- **Loop closure.** A repeated section's *last chord back to its own first chord* is a
  transition too. A voice-leading solve that ignores it will happily give you a verse that
  ends at fret 9 and restarts at fret 1. Offer a "closes on repeat" solve: add the wrap
  edge to the cost function. Small change to the existing DAG solve, large practical
  payoff, and it is exactly what a turnaround is.

Form is optional. A song with sections and no explicit form plays its sections in order,
once each.

## 5. Workflows

### 5.1 Import the chart before choosing shapes

- Paste a chord line (`Am7 D7 Gmaj7 Cmaj7`) → a section of unpinned slots. Same parse path
  as the voice-lead form's `chords` field.
- Instantiate a saved `ProgressionModel` as a section (the library already ships
  `twelve-bar-blues`, `pachelbel-canon`, `jazz-ii-v-i`, `andalusian-cadence`, …). These
  are section templates for free.
- The reverse: promote a section to a saved progression, so a riff you worked out in one
  song is reusable in the next. This is the doc's "each section is a progression"
  statement made bidirectional and explicit.

### 5.2 Fill the unset slots by voice leading — honouring what is already pinned

The best feature in the set. `VoiceLeadSolver.Solve` takes an array of candidate voicing
arrays per chord. A pinned slot is simply a layer of **one** candidate. So:

> Pin the two shapes you love. Press *fill*. The solver chooses everything in between to
> minimise total motion, and it cannot move the shapes you already committed to.

No solver change is needed — it is a matter of what you put in `voicingsPerChord[i]`.
Offer `--solutions`-style alternates (the solver already returns k paths) so the user can
audition three fillings and keep one. Re-solvable at any time: raise min-comfort because
the tune is fast or your hands are cold, and refill.

### 5.3 Alternatives drawer on any slot

Click a pinned slot → the other candidates for that chord, ranked not by comfort alone but
by **comfort plus motion cost to its actual neighbours in this section**. Swap one in and
the section's totals re-score live. This is the difference between a chord dictionary and
an arranging tool: the same voicing is good or bad depending on what surrounds it.

### 5.4 Transitions described by which strings move

`VoiceMove` already carries per-string from/to frets. A total distance of 4 tells a
guitarist nothing; "only the B string moves, 2 frets" tells them everything, and "common
tones on strings 1, 2, 5 — hold them" is how the move is actually taught. Render
transitions as their moves, not just their scalar cost. Flag the good cases explicitly:
no motion (a pivot), one finger changes, common tones held.

### 5.5 Position map

Every pinned voicing has fret numbers, so per section: min / max fret, and the span of the
whole song. Draw it as a strip — *Verse: frets 0–4 · Chorus: 5–9 · Bridge: 2–7* — with the
big shifts marked. Position changes are what you rehearse; a song that never leaves frets
0–5 and one that jumps to 12 twice are different songs to learn, and nothing in the app
currently says which one you have.

### 5.6 Practice targets — the comfort budget

Aggregate comfort over the song's pinned voicings and sort ascending. The weakest link is
the thing that will break in performance:

> Bridge, bar 3: F#m7b5 at 38% comfort. Three alternatives above 70% within 2 frets of its
> neighbours →

Comfort, min-comfort filtering, and transition distance are all deployed; this is a query
over them and a link into §5.3. It also gives the song a single honest headline number
("hardest chord: 38%") that is more meaningful than an average.

### 5.7 Transpose, and capo

The most common real-world request a working musician gets is *"can we do it in B♭ for the
singer?"* Two distinct operations, both cheap:

- **Transpose** — shift every slot's symbol root by *n* semitones and re-solve. Requires a
  small symbol-level transpose helper (root pitch class + preserved quality suffix);
  `KeyInference.TryParseRoot` already does the parsing half, and `Pitch.Transpose` the
  arithmetic. Pinned voicings are invalidated by definition — say so, then offer §5.2 to
  refill. On an alternate tuning this is genuinely necessary work, because you cannot
  transpose by sliding shapes the way standard tuning lets you.
- **Capo** — the pitches move but the shapes do not. A `Tuning` is just a list of open
  pitches, so a capo at fret *n* is a tuning transform, and everything downstream works
  unchanged. Store it as a song field, display fret numbers relative to the capo, and show
  both the written and sounding key.

Transpose is *"re-learn it in a new key"*; capo is *"play the same shapes higher"*. Both
belong on a song, and conflating them will annoy anyone who plays for a singer.

### 5.8 Songwriting: predict → accept → pin, in a loop

The doc's per-section next-chord prediction, taken one step further into a compositional
loop:

1. Section context so far → `NextChordPredictor.Predict`, weighted by the song's key
   (D39 override beats inference when the user has set one).
2. **Filter candidates to what is playable** in the song's tuning above the song's comfort
   floor. A prediction you cannot finger in DADABE is noise, and the search to check it is
   the same search the app already runs.
3. Accept a candidate → it appends as a slot *and* pins the voicing that voice-leads best
   from the current last chord.
4. Predict again.

That loop is the app becoming a writing tool rather than a lookup table, and every piece of
it already ships. Show the diatonic / valid-non-diatonic / unrelated classification from
`KeyInference.Classify` as the badge on each candidate, so the user can see they are being
offered a borrowed chord rather than wondering why it sounds like that.

### 5.9 The chart: one page you can put on a music stand

The output artifact. Sections in form order, each slot showing its chord symbol, bars, and
its shape — and this is where the `ChordDiagram` model on `VoicingRow` finally earns its
keep. It is built today and never rendered; the voicings page shows ASCII only. A song
chart is the natural place for real diagrams, because it is the view you read at arm's
length rather than scan in a list.

Include tempo, key, tuning, capo, and per-section position ranges. Make it printable. Also
export plain text (chords over bars) for pasting into a message, and the song JSON for
sharing — the `DataStore` already writes exactly this shape, and the CLI can consume it.

### 5.10 Cadence labelling

Match the tail of each section against the reference `CadenceModel` catalog, transposed to
the inferred key: "Chorus ends on a deceptive cadence." Lower utility than the rest, and
worth doing mainly because it teaches the vocabulary while the user works, and because it
makes the reference tab feel connected to the songs rather than being a glossary nobody
opens.

## 6. Song-level hand constraints (D42)

`HandModel` and `SearchParams` are already exposed in the voicings advanced panel — max
fret, span, min/max strings, allow open / barre / thumb, categories. Attach them to the
song and every search or re-solve launched from that song honours them:

- *"No barres"* — playing it on a 12-string, or a bad-wrist day.
- *"Open strings encouraged"* — it is a drone-y tuning, that is the point.
- *"Frets 0–7"* — it is an acoustic and the neck joint is in the way.

Set once per song rather than re-entered on every search, this turns a preference into an
arrangement decision, which is what it actually is.

## 7. Provenance: what a pinned voicing was pinned under (D43)

A pinned voicing stores positions, so it stays playable forever. But the *comfort number*
next to it was computed under a particular `ModelVersion`, hand model, and search params —
and the comfort model has already been rebalanced once (v0.4.1 barre and mute penalties;
C went from 29 to 142 voicings above the 70% threshold). A stored 82% from before that
change is not comparable to an 82% today.

Record `{ modelVersion, tuning, handHash }` on each pinned slot. When it does not match
current, badge the slot and offer a one-click re-score. Cheap to add now, impossible to
reconstruct later.

## 8. Deferred

- **Setlists** — songs ordered into a set, with retune costs between them made visible
  ("these four are in DADABE, these three standard — here is an order with one retune").
  Genuinely useful for anyone who gigs, and computable from song tunings alone, but it is
  a second entity with its own tab. v0.6.
- **Audio, notation, tab rendering** — still `docs/future.md`.
- **Harmonic transformations** (tritone sub, modal interchange, negative harmony) —
  `ITransformation` is a stub interface with no implementations. Song mode would be the
  natural home for "show me a reharmonisation of this section", but nothing behind it
  exists yet.
- **Rhythm beyond bar counts** — no time signature, no strumming or subdivision. Bars per
  slot is the honest limit of what the current model can say about time.

## 9. Open questions

1. **Does pinning replace or extend the voicings workflow?** Pinning from the voicings tab
   (as written) means leaving the song to do song work. The alternative is a chord slot
   that opens the search inline, already scoped to the song's tuning and hand model, with
   its neighbours known so candidates can be ranked by §5.3. That is a better workflow and
   a bigger build.
2. **Is `form` in v0.5.2 or later?** Sections alone deliver most of the value; the form
   list is what makes seams, loop closure, and the printable chart correct. Shipping
   sections without it means the chart cannot express "Chorus ×2".
3. **What happens to a song when its tuning changes?** Proposed: refuse silently-breaking
   edits — require an explicit "retune this song" action that invalidates every pinned
   voicing and offers §5.2 to refill.
