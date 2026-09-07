# Dadabe v0.6.1 — Bug tracker

Add each discovered problem as a row. Keep **Status** as `open` until a fix is
confirmed; change to `fixed` when the commit lands. Add a **Notes** column entry
for any workaround or diagnosis detail worth keeping.

---

## Editor bugs

| # | Area               | Description                                                                                                                                                                                                                | Status | Notes |
|---|--------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------|-------|
| 1 | Transforms         | You can remove steps in the transform window, but if you remove all of them, the "add step" button stops working.                                                                                                          |        |       |
| 2 | Predictions        | If you change a voicing option and then click a chord to show voicings, the change isn't reflected (I unclicked "spread" and clicked the same chord, but spread options still reflect.)                                    |        |       |
| 3 | Songs              | No way to unpin a voicing after it's been pinned (chord should stay, voicing should go)                                                                                                                                    |        |       |
| 4 | Songs              | With a song selected, I pinned a voicing from the "voicings" tab for Bm9, then in the songs -> sections area I added Bm9 (so now there is a pinned Bm9 and an unpinned Bm9) - when I press "fill unpinned", I get an error: "'Bm9' was pinned under a different tuning or hand model — re-pin it before filling." | | |

## Core bugs

| # | Area  | Description                                                                                                                                                                 | Status | Notes |
|---|-------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------|-------|
| 1 | Songs | Songs need time signatures. (e.g., 4/4, 3/4) Default should be 4/4. We should model a global time signature per song, with a per-section override.                          |        |       |
| 2 | Songs | Songs have "bars" and bars is a whole number. Instead, there should be bars AND beats to allow subdivisions. A bar can have multiple chords, this allows us to express it.  |        |       |
