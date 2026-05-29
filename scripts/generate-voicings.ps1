<#
.SYNOPSIS
Extract chords from debug output and run `dadabe voicings` for each chord,
placing results into one folder per chord shape.

.DESCRIPTION
This script scans a text file or directory of text files (logs/debug output),
extracts chord tokens using a regex (customizable), de-duplicates them, and
invokes `dadabe voicings <chord> --pretty [--next-chords] --out <outdir>` for
each chord. After each voicings run it renders a human-readable chart file
alongside the JSON.

When -TopN is greater than 0 the voicings command also receives
--top-n <TopN> --entropy <Entropy>, enabling next-chord prediction output.
Requires dadabe v0.2 or later.

.EXAMPLE
  # Fallback (predefined chord set) with next-chord prediction:
  .\scripts\generate-voicings.ps1 -Source C:\empty -OutDir .\voicings -TopN 5 -Entropy 0.5

  # From a log file:
  .\scripts\generate-voicings.ps1 -Source .\logs\session.log -OutDir .\voicings -TopN 3

Parameters:
  -Source      Path to a file or directory to scan for chord tokens.
  -OutDir      Directory where output files will be created.
  -DadabeCmd   Command to invoke the dadabe CLI (default: 'dadabe').
  -Pattern     Regex pattern to locate chord tokens.
  -TopN        Number of next-chord candidates (0 = disabled). Requires v0.2.
  -Entropy     Softmax temperature for next-chord prediction (ignored when TopN is 0).
  -ChartLimit  Max voicings shown per chart file (default: 5).
  -Throttle    Max concurrent jobs (not implemented; runs sequentially).
  -WhatIf      Show what would be done without running commands.
  -Force       Overwrite existing output.
#>

param(
    [string]$Source = '.',
    [string]$OutDir = 'C:\\dadabe_out',
    [string]$DadabeCmd = 'dadabe',
    [string]$Pattern = '([A-G](?:#|b)?(?:maj|min|m|dim|aug|sus|add)?\d*[A-Za-z0-9#b/\-+_]*)',
    [int]$TopN = 0,
    [double]$Entropy = 0.5,
    [int]$ChartLimit = 5,
    [int]$Throttle = 4,
    [switch]$WhatIf,
    [switch]$Force
)

# ─── helpers ────────────────────────────────────────────────────────────────

function Get-TextFilesFromSource {
    param([string]$Path)
    if (Test-Path $Path -PathType Leaf) { return ,(Get-Item $Path) }
    if (Test-Path $Path -PathType Container) {
        return Get-ChildItem -Path $Path -Recurse -File `
            -Include *.txt,*.log,*.out,*.dump -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|\.remember)\\' }
    }
    throw "Source path '$Path' does not exist."
}

function Sanitize-Name {
    param([string]$Name)
    $invalid = [IO.Path]::GetInvalidFileNameChars() + [IO.Path]::GetInvalidPathChars()
    $s = $Name
    foreach ($c in $invalid) { $s = $s -replace [Regex]::Escape($c), '_' }
    $s = $s -replace '\s+', '_'
    return $s.Trim('_')
}

# Build argument list for a `dadabe voicings` call.
function Get-VoicingsArgs {
    param([string]$Chord, [string]$Tuning, [string]$OutPath)
    $a = @('voicings', $Chord)
    if ($Tuning) { $a += @('--tuning', $Tuning) }
    if ($TopN -gt 0) { $a += @('--top-n', $TopN, '--entropy', $Entropy) }
    $a += @('--pretty', '--out', $OutPath)
    return $a
}

function Invoke-Dadabe {
    param([string[]]$Arguments, [string]$Label)
    Write-Host "  dadabe $($Arguments -join ' ')"
    if ($WhatIf) { return }
    try {
        $proc = Start-Process -FilePath $DadabeCmd -ArgumentList $Arguments `
            -NoNewWindow -Wait -PassThru -ErrorAction Stop
        if ($proc.ExitCode -ne 0) {
            Write-Warning "[$Label] Exited with code $($proc.ExitCode)"
        }
    }
    catch { Write-Warning "[$Label] Failed: $_" }
}

