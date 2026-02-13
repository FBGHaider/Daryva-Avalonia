# Daryva - Release build from repo root
# Run this from the repository root folder, e.g.:
#   cd "C:\Users\Abbas Haider\Repo\Daryva"
#   .\build-release.ps1 -Version 1.0.3
#
# Or run with full path from anywhere:
#   & "C:\Users\Abbas Haider\Repo\Daryva\build-release.ps1" -Version 1.0.3

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [switch]$SkipInnoSetup
)

$ErrorActionPreference = "Stop"

# Resolve repo root: if this script is in the repo root, use it; else assume we're in repo root
$ThisScript = $MyInvocation.MyCommand.Path
$RepoRoot = if ($ThisScript) { Split-Path -Parent $ThisScript } else { Get-Location }
$BuildScript = Join-Path $RepoRoot "velopack-installer\build-win.ps1"

if (-not (Test-Path $BuildScript)) {
    Write-Host "Error: build script not found at $BuildScript" -ForegroundColor Red
    Write-Host "Run this script from the Daryva repository root (the folder that contains velopack-installer)." -ForegroundColor Yellow
    exit 1
}

Push-Location $RepoRoot
try {
    & $BuildScript -Version $Version -SkipInnoSetup:$SkipInnoSetup
    exit $LASTEXITCODE
} finally {
    Pop-Location
}
