# Chord symbol grammar — forms, extensions, and alterations

Reference for how `Dadabe.Core.Chord.ChordParser`/`ChordExpander` turn a typed chord symbol
(`E13sus4`, `C7#9b13`) into a tone set, and how `ChordSymbol.ToSymbol()` renders one back to
canonical text. The grammar itself is data (`ChordGrammar.json`, D19), user-extensible via a
same-named overlay file (D22) — this document explains the two building blocks the JSON
expresses, not the JSON schema line by line.

## Forms vs. modifiers

A chord symbol parses as **root + form + zero or more modifiers**, matched in that order,
each phase longest-token-first (`ChordParser.cs`):

- A **form** is a complete named quality with its own fixed tone set — `maj7`, `m11`, `13`,
  `sus4`, `dim`. Exactly one form is matched, immediately after the root.
- A **modifier** adjusts the matched form's tones afterward, and any number may follow, in
  any order (`C7b9#11` and `C7#11b9` parse to the same alterations). Two kinds:
  - **Addition** (`add9`, `add11`, `add13`) — adds a tone without removing anything.
    Distinct from a form like `9`, which already includes the 9th as part of its own tone
    set; `add9` is for stacking a 9th onto a form that doesn't otherwise have one (e.g.
    `Cadd9` is a bare major triad plus a 9th, not a `C9` dominant chord).
  - **Alteration** (`b5`, `#5`, `b9`, `#9`, `#11`, `b13`, `sus4`) — displaces one tone and
    adds another in its place. `displaces` names the function being removed (e.g. `"5"` for
    `b5`/`#5`); `addTones` names what replaces it.

The parser does not check musical sense — any modifier may follow any form (`Cdim7add9` is
grammatical, if not something a working musician would ever write) — because the grammar's
job is mechanical tone-set assembly, not validation.

## Extensions vs. alterations, in `ChordSymbol`

`ChordSymbol` stores the matched form as `Quality` (a single string — `""`, `"m7"`, `"13"`,
`"sus4"`) and the matched modifiers split into two arrays by kind:

- `Extensions` — addition-kind modifier tokens (`add9`, `add11`, `add13`).
- `Alterations` — alteration-kind modifier tokens (`b5`, `#5`, `b9`, `#9`, `#11`, `b13`,
  `sus4`).

A seventh, ninth, eleventh, or thirteenth written as part of the base quality (`Cmaj7`,
`C9`, `Cm13`) is **not** in `Extensions` — it's baked into `Quality` via its own form. Only a
tone added independently of the form (`add9`/`add11`/`add13`) lands in `Extensions`. This
distinction matters for anything that inspects a chord's shape rather than just its emitted
string — see [Extensions and alterations under the v0.6 key-aware
transforms](v0.6/design.md#extensions-and-alterations-under-strictloose) for a concrete case
where it does.

`ChordSymbol.ToSymbol()` renders `Quality` then `Extensions` then `Alterations`, each group
in a fixed canonical order (`ExtensionOrder`/`AlterationOrder` in `ChordSymbol.cs`) regardless
of input order — `C7b13#9` and `C7#9b13` both parse and re-emit as `C7#9b13` — and aliases
normalize (`C-` → `Cm`, `CM7` → `Cmaj7`).

## `sus4` as a modifier (D57)

Before this decision, `sus4`/`sus`/`sus2` existed only as standalone **forms** — a sus chord
was its own triad (root + 4th + 5th, no third, no seventh). That's correct for a plain
`Csus4`, but it meant `7sus4`, `9sus4`, and `13sus4` — all standard fakebook vocabulary, and
far more common in practice than a bare sus triad — couldn't be typed at all:

```
$ dadabe chord E7sus4
error: Unparsed tail 'sus4' after parsing root + form + modifiers in 'E7sus4'.
```

**Resolved:** `sus4`/`sus` is now *also* registered as an alteration modifier — `displaces:
"3"`, `addTones: [{"function": "4", ...}]` — the same mechanism `b5`/`#5`/etc. already use.
It composes with any form that has a plain `3` to displace (`7`, `9`, `11`, `13`, `maj7`,
`maj9`, `maj11`, `maj13`, `6`, and the bare triad), and with other alterations in the same
symbol (`E7sus4b9` is the classic altered-dominant-sus chord and now parses). The standalone
`sus4` **form** token is unchanged, so a bare `Csus4` still matches it directly before the
modifier phase ever runs — the two registrations don't collide because forms and modifiers
are matched in separate passes.

Only the standard notation order is supported: the extension number first, `sus4` after
(`E13sus4`, `E13sus`, `E7sus4`) — which is what every real chord chart uses. The reverse
order (`Esus13`) is not standard notation and does not parse; supporting it would require the
parser to backtrack on the form it already committed to, for an order nobody actually writes.

Worked example — the chord that prompted this: **E13** (`E, G#, B, D, F#, A, C#` — root,
3, 5, b7, 9, 11, 13, with only 3/b7/13 *required*, so a practical guitar voicing dropping 5,
9, and 11 was already valid) and **E13sus** / **E13sus4** (same, with the 3 displaced by a
4: `E, B, D, F#, C#, A` as the tone set, `4/b7/13` required).
