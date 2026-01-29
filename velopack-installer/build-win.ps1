# Daryva - Windows build and Velopack pack script
# Prerequisites: .NET 8 SDK, vpk tool (dotnet tool install -g vpk)
# Output: ../artifacts/win-x64, ../releases/

param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot "Daryva-Avalonia"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$ReleasesDir = Join-Path $RepoRoot "releases"
$PublishDir = Join-Path $ArtifactsDir "win-x64"

Write-Host "Building Daryva for Windows (win-x64) v$Version..." -ForegroundColor Cyan

# Publish
dotnet publish $ProjectDir -c Release -r win-x64 --self-contained -o $PublishDir -p:Version=$Version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Velopack pack
Write-Host "Packaging with Velopack..." -ForegroundColor Cyan
vpk pack --packId FBGHaider.Daryva --packVersion $Version --packDir $PublishDir --mainExe Daryva.exe --outputDir $ReleasesDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Done. Output: $ReleasesDir" -ForegroundColor Green
