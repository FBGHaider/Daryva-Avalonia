param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [string]$DevUser = "dev-user-1",
    [string]$LocalDbPath = "$env:APPDATA\Daryva\Database\DaryvaDB.db",
    [string]$OrgId
)

$ErrorActionPreference = "Stop"

function Normalize-Key {
    param(
        [string]$AddressLine1,
        [string]$City,
        [string]$Postcode
    )

    $a = if ($null -eq $AddressLine1) { "" } else { "$AddressLine1" }
    $c = if ($null -eq $City) { "" } else { "$City" }
    $p = if ($null -eq $Postcode) { "" } else { "$Postcode" }

    $a = $a.Trim().ToLowerInvariant()
    $c = $c.Trim().ToLowerInvariant()
    $p = $p.Trim().Replace(" ", "").ToLowerInvariant()
    return "$a|$c|$p"
}

if (-not (Test-Path $LocalDbPath)) {
    throw "Local DB not found: $LocalDbPath"
}

if (-not (Get-Command sqlite3 -ErrorAction SilentlyContinue)) {
    throw "sqlite3 executable is not available in PATH."
}

if ([string]::IsNullOrWhiteSpace($OrgId)) {
    throw "OrgId is required. Pass the current cutover org id."
}

$headers = @{ "X-Dev-User" = $DevUser; "X-Org-Id" = $OrgId }

$health = Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + "/health") -Method Get
if ($health.status -ne "healthy") {
    throw "API health check failed."
}

$localRowsJson = & sqlite3 $LocalDbPath -json @"
SELECT AddressLine1, City, Postcode, TotalRooms
FROM House;
"@

$localRows = @()
if (-not [string]::IsNullOrWhiteSpace($localRowsJson)) {
    $parsed = $localRowsJson | ConvertFrom-Json
    if ($parsed -is [System.Array]) { $localRows = $parsed } else { $localRows = @($parsed) }
}

$apiHouses = Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + "/api/houses") -Headers $headers -Method Get

$localByKey = @{}
foreach ($row in $localRows) {
    $key = Normalize-Key -AddressLine1 "$($row.AddressLine1)" -City "$($row.City)" -Postcode "$($row.Postcode)"
    $localByKey[$key] = [int]$row.TotalRooms
}

$updated = 0
$skipped = 0
$missing = 0

foreach ($house in $apiHouses) {
    $key = Normalize-Key -AddressLine1 "$($house.addressLine1)" -City "$($house.city)" -Postcode "$($house.postcode)"

    if (-not $localByKey.ContainsKey($key)) {
        $missing++
        continue
    }

    $targetRooms = [int]$localByKey[$key]
    $currentRooms = [int]$house.totalRooms

    if ($currentRooms -eq $targetRooms) {
        $skipped++
        continue
    }

    $body = @{ totalRooms = $targetRooms } | ConvertTo-Json
    Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + "/api/houses/$($house.id)") -Headers $headers -Method Put -ContentType "application/json" -Body $body | Out-Null
    $updated++
}

Write-Host "House rooms sync complete." -ForegroundColor Green
Write-Host "Updated: $updated"
Write-Host "Already matching: $skipped"
Write-Host "No local match: $missing"
