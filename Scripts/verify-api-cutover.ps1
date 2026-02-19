param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [string]$DevUser = "dev-user-1",
    [string]$OrgId,
    [string]$LocalDbPath = "$env:APPDATA\Daryva\Database\DaryvaDB.db"
)

$ErrorActionPreference = "Stop"

function Invoke-ApiJson {
    param(
        [string]$Path,
        [string]$OrgHeader
    )

    $headers = @{ "X-Dev-User" = $DevUser }
    if (-not [string]::IsNullOrWhiteSpace($OrgHeader)) {
        $headers["X-Org-Id"] = $OrgHeader
    }

    return Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + $Path) -Headers $headers -Method Get
}

function Get-Count {
    param($obj)
    return (To-ApiArray $obj).Count
}

function To-ApiArray {
    param($obj)

    if ($null -eq $obj) { return @() }

    if ($obj -is [System.Array]) { return $obj }

    if ($obj.PSObject -and ($obj.PSObject.Properties.Name -contains 'value') -and $obj.value -is [System.Array]) {
        return $obj.value
    }

    return @($obj)
}

if (-not (Test-Path $LocalDbPath)) {
    throw "Local DB not found: $LocalDbPath"
}

if (-not (Get-Command sqlite3 -ErrorAction SilentlyContinue)) {
    throw "sqlite3 not found in PATH"
}

$localCountsRaw = & sqlite3 $LocalDbPath "SELECT 'houses', COUNT(*) FROM House UNION ALL SELECT 'tenants', COUNT(*) FROM Tenant UNION ALL SELECT 'tenancies', COUNT(*) FROM Tenancy UNION ALL SELECT 'rentPayments', COUNT(*) FROM RentPayment UNION ALL SELECT 'depositPayments', COUNT(*) FROM DepositPayment UNION ALL SELECT 'expenses', COUNT(*) FROM HouseExpense UNION ALL SELECT 'documents', COUNT(*) FROM Document;"
$localSumsRaw = & sqlite3 $LocalDbPath "SELECT 'rentAmount', IFNULL(SUM(AmountPaid),0) FROM RentPayment UNION ALL SELECT 'depositAmount', IFNULL(SUM(AmountPaid),0) FROM DepositPayment;"

$local = @{}
foreach ($line in $localCountsRaw) {
    $parts = $line -split "\|"
    $local[$parts[0]] = [int]$parts[1]
}
foreach ($line in $localSumsRaw) {
    $parts = $line -split "\|"
    $local[$parts[0]] = [decimal]$parts[1]
}
$local["transactions"] = $local["rentPayments"] + $local["depositPayments"]

if ([string]::IsNullOrWhiteSpace($OrgId)) {
    $orgResponse = Invoke-ApiJson -Path "/api/orgs" -OrgHeader ""
    $orgs = if ($orgResponse.value) { $orgResponse.value } else { $orgResponse }

    if (-not $orgs -or (Get-Count $orgs) -eq 0) {
        throw "No organizations available for dev user '$DevUser'."
    }

    $bestScore = -1
    $bestOrgId = $null
    foreach ($org in $orgs) {
        $oid = "$($org.id)"
        $score = 0
        $score += Get-Count (Invoke-ApiJson -Path "/api/houses" -OrgHeader $oid)
        $score += Get-Count (Invoke-ApiJson -Path "/api/tenants?includeArchived=true" -OrgHeader $oid)
        $score += Get-Count (Invoke-ApiJson -Path "/api/tenancies" -OrgHeader $oid)
        $score += Get-Count (Invoke-ApiJson -Path "/api/payments/transactions?paymentType=All" -OrgHeader $oid)

        if ($score -gt $bestScore) {
            $bestScore = $score
            $bestOrgId = $oid
        }
    }

    $OrgId = $bestOrgId
}

$api = @{}
$houses = Invoke-ApiJson -Path "/api/houses" -OrgHeader $OrgId
$tenants = Invoke-ApiJson -Path "/api/tenants?includeArchived=true" -OrgHeader $OrgId
$tenancies = Invoke-ApiJson -Path "/api/tenancies" -OrgHeader $OrgId
$expenses = Invoke-ApiJson -Path "/api/expenses" -OrgHeader $OrgId
$documents = Invoke-ApiJson -Path "/api/documents" -OrgHeader $OrgId
$txAll = Invoke-ApiJson -Path "/api/payments/transactions?paymentType=All" -OrgHeader $OrgId
$txRent = Invoke-ApiJson -Path "/api/payments/transactions?paymentType=Rent" -OrgHeader $OrgId
$txDeposit = Invoke-ApiJson -Path "/api/payments/transactions?paymentType=Deposit" -OrgHeader $OrgId

$api["houses"] = Get-Count $houses
$api["tenants"] = Get-Count $tenants
$api["tenancies"] = Get-Count $tenancies
$api["expenses"] = Get-Count $expenses
$api["documents"] = Get-Count $documents
$api["transactions"] = Get-Count $txAll
$api["rentPayments"] = Get-Count $txRent
$api["depositPayments"] = Get-Count $txDeposit

$rentAmount = [decimal]0
$depositAmount = [decimal]0

$allTx = To-ApiArray $txAll

foreach ($t in $allTx) {
    if ($null -eq $t) { continue }
    $amt = [decimal]$t.amount
    if ($t.paymentType -eq "Rent") { $rentAmount += $amt }
    elseif ($t.paymentType -eq "Deposit") { $depositAmount += $amt }
}

$api["rentAmount"] = $rentAmount
$api["depositAmount"] = $depositAmount

$keys = @("houses","tenants","tenancies","expenses","documents","rentPayments","depositPayments","transactions","rentAmount","depositAmount")
$rows = @()
$hasMismatch = $false

foreach ($k in $keys) {
    $l = $local[$k]
    $a = $api[$k]
    $ok = $l -eq $a
    if (-not $ok) { $hasMismatch = $true }

    $rows += [PSCustomObject]@{
        Metric = $k
        Local = $l
        Api = $a
        Match = if ($ok) { "PASS" } else { "FAIL" }
    }
}

Write-Host "`nCutover Verification (OrgId=$OrgId)" -ForegroundColor Cyan
$rows | Format-Table -AutoSize

if ($hasMismatch) {
    Write-Host "`nCutover status: BLOCKED (mismatches found)" -ForegroundColor Red
    exit 1
}

Write-Host "`nCutover status: SAFE (all metrics match)" -ForegroundColor Green
exit 0
