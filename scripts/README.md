# generate-voicings.ps1

Small helper to extract chord tokens from debug/log output and run the
`dadabe voicings <chord> --pretty --out <outdir>` command for each unique chord.

Usage examples

- Dry-run (shows actions, does not invoke `dadabe`):

```
powershell
.\scripts\generate-voicings.ps1 -Source .\logs\debug.log -OutDir .\out\voicings -WhatIf
```

- Real run using `dadabe` from PATH:

```
powershell
.\scripts\generate-voicings.ps1 -Source .\logs -OutDir .\out\voicings
```

- If `dadabe` is not on PATH, call via `dotnet run` (example):

```
powershell
.\scripts\generate-voicings.ps1 -Source .\logs -OutDir .\out\voicings -DadabeCmd 'dotnet run --project .\src\Dadabe.Cli --'
```

Options

- `-Source` (required): File or directory to scan for chord tokens.
- `-OutDir` (required): Base output directory; a folder will be created per chord.
- `-DadabeCmd`: Command to invoke the CLI (default `dadabe`).
- `-Pattern`: Regex used to extract chord tokens (override if needed).
- `-WhatIf`: Shows planned actions without running `dadabe`.
- `-Force`: Overwrite existing per-chord output folders.

Notes

- The script runs commands sequentially. If you need parallel execution I can add job-based parallelism.
- If the chord extraction misses tokens, tweak `-Pattern` to match your log format.
