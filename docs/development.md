# Building, running, and testing

## Prerequisites

- .NET SDK 10.0 (all projects target `net10.0`, pinned in `Directory.Build.props`).
  Check with `dotnet --version`.

## Build

```sh
dotnet build Dadabe.slnx
```

Builds all four projects — `Dadabe.Core`, `Dadabe.Fretboard`, `Dadabe.Cli`,
`Dadabe.Editor` — and their test projects. `Directory.Build.props` sets
`TreatWarningsAsErrors`, so a clean build is the same bar CI holds.

To build a single project, point at its `.csproj` instead of the solution, e.g.
`dotnet build src/Dadabe.Editor/Dadabe.Editor.csproj`.

## Run

### CLI

```sh
dotnet run --project src/Dadabe.Cli -- voicings Cmaj7 --tuning DADABE --pretty
```

See the root [README](../README.md#build-and-run) for the full subcommand
reference (`voicings`, `predict`, `tuning`, `chord`) and schema pins.

### Editor

```sh
dotnet run --project src/Dadabe.Editor
```

Serves the Web Awesome UI at `http://localhost:5044` (see
`src/Dadabe.Editor/Properties/launchSettings.json` for the HTTPS profile).

- **Data location.** By default the Editor reads/writes JSON under
  `src/Dadabe.Editor/data/` — the same directory tracked in git, seeded with
  sample songs/progressions/predictions. Manual UI testing mutates those files
  in place (there's no seed/scratch separation yet — see `docs/v0.5.3/bugs.md`
  if you want to fix that). To point the Editor at a throwaway directory
  instead, set `DataRoot`:

  ```sh
  dotnet run --project src/Dadabe.Editor -- --DataRoot=/path/to/scratch-data
  ```

  or via an unprefixed `DataRoot` environment variable — it's read through
  the standard ASP.NET Core configuration pipeline (`DataStore.cs`), which
  binds command-line args and environment variables the same way.
- **Logs.** Request/response logging (console + `src/Dadabe.Editor/logs/`,
  git-ignored) is on by default — see `docs/v0.5.3/design.md` §4 for what's
  captured.

## Test

```sh
dotnet test Dadabe.slnx
```

Runs all four test projects (`Dadabe.Core.Tests`, `Dadabe.Fretboard.Tests`,
`Dadabe.Cli.Tests`, `Dadabe.Editor.Tests`).

To run one project or filter by name:

```sh
dotnet test tests/Dadabe.Fretboard.Tests/Dadabe.Fretboard.Tests.csproj
dotnet test tests/Dadabe.Core.Tests/Dadabe.Core.Tests.csproj --filter "FullyQualifiedName~ChordParserTests"
```

`tests/Directory.Build.props` configures shared test-project settings (e.g.
temp-directory cleanup for tests that touch the filesystem).

## Schema validation

CLI output can be validated against its schema inline:

```sh
dotnet run --project src/Dadabe.Cli -- voicings Cmaj7 --tuning DADABE --validate-schema
```

See the README's [Schemas](../README.md#schemas) table for which schema pins
which command's output.
