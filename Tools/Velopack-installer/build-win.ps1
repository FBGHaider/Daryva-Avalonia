# Daryva - Windows build and Velopack pack script
# Prerequisites: .NET 8 SDK, vpk tool (dotnet tool install -g vpk)
# Optional: Inno Setup 6 (for branded installer with logo, terms, destination picker)
# Output: ../artifacts/win-x64, ../releases/

param(
    [string]$Version = "1.0.0",
    [switch]$SkipInnoSetup
)

# Allow "v1.0.12" or "1.0.12" (NuGet/dotnet require version without "v" prefix)
if ($Version -match '^v(.+)$') { $Version = $Matches[1] }

# Velopack requires a 3-part SemVer2 version (e.g. 1.0.13). Normalize 1.0.13.1 -> 1.0.13
$parts = $Version -split '\.'
if ($parts.Count -gt 3) {
    $Version = $parts[0..2] -join '.'
    Write-Host "Version normalized to 3-part SemVer for Velopack: $Version" -ForegroundColor Yellow
}

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$RepoRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$ProjectDir = Join-Path $RepoRoot "src\Daryva.UI"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$ReleasesDir = Join-Path $RepoRoot "releases"
$PublishDir = Join-Path $ArtifactsDir "win-x64"
$InstallerAssets = Join-Path $ScriptDir "installer-assets"
$IconPath = Join-Path $ProjectDir "Assets\Logo\Daryva_icon.ico"

Write-Host "Building Daryva for Windows (win-x64) v$Version..." -ForegroundColor Cyan

# Clean first to ensure fresh build (avoids stale Views/UI in installer)
Write-Host "Cleaning project..." -ForegroundColor Cyan
dotnet clean $ProjectDir -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Remove old publish output so installer always gets latest code (incl. Updates panel in Settings)
if (Test-Path $PublishDir) {
    Write-Host "Removing old artifacts..." -ForegroundColor Cyan
    Remove-Item $PublishDir -Recurse -Force
}

# Publish (fresh build - installer will use this output)
dotnet publish $ProjectDir -c Release -r win-x64 --self-contained -o $PublishDir -p:Version=$Version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Velopack pack (with icon and splash for Setup.exe branding)
Write-Host "Packaging with Velopack..." -ForegroundColor Cyan
# No --splashImage: our Inno Setup wizard is the UI; Velopack runs silently in background
$vpkArgs = @(
    "--packId", "FBGHaider.Daryva",
    "--packVersion", $Version,
    "--packDir", $PublishDir,
    "--mainExe", "Daryva.exe",
    "--outputDir", $ReleasesDir,
    "--packTitle", "Daryva",
    "--icon", $IconPath
)
& vpk pack @vpkArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Branded Inno Setup installer (optional: logo, terms, destination picker)
if (-not $SkipInnoSetup) {
    $nupkg = Get-ChildItem $ReleasesDir -Filter "*-full.nupkg" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $nupkg) {
        Write-Host "Error: No *-full.nupkg found in $ReleasesDir. Run full build first (vpk pack creates it)." -ForegroundColor Red
        exit 1
    }
    $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $iscc)) { $iscc = "${env:ProgramFiles}\Inno Setup 6\ISCC.exe" }
    if (Test-Path $iscc) {
        Write-Host "Creating branded installer (Inno Setup)..." -ForegroundColor Cyan
        Push-Location $ScriptDir
        try {
            & $iscc "/DMyAppVersion=$Version" "daryva.iss"
            if ($LASTEXITCODE -eq 0) {
                $brandedInstaller = Join-Path $ReleasesDir "Daryva-Setup-$Version.exe"
                $defaultSetup = Join-Path $ReleasesDir "FBGHaider.Daryva-win-Setup.exe"
                $rawVelopackSetupBackup = Join-Path $ReleasesDir "FBGHaider.Daryva-win-VelopackSetup.exe"

                if (Test-Path $defaultSetup) {
                    Copy-Item $defaultSetup $rawVelopackSetupBackup -Force
                }

                if (Test-Path $brandedInstaller) {
                    Copy-Item $brandedInstaller $defaultSetup -Force
                }

                Write-Host "Branded installer: $brandedInstaller" -ForegroundColor Green
                Write-Host "Default setup (wizard): $defaultSetup" -ForegroundColor Green
                Write-Host "Raw Velopack setup backup: $rawVelopackSetupBackup" -ForegroundColor DarkGray
            } else {
                throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
            }
        } finally { Pop-Location }
    } else {
        Write-Host "Inno Setup not found. Run with -SkipInnoSetup to suppress. Install from https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
    }
}

Write-Host "Done. Output: $ReleasesDir" -ForegroundColor Green
