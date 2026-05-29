# Dadabe v0.2 — Pseudocode

> v0.1 source documentation is archived in `docs/v0.1/source/`.

## Next-chord probability prediction

```text
function PredictNextChords(currentChord, tuning, handModel, topN, entropy):
    candidates = GenerateNextChordCandidates(currentChord)
    scores = []

    for candidate in candidates:
        score = ScoreChordTransition(currentChord, candidate, tuning, handModel)
        scores.append((candidate, score))

    probabilities = Softmax(scores, temperature=EntropyToTemperature(entropy))
    topCandidates = SortDescendingByProbability(probabilities)
    return topCandidates.take(topN)
```

## Entropy input

```text
function EntropyToTemperature(entropy):
    // entropy is user-facing; lower values mean more confident predictions
    // higher values mean more exploration and a flatter distribution.
    return Clamp(1.0 / Max(entropy, 0.01), min=0.1, max=10.0)
```

## Candidate generation

```text
function GenerateNextChordCandidates(currentChord):
    // v0.2 chooses a small, musically meaningful set of next-chord candidates.
    // Start with diatonic progressions, common dominant resolutions, and
    // simple modal interchange targets.
    return [IV, V, vi, ii, iii, bVII, bVI, secondaryDominants...]
```

## Transition scoring

```text
function ScoreChordTransition(currentChord, nextChord, tuning, handModel):
    score = 0
    score += QualityMatchScore(currentChord, nextChord)
    score += VoiceLeadingScore(currentChord, nextChord, tuning)
    score += PlayabilityBias(nextChord, handModel)
    return score
```

## Output assembly

```text
function BuildChordOutput(chord, voicings, nextChords, pretty):
    output = {
        chord: chord,
        voicings: voicings,
        nextChords: [
            { symbol: candidate.symbol, probability: candidate.probability }
            for candidate in nextChords
        ]
    }

    if pretty:
        output.prettyOutput = RenderPrettyChordSummary(chord, voicings)

    return output
```

## Pretty printing

```text
function RenderPrettyChordSummary(chord, voicings):
    diagrams = [RenderChordDiagram(voicing) for voicing in voicings.take(3)]
    return {
        summary: FormatChordHeader(chord),
        diagrams: diagrams,
        note: "Simple ASCII chord diagrams for the top voicings."
    }
```

```text
function RenderChordDiagram(voicing):
    strings = ["E", "A", "D", "G", "B", "e"]
    diagram = ["|" + "---" * 6 + "|"]
    for stringIndex in 0..5:
        fret = voicing.positions[stringIndex].fret
        marker = "x" if fret == -1 else ("0" if fret == 0 else str(fret))
        diagram.append(strings[stringIndex] + ": " + marker)
    return diagram.join("\n")
```