# Render a human-readable chart from a voicings JSON file.
function Render-ChordChart {
    param([string]$JsonFile, [string]$ChartFile, [int]$Limit)

    if (-not (Test-Path $JsonFile)) {
        Write-Warning "Chart skipped (JSON not found): $JsonFile"; return
    }
    try { $d = Get-Content $JsonFile -Raw | ConvertFrom-Json }
    catch { Write-Warning "Chart skipped (parse error) $JsonFile : $_"; return }

    $data     = $d.data
    $chord    = $data.chord
    $tuning   = $data.tuning
    $voicings = $data.voicings
    $nextChords = $data.nextChords

    $sb = [System.Text.StringBuilder]::new()

    # ── header ───────────────────────────────────────────────────────────
    $tones = ($chord.pitchClasses | ForEach-Object { $_.name }) -join '  '
    [void]$sb.AppendLine("$($chord.symbol)  [$($tuning.name)]")
    [void]$sb.AppendLine("Tones:  $tones")
    [void]$sb.AppendLine()

    # ── next-chord bar chart ─────────────────────────────────────────────
    if ($nextChords -and $nextChords.Count -gt 0) {
        $barWidth = 24
        [void]$sb.AppendLine('Next chords:')
        foreach ($nc in $nextChords) {
            $pct    = [int][math]::Round($nc.probability * 100)
            $filled = [int][math]::Round($nc.probability * $barWidth)
            $empty  = $barWidth - $filled
            $bar    = ('=' * $filled) + (' ' * $empty)
            $sym    = $nc.symbol.PadRight(8)
            [void]$sb.AppendLine("  $sym  $($pct.ToString().PadLeft(3))%  |$bar|")
        }
        [void]$sb.AppendLine()
    }

    # ── voicings ─────────────────────────────────────────────────────────
    $total = $voicings.Count
    $shown = [math]::Min($Limit, $total)
    [void]$sb.AppendLine(('-' * 52))
    [void]$sb.AppendLine("$total voicing(s), showing $shown")

    for ($i = 0; $i -lt $shown; $i++) {
        $v       = $voicings[$i]
        $comfort = [int][math]::Round($v.comfort * 100)
        $label   = "#$($i+1)  $($v.structure)".PadRight(28)
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("  $label comfort: $comfort%")

        foreach ($pos in $v.positions) {
            $strName = $tuning.strings[$pos.string]
            if ($pos.muted -eq $true) {
                $fretMark = 'x'
                $detail   = ''
            }
            elseif ($pos.fret -eq 0) {
                $fretMark = '0'
                $detail   = if ($pos.function) { "  ($($pos.function)) open" } else { '' }
            }
            else {
                $fretMark = "$($pos.fret)"
                $detail   = if ($pos.function) { "  ($($pos.function))" } else { '' }
            }
            [void]$sb.AppendLine("    $($strName.PadRight(3)) | $($fretMark.PadLeft(2))$detail")
        }
    }

    if ($total -gt $shown) {
        [void]$sb.AppendLine()
        [void]$sb.AppendLine("  ... $($total - $shown) more voicing(s) in the JSON file.")
    }
    [void]$sb.AppendLine()

    Set-Content -Path $ChartFile -Value $sb.ToString() -Encoding UTF8
    Write-Host "  chart  -> $ChartFile"
}

# ─── main ───────────────────────────────────────────────────────────────────

Write-Host "Scanning source: $Source"
if ($TopN -gt 0) {
    Write-Host "Next-chord prediction: top-n=$TopN  entropy=$Entropy"
}
Write-Host "Chart limit: $ChartLimit voicing(s) per chart"
Write-Host ''

if (-not (Test-Path $OutDir)) {
    Write-Host "Creating output root: $OutDir"
    if (-not $WhatIf) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }
}

$files = Get-TextFilesFromSource -Path $Source
if ($files.Count -eq 0) {
    Write-Host "No input files found in '$Source'. Using predefined chord set (DADABE)."
    Write-Host ''

    $chords = @('C','Am','G','D','E','F','Bm','Em','Cmaj7','G7','D7','A7','Eadd9','Dsus4','F#dim','C#m7b5','F#maj7')

    $tuningFile = Join-Path $OutDir 'tuning-DADABE.json'
    Write-Host '[tuning]'
    Invoke-Dadabe -Arguments @('tuning','DADABE','--pretty','--out',$tuningFile) -Label 'tuning'
    Write-Host ''

    foreach ($chord in $chords) {
        $safe        = Sanitize-Name -Name $chord
        $chordFile   = Join-Path $OutDir "chord-$safe.json"
        $voicingsFile = Join-Path $OutDir "voicings-$safe.json"
        $chartFile   = Join-Path $OutDir "chart-$safe.txt"

        Write-Host "[$chord]"
        Invoke-Dadabe -Arguments @('chord',$chord,'--pretty','--out',$chordFile) -Label "chord/$chord"
        Invoke-Dadabe -Arguments (Get-VoicingsArgs -Chord $chord -Tuning 'DADABE' -OutPath $voicingsFile) -Label "voicings/$chord"
        if (-not $WhatIf) {
            Render-ChordChart -JsonFile $voicingsFile -ChartFile $chartFile -Limit $ChartLimit
        }
        Write-Host ''
    }

    Write-Host "Done. Output: $OutDir"
    exit 0
}

# ─── source-file path ────────────────────────────────────────────────────────

$regex = New-Object System.Text.RegularExpressions.Regex($Pattern)
$found = [System.Collections.Generic.HashSet[string]]::new()

foreach ($f in $files) {
    Write-Host "Reading: $($f.FullName)"
    try { $text = Get-Content -Raw -ErrorAction Stop -Path $f.FullName }
    catch { Write-Warning "Failed to read $($f.FullName): $_"; continue }
    foreach ($m in $regex.Matches($text)) {
        $token = $m.Groups[1].Value.Trim()
        if (-not [string]::IsNullOrWhiteSpace($token)) { [void]$found.Add($token) }
    }
}

if ($found.Count -eq 0) { Write-Error "No chord tokens found using: $Pattern"; exit 1 }
Write-Host "Found $($found.Count) unique chord tokens."
Write-Host ''

foreach ($chord in $found) {
    $safe = Sanitize-Name -Name $chord
    $dest = Join-Path -Path $OutDir -ChildPath $safe

    if (Test-Path $dest) {
        if ($Force) {
            Write-Host "Removing: $dest"
            if (-not $WhatIf) { Remove-Item -Recurse -Force -LiteralPath $dest }
        }
        else { Write-Host "Skipping (exists): $dest"; continue }
    }

    Write-Host "[$chord]"
    if (-not $WhatIf) { New-Item -ItemType Directory -Force -Path $dest | Out-Null }

    $voicingsFile = Join-Path $dest 'voicings.json'
    $chartFile    = Join-Path $dest 'chart.txt'

    Invoke-Dadabe -Arguments (Get-VoicingsArgs -Chord $chord -Tuning '' -OutPath $voicingsFile) -Label "voicings/$chord"
    if (-not $WhatIf) {
        Render-ChordChart -JsonFile $voicingsFile -ChartFile $chartFile -Limit $ChartLimit
    }
    Write-Host ''
}

Write-Host "Done. Output: $OutDir"
