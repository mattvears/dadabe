# Dadabe v0.2 — Design

> Current v0.1 source documentation is archived under `docs/v0.1/source/`.

## Overview

v0.2 extends the existing chord voicing engine with probabilistic next-chord prediction and a user-configurable entropy parameter to control prediction diversity.

## New features

- `nextChords` output in chord responses:
  - computes probability scores for likely next chords
  - returns the top `N` chords ordered by probability
- `entropy` input parameter:
  - controls the sharpness or spread of the distribution
  - lower entropy → more confident, narrower predictions
  - higher entropy → more exploratory, broader predictions
- Add a lightweight pretty printer for human-readable chord output:
  - chord diagrams rendered in plain text or simple ASCII graphics
  - optional `pretty` mode as a delivery format for developer feedback
  - minimal implementation cost, intended as a v0.2 driving feature
- Results should be reproducible given the same seed, chord context, tuning, and entropy.

## Input model

- chord symbol / chord spec
- tuning
- search constraints / hand model
- `topN` (number of next-chord candidates to return)
- `entropy` (real value controlling distribution temperature)
- `pretty` / `outputFormat` to request human-readable chord diagrams
- optional context / progression history for future v0.3 work

## Output model

The chord output shape should include:

- original chord information
- playable voicing set
- metadata including comfort and source hand model
- `nextChords`: array of predicted next chords, each with:
  - `symbol`
  - `probability`
  - optional explanation or ranking score
- optional `prettyOutput`: human-readable chord diagram text and formatted summary

## Prediction architecture

- define a lightweight next-chord prediction model suitable for v0.2
- base probabilities on the current chord's function, quality, and common progression behavior
- apply entropy as a softmax temperature or exploration parameter
- sort candidates descending and truncate to `topN`

## Migration from v0.1

- preserve v0.1 architecture and JSON schema where possible
- add `nextChords` as an additive field in chord output
- keep v0.1 source docs archived and stable in `docs/v0.1/source/`

## Open questions

- What candidate space should next-chord prediction operate over? (diatonic only, extended alterations, modal interchange)
- Should the model be data-driven, rule-driven, or hybrid?
- How should `entropy` map to the internal probability distribution?
- What schema guarantees are required for the chord prediction output?
