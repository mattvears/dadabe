# Admin Editor

A companion tool to Dadabe for creating and managing the data files Dadabe consumes.

## Technology Stack

- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0)
- [htmx](https://four.htmx.org/)
- [beercss](https://www.beercss.com/)
- Razor templating via [RazorSlices](https://github.com/DamianEdwards/RazorSlices) for HTML fragment responses

## Project Location

`src/Dadabe.Editor/`

Single-user, local-only. No authentication.

---

## Data Storage

All data is stored as JSON files on the local filesystem under a configurable root directory (default: `data/` relative to the working directory).

```
data/
  progressions/
    {slug}.json          # progression.schema.json shape
  tunings/
    {slug}.json          # {name, strings[]} object
  predictions/
    {slug}.json          # prediction.schema.json shape (input only)
  reference/
    cadences/
      {slug}.json        # cadence.schema.json shape
    modes/
      {slug}.json        # mode.schema.json shape
    scales/
      {slug}.json        # scale.schema.json shape
    voicing-categories.json  # voicing-category.schema.json overlay (single file)
```

Slug rules: lowercase name, spaces replaced with hyphens, non-alphanumeric stripped. Duplicate slugs are rejected at creation time.

---

## Project Structure

```
src/Dadabe.Editor/
  Program.cs
  Services/
    DataStore.cs             # path resolution + generic JSON CRUD helpers
    ProgressionService.cs
    TuningService.cs
    PredictionService.cs
    ReferenceService.cs
  Routes/
    ProgressionRoutes.cs
    TuningRoutes.cs
    PredictionRoutes.cs
    ReferenceRoutes.cs
  Slices/                    # RazorSlices .cshtml fragments
    Shared/
      _Layout.cshtml
      _Nav.cshtml
    Dashboard.cshtml
    Progressions/
      _List.cshtml
      _Row.cshtml
      _Form.cshtml
    Tunings/
      _List.cshtml
      _Row.cshtml
      _Form.cshtml
    Predictions/
      _List.cshtml
      _Row.cshtml
      _Form.cshtml
      _Result.cshtml
    Reference/
      _CadenceList.cshtml
      _ModeList.cshtml
      _ScaleList.cshtml
      _Form.cshtml
  wwwroot/
    app.js                   # minimal htmx config / helpers
  Dadabe.Editor.csproj
```

---

## Pages

### Dashboard (`GET /`)

Four stat cards: Progressions · Predictions · Tunings · Reference items.
Each card has a count and a "New" quick-link. Links to each section.

---

### Progressions (`/progressions`)

**List view**: table — Name | Chords | Tempo | Actions (Edit, Delete).

**Create / Edit form** (rendered in a beercss dialog):
- `name` — text input (becomes the slug)
- `chords` — dynamic list: type a chord symbol, press Add; drag-to-reorder; ✕ to remove
- `tempo` — optional integer field

Saved file shape matches `schemas/progression.schema.json`.

---

### Prediction Requests (`/predictions`)

**List view**: table — Name | Chord | Filters | Actions (Edit, Delete, **Run**).

**Create / Edit form** (dialog):
- `name` — text input
- `chord` — chord symbol (e.g. `Dm7`)
- `context` — optional dropdown of saved progressions (maps to `progression.schema.json`)
- `filters` — add zero or more filters:
  - `byChord` → params: `chord` (string)
  - `byQuality` → params: `quality` (string)
- `maxResults` — integer 1–50 (default 10)
- `entropy` — number 0.01–10.0 (optional; leaves env default in effect if blank)

Saved file shape matches `schemas/prediction.schema.json`.

**Run**: clicking Run on a list row `hx-post`s to `/api/predictions/{slug}/run`, which
invokes `dadabe predict --input <file>` as a subprocess and streams the result back as
an HTML fragment swapped into a result panel below the table.

---

### Tunings (`/tunings`)

**List view**: table — Name | Strings | Actions (Edit, Delete).

**Create / Edit form** (dialog):
- `name` — text input
- `strings` — dynamic list of note names (e.g. `E2`, `A2`, `D3` …); Add / Remove buttons

Saved file shape: `{ "name": "...", "strings": ["E2", "A2", ...] }`.

---

### Reference Data (`/reference`)

Sub-navigation tabs: Cadences · Modes · Scales · Voicing Categories.

#### Cadences

Table — Type | Resolution | Example Progression | Actions.

Form fields: `type` (select: authentic / plagal / deceptive / half), `resolution` (text),
`exampleProgression` (chord list, same dynamic widget as progressions), `description` (textarea).

#### Modes

Table — Name | Parent Scale | Degree | Intervals | Actions.

Form fields: `name`, `parentScale` (text), `degreeIndex` (integer), `intervals`
(comma-separated semitone list), `noteNames` (comma-separated, optional), `description`.

#### Scales

Table — Name | Notes | Intervals | Mode Of | Actions.

Form fields: `name`, `notes` (comma-separated), `intervals` (comma-separated), `modeOf` (optional text), `description`.

#### Voicing Category Overlay

Single file (`data/reference/voicing-categories.json`). Rendered as a syntax-highlighted
`<textarea>` with a Save button. Validated against `schemas/voicing-category.schema.json`
on save; errors shown inline.

---

## API Routes

All routes return HTML fragments for htmx. Form submissions use `application/x-www-form-urlencoded`.

```
GET  /                                   → Dashboard page
GET  /progressions                       → Progressions page
GET  /predictions                        → Predictions page
GET  /tunings                            → Tunings page
GET  /reference                          → Reference page (defaults to Cadences tab)

GET    /api/progressions                 → _List fragment
POST   /api/progressions                 → create → _List fragment (or error)
GET    /api/progressions/{slug}          → _Form fragment (pre-filled)
PUT    /api/progressions/{slug}          → update → _Row fragment
DELETE /api/progressions/{slug}          → 200 empty (row removed via hx-swap)

GET    /api/predictions                  → _List fragment
POST   /api/predictions                  → create → _List fragment
GET    /api/predictions/{slug}           → _Form fragment
PUT    /api/predictions/{slug}           → update → _Row fragment
DELETE /api/predictions/{slug}           → 200 empty
POST   /api/predictions/{slug}/run       → _Result fragment (prediction output or error)

GET    /api/tunings                      → _List fragment
POST   /api/tunings                      → create → _List fragment
GET    /api/tunings/{slug}               → _Form fragment
PUT    /api/tunings/{slug}               → update → _Row fragment
DELETE /api/tunings/{slug}               → 200 empty

GET    /api/reference/cadences           → _CadenceList fragment
POST   /api/reference/cadences           → create → fragment
GET    /api/reference/cadences/{slug}    → _Form fragment
PUT    /api/reference/cadences/{slug}    → update → row fragment
DELETE /api/reference/cadences/{slug}    → 200 empty

(same pattern for /api/reference/modes and /api/reference/scales)

GET    /api/reference/voicing-categories     → raw JSON in textarea fragment
PUT    /api/reference/voicing-categories     → validate + save → result fragment
```

---

## Key UX Patterns

**CRUD flow** (consistent across all sections):

1. "New" button → `hx-get` the `_Form` fragment → beercss `<dialog>` opens.
2. Submit → `hx-post` → on success, dialog closes and list reloads (or new row appended).
3. Edit row → `hx-get /api/{resource}/{slug}` → same dialog, pre-filled.
4. Delete row → `hx-delete` with `hx-confirm` → row removed with `hx-swap="outerHTML"` targeting the `<tr>`.

**Run prediction**:

1. "Run" button on a prediction row → `hx-post /api/predictions/{slug}/run` with `hx-target="#result-panel"`.
2. Result panel below the table swaps in either a formatted result (chord table + scores) or an error message.
3. A "Clear" link resets the panel.

**Chord list widget**:

Reusable htmx pattern used by Progressions, Cadences, and the prediction context field:
- Text input + "Add" button → `hx-post` to a micro-endpoint that returns a new `<li>` appended to the list.
- Each `<li>` has a ✕ button that removes itself with `hx-delete` / `hx-swap="outerHTML"`.
- Hidden inputs carry the ordered list to the form on submit.

---

## v0 Scope

| Feature | Included |
|---|---|
| Progressions CRUD | ✓ |
| Tunings CRUD | ✓ |
| Prediction requests CRUD | ✓ |
| Run prediction (shell out to CLI) | ✓ |
| Reference data CRUD (cadences, modes, scales) | ✓ |
| Voicing category overlay (raw JSON editor) | ✓ |
| Schema validation on save | cadences / modes / scales only; voicing-categories only |
| Chord symbol validation | client-side regex only |

---

## Deferred

- Schema validation for progressions and tunings on save.
- Chord symbol server-side validation (shell out to `dadabe chord`).
- Import existing JSON files by drag-and-drop.
- Export / bundle: package a set of items into a zip.
- Progression playback or audio preview.
- Bass note enforcement in voicing search (tracked as Q4 in v0.4 design).
