# generate-voicings.ps1

Small helper to extract chord tokens from any text file (log, chord sheet,
session notes) and run `dadabe voicings <chord> --pretty --out <outdir>` for
each unique chord found, then render a human-readable chart alongside the JSON.

## Canonical fixture

`scripts/fixtures/chords.txt` is the curated test input for this script.
It covers every grammar form defined in `ChordGrammar.json`, every modifier
kind (`b5 #5 b9 #9 #11 b13`), a spread of roots (natural, sharp, flat), and
the F#maj7/Gbmaj7 enharmonic pair that exercises the display-layer spelling
simplification (E#→F).

Run it — pick whichever form matches your setup:

```powershell
# Built binary (after dotnet build):
.\scripts\generate-voicings.ps1 `
    -Source    .\scripts\fixtures\chords.txt `
    -OutDir    .\out\voicings `
    -DadabeCmd '.\src\Dadabe.Cli\bin\Debug\net10.0\dadabe.exe'

# Via dotnet run (no build step needed):
.\scripts\generate-voicings.ps1 `
    -Source    .\scripts\fixtures\chords.txt `
    -OutDir    .\out\voicings `
    -DadabeCmd 'dotnet run --project .\src\Dadabe.Cli --'
```

Add `-WhatIf` to preview which commands would run without invoking `dadabe`.

## Usage examples

Dry-run against a log file (shows actions, does not invoke `dadabe`):

```powershell
.\scripts\generate-voicings.ps1 -Source .\logs\debug.log -OutDir .\out\voicings -WhatIf
```

Real run using `dadabe` from PATH:

```powershell
.\scripts\generate-voicings.ps1 -Source .\logs -OutDir .\out\voicings
```

`dadabe` not on PATH — call via `dotnet run`:

```powershell
.\scripts\generate-voicings.ps1 `
    -Source .\logs `
    -OutDir .\out\voicings `
    -DadabeCmd 'dotnet run --project .\src\Dadabe.Cli --'
```

## Options

| Parameter     | Default         | Description                                                                                                                                                  |
|---------------|-----------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `-Source`     | `.`             | File or directory to scan for chord tokens. Pass the fixture path or a log file. Directories are recursed for `*.txt *.log *.out *.dump` (excludes `bin/`, `obj/`, `.git/`). |
| `-OutDir`     | `C:\dadabe_out` | Base output directory. One subfolder per chord.                                                                                                              |
| `-DadabeCmd`  | `dadabe`        | Command to invoke the CLI. Supports compound forms like `dotnet run --project .\src\Dadabe.Cli --`.                                                          |
| `-Pattern`    | (chord regex)   | Regex used to extract tokens. Override if the source format needs it.                                                                                        |
| `-TopN`       | `0`             | Number of next-chord candidates (requires dadabe v0.2+).                                                                                                     |
| `-Entropy`    | `0.5`           | Softmax temperature for next-chord prediction.                                                                                                               |
| `-ChartLimit` | `5`             | Max voicings shown per chart file.                                                                                                                           |
| `-WhatIf`     |                 | Show planned actions without running `dadabe`.                                                                                                               |
| `-Force`      |                 | Overwrite existing per-chord output folders.                                                                                                                 |

## Notes

- Commands run sequentially. Add job-based parallelism via `-Throttle` if needed (not yet implemented).
- If the script finds no matching files in `-Source`, it falls back to the built-in chord set for quick smoke-testing.
- If chord extraction misses tokens, tweak `-Pattern` to match your source format.
