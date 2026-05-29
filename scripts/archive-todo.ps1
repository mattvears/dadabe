<#
Archive docs\todo.md into docs\v{version}\source\todo-full-{date}.md and replace with a concise index.
Usage:
  .\scripts\archive-todo.ps1 -Version 0.2
  .\scripts\archive-todo.ps1 -Version 0.2 -Force -Push -Tag

Options:
  -Version  (string)  Required. Target version (e.g. 0.2).
  -Force    (switch)  Proceed even if size/lines under thresholds.
  -MaxLines (int)     Threshold lines (default 200).
  -MaxBytes (int)     Threshold bytes (default 20480 = 20KB).
  -Push     (switch)  Attempt to git push origin HEAD after commit.
  -Tag      (switch)  Create annotated git tag v{version} and push it.
#>
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [switch]$Force,
    [int]$MaxLines = 200,
    [int]$MaxBytes = 20480,
    [switch]$Push,
    [switch]$Tag
)

$todoPath = "docs\todo.md"
if (-not (Test-Path $todoPath)) {
    Write-Error "$todoPath not found."
    exit 1
}

$info = Get-Item $todoPath
$size = $info.Length
$lines = (Get-Content $todoPath -Raw).Split("`n").Length

Write-Host "todo.md lines: $lines, bytes: $size"
if (-not $Force) {
    if ($lines -le $MaxLines -and $size -le $MaxBytes) {
        Write-Host "Under thresholds (MaxLines=$MaxLines, MaxBytes=$MaxBytes). Use -Force to override. Exiting."
        exit 0
    }
}

$date = Get-Date -Format yyyy-MM-dd
$archiveDir = "docs\v$Version\source"
if (-not (Test-Path $archiveDir)) { New-Item -ItemType Directory -Force -Path $archiveDir | Out-Null }
$archiveName = "todo-full-$date.md"
$archivePath = Join-Path $archiveDir $archiveName

# Prefer git mv when available
$useGit = $false
try {
    git --version > $null 2>&1
    $useGit = $true
} catch {
    $useGit = $false
}

if ($useGit) {
    git mv $todoPath $archivePath
} else {
    Move-Item -Path $todoPath -Destination $archivePath -Force
}

# Build concise replacement
$archiveLines = Get-Content $archivePath -Raw -ErrorAction Stop | Out-String
$allLines = $archiveLines -split "`n"

# Gather decision table rows (lines starting with "| D")
$decisions = $allLines | Where-Object { $_ -match '^\|\s*D\d+' } | Select-Object -First 20
# Gather top-level checkbox lines under Work breakdown
$checkboxes = $allLines | Where-Object { $_ -match '^[\s\-]*-\s*\[[ xX]\]' } | Select-Object -First 20

$concise = @()
$concise += "# Dadabe v$Version - TODO (concise)"
$concise += ""
$concise += "Archived full TODO: $archivePath"
$concise += ""
$concise += "Short context: The full todo was archived to keep docs/todo.md concise. See the archived file for the full history and rationale."
$concise += ""
$concise += "## Top decisions (excerpt)"
if ($decisions.Count -gt 0) {
    $concise += "| # | Topic | Decision"
    $concise += "| --- | --- | ---"
    foreach ($d in $decisions) { $concise += $d }
} else {
    $concise += "(no decision table excerpt found in archive)"
}
$concise += ""
$concise += "## Current work (excerpt of checkboxes)"
if ($checkboxes.Count -gt 0) {
    foreach ($c in $checkboxes) { $concise += $c }
} else {
    $concise += "(no checkboxes found in archive)"
}
$concise += ""
$concise += "## Links"
$concise += "- Full archived TODO: $archivePath"
$concise += ""
$concise += "(Edit this concise file to add the most important top-level todos; keep it under $MaxLines lines.)"

$concise -join "`n" | Out-File -FilePath $todoPath -Encoding utf8

# Stage & commit
if ($useGit) {
    git add $todoPath $archivePath
    $commitMsg = "docs: archive todo.md -> $archivePath"
    git commit -m "$commitMsg" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
    if ($Push) {
        try { git push origin HEAD } catch { Write-Warning "git push failed: $_" }
    }
    if ($Tag) {
        $tagName = "v$Version"
        try {
            git tag -a $tagName -m "Release $tagName"
            git push origin $tagName
        } catch { Write-Warning "git tag/push failed: $_" }
    }
} else {
    Write-Host "Git not available; files moved locally. Stage & commit manually if desired."
}

Write-Host "Archived $todoPath -> $archivePath and replaced with concise TODO."