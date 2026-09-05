# Future work

Scheduled work has moved out of this file. See:

- `docs/v0.5.2/design.md` — transform engine (key-blind family, playability ranking),
  song mode foundation, chord diagram rendering.
- `docs/v0.6/design.md` — key-aware transforms (modal interchange / parallel mode,
  secondary dominants, negative harmony, functional substitution), song arrangement,
  transposition and capo, setlists.

What remains genuinely unscheduled:

- The hand model should be fully customizable.
- Harmonic transformations not covered by v0.5.2/v0.6: chromatic planing and parallel
  harmony. (Tritone substitution ships in v0.5.2; modal interchange, secondary dominants,
  diminished substitution, and negative harmony are scheduled for v0.6.)
- Inner-line generation inside static harmony.
- Chord-melody harmonization.
- Voicing categories beyond Core 5: quartal, cluster, polychord, upper-structure triad,
  altered dominant (as a category, not just as input).
- Polychord input parsing (`C|G`). Extension point already stubbed in `ChordParser.TryParse`;
  slash chords shipped separately.
- Rendering: tab, notation, audio. (Chord diagrams are scheduled for v0.5.2 — the
  `ChordDiagram` model has been computed and discarded since v0.4.1.)
- Persistent memo backends beyond in-memory.

## Maybe future

- No true 2-note dyads will be emitted. ResolveEffectiveHand computes Math.Max(hand.MinStrings, p.MinStrings) and HandModel.Default.MinStrings is 3, so lowering "Min strings" to 2 in the UI has no effect. Power chords come out as 3+ string shapes — which is what guitarists generally play, so it was skipped. (Same floor makes the shell category's noteCountRange: [2, 3] lower bound unreachable, incidentally.) The floor can be relaxed, but it would change output for every chord, so it's not a change to make in passing.
