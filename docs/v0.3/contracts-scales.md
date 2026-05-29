# v0.3 — Scales / Modes / Cadences contracts

Purpose: define conservative, additive schemas and DTOs for scales, modes, and cadences so future features (progressions, transformations, rendering) can rely on stable, testable input shapes.

Schemas created:
- schemas/scale.schema.json — named scale with ordered notes and interval list.
- schemas/mode.schema.json — mode definition with parentScale and degree index.
- schemas/cadence.schema.json — cadence types (authentic/plagal/deceptive/half) and example progression.

DTOs created (placeholders):
- Dadabe.Cli.Io.ScaleDto
- Dadabe.Cli.Io.ModeDto
- Dadabe.Cli.Io.CadenceDto

Tests:
- tests/Dadabe.Cli.Tests/ScaleCadenceModeTests.cs verifies JSON round-trip for these DTOs.

Guidelines:
- Keep schemas conservative and additive; prefer `additionalProperties: false` for clarity but keep nullable optional fields.
- When extending, add new optional fields and update `schemas/*.schema.json` and tests verifying backward compatibility.

Next actions:
- Wire a minimal `--validate-schema` CLI flag that can validate a given scale/mode/cadence JSON file against the schemas using JsonSchema.Net.
- Add documentation examples under docs/v0.3/rendering.md showing canonical scale/mode/cadence JSON examples.
