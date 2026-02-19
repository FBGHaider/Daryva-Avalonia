param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Local','Api')]
    [string]$Mode,
    [switch]$Restart
)

$ErrorActionPreference = 'Stop'

$configPath = Join-Path $env:APPDATA 'Daryva\app.config.local.json'

if (-not (Test-Path $configPath)) {
    throw "Config not found: $configPath"
}

$json = Get-Content $configPath -Raw | ConvertFrom-Json
if (-not $json.AppSettings) {
    $json | Add-Member -NotePropertyName AppSettings -NotePropertyValue (@{})
}

if ($json.AppSettings.PSObject.Properties.Name -contains 'DataMode') {
    $json.AppSettings.DataMode = $Mode
}
else {
    $json.AppSettings | Add-Member -NotePropertyName DataMode -NotePropertyValue $Mode
}

$json | ConvertTo-Json -Depth 20 | Set-Content $configPath -Encoding UTF8
Write-Host "DataMode set to '$Mode' in $configPath" -ForegroundColor Green

if ($Restart) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $restartScript = Join-Path $repoRoot 'Scripts\restart-dev.ps1'
    if (Test-Path $restartScript) {
        powershell -ExecutionPolicy Bypass -File $restartScript
    }
}
