param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [string]$DevUser = "dev-user-1",
    [string]$LocalDbPath = "$env:APPDATA\Daryva\Database\DaryvaDB.db",
    [string]$OrgName = "Cutover Org $(Get-Date -Format yyyyMMdd_HHmmss)",
    [string]$OrgId,
    [switch]$ClearTarget
)

$ErrorActionPreference = "Stop"

function Get-SqliteRows {
    param(
        [string]$Db,
        [string]$Sql
    )

    $json = & sqlite3 $Db -json $Sql
    if ([string]::IsNullOrWhiteSpace($json)) {
        return @()
    }

    $rows = $json | ConvertFrom-Json
    if ($rows -is [System.Array]) { return $rows }
    if ($null -eq $rows) { return @() }
    return @($rows)
}

function To-NullableInt {
    param($value)
    if ($null -eq $value) { return $null }
    $s = "$value"
    if ([string]::IsNullOrWhiteSpace($s)) { return $null }
    if ($s.Trim().ToUpperInvariant() -eq 'NULL') { return $null }
    return [int]$value
}

function To-NullableString {
    param($value)
    if ($null -eq $value) { return $null }
    $s = "$value"
    if ([string]::IsNullOrWhiteSpace($s)) { return $null }
    if ($s.Trim().ToUpperInvariant() -eq 'NULL') { return $null }
    return $s
}

function To-IsoDate {
    param($value)
    $s = To-NullableString $value
    if ($null -eq $s) {
        return [DateTime]::UtcNow.ToString("o")
    }

    try {
        return ([DateTime]::Parse($s)).ToUniversalTime().ToString("o")
    }
    catch {
        return [DateTime]::UtcNow.ToString("o")
    }
}

function To-IsoNullableDate {
    param($value)
    $s = To-NullableString $value
    if ($null -eq $s) {
        return $null
    }

    try {
        return ([DateTime]::Parse($s)).ToUniversalTime().ToString("o")
    }
    catch {
        return $null
    }
}

function Normalize-DocumentMimeAndPath {
    param(
        $fileMimeType,
        $storagePath
    )

    $mime = To-NullableString $fileMimeType
    $path = To-NullableString $storagePath

    $looksLikePath = $false
    if ($mime) {
        $looksLikePath = ($mime.Length -gt 128) -or ($mime -match '^[A-Za-z]:\\') -or ($mime -match '[\\/]')
    }

    $pathLooksLikeMime = $false
    if ($path) {
        $pathLooksLikeMime = $path -match '^[a-zA-Z]+/[a-zA-Z0-9.+-]+$'
    }

    if ($looksLikePath -and $pathLooksLikeMime) {
        $tmp = $mime
        $mime = $path
        $path = $tmp
    }

    if ($mime -and $mime.Length -gt 128) {
        $mime = $mime.Substring(0, 128)
    }

    return @{ Mime = $mime; Path = $path }
}

if (-not (Test-Path $LocalDbPath)) {
    throw "Local DB not found: $LocalDbPath"
}

if (-not (Get-Command sqlite3 -ErrorAction SilentlyContinue)) {
    throw "sqlite3 executable is not available in PATH."
}

$headers = @{ "X-Dev-User" = $DevUser }

# Validate API health
$health = Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + "/health") -Method Get
if ($health.status -ne "healthy") {
    throw "API health check failed."
}

# Resolve or create target org
if ([string]::IsNullOrWhiteSpace($OrgId)) {
    $orgBody = @{ name = $OrgName } | ConvertTo-Json
    $created = Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + "/api/orgs") -Method Post -Headers $headers -ContentType "application/json" -Body $orgBody
    $OrgId = "$($created.id)"
    Write-Host "Created target org: $OrgId ($OrgName)" -ForegroundColor Green
}
else {
    Write-Host "Using provided target org: $OrgId" -ForegroundColor Yellow
}

$headersWithOrg = @{ "X-Dev-User" = $DevUser; "X-Org-Id" = $OrgId }

if ($ClearTarget) {
    Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + "/api/import/clear") -Method Delete -Headers $headersWithOrg | Out-Null
    Write-Host "Cleared target org before import." -ForegroundColor Yellow
}

# Read local data
$housesRows = Get-SqliteRows -Db $LocalDbPath -Sql @"
SELECT HouseId, AddressLine1, AddressLine2, City, Postcode, TotalRooms, CreatedAt
FROM House
ORDER BY HouseId;
"@

$tenantsRows = Get-SqliteRows -Db $LocalDbPath -Sql @"
SELECT TenantId, FullName, PhoneNumber, Email, UniversityName, CreatedAt, IsArchived
FROM Tenant
ORDER BY TenantId;
"@

$tenanciesRows = Get-SqliteRows -Db $LocalDbPath -Sql @"
SELECT TenancyId, HouseId, TenantId, MoveInDate, MoveOutDate, RentStartMonth, RentStartYear,
       RentAmountMonthly, DepositAmount, PaymentDueDay, Status, Notes
FROM Tenancy
ORDER BY TenancyId;
"@

$expensesRows = Get-SqliteRows -Db $LocalDbPath -Sql @"
SELECT HouseExpenseId, HouseId, DateIncurred, Category, Amount, Vendor, Notes, ReceiptDocumentId
FROM HouseExpense
ORDER BY HouseExpenseId;
"@

$documentsRows = Get-SqliteRows -Db $LocalDbPath -Sql @"
SELECT DocumentId, TenantId, TenancyId, HouseId, Type, DisplayName, FileName, FileMimeType,
       StoragePath, Source, UploadedAt, NULL AS ValidFrom, NULL AS ValidTo, Version, IsActive
FROM Document
ORDER BY DocumentId;
"@

$rentPaymentsRows = Get-SqliteRows -Db $LocalDbPath -Sql @"
SELECT RentPaymentId, TenancyId, PaidOn, AmountPaid, Method, Reference, Notes, CollectedBy
FROM RentPayment
ORDER BY RentPaymentId;
"@

$depositPaymentsRows = Get-SqliteRows -Db $LocalDbPath -Sql @"
SELECT DepositPaymentId, TenancyId, PaidOn, AmountPaid, Method, Reference, Notes
FROM DepositPayment
ORDER BY DepositPaymentId;
"@

$templateRows = Get-SqliteRows -Db $LocalDbPath -Sql @"
SELECT TemplateId, Name, Channel, Type, SubjectTemplate, BodyTemplate, IsDefault, CreatedAt
FROM NotificationTemplate
ORDER BY TemplateId;
"@

$notificationRows = Get-SqliteRows -Db $LocalDbPath -Sql @"
SELECT NotificationId, TenantId, TenancyId, Channel, Type, ToAddress, Subject, Body,
       ScheduledFor, SentAt, Status, ProviderMessageId, Error, TemplateId
FROM Notification
ORDER BY NotificationId;
"@

$attemptRows = Get-SqliteRows -Db $LocalDbPath -Sql @"
SELECT AttemptId, NotificationId, AttemptedAt, Status, Error, ProviderMessageId
FROM NotificationAttempt
ORDER BY AttemptId;
"@

# Build import payload
$payload = [ordered]@{
    houses = @($housesRows | ForEach-Object {
        $houseName = To-NullableString $_.AddressLine1
        if ([string]::IsNullOrWhiteSpace($houseName)) { $houseName = "House $($_.HouseId)" }
        [ordered]@{
            oldId = [int]$_.HouseId
            name = $houseName
            addressLine1 = "$($_.AddressLine1)"
            addressLine2 = To-NullableString $_.AddressLine2
            city = "$($_.City)"
            postcode = "$($_.Postcode)"
            totalRooms = [int]$_.TotalRooms
            createdAt = To-IsoDate $_.CreatedAt
        }
    })
    tenants = @($tenantsRows | ForEach-Object {
        [ordered]@{
            oldId = [int]$_.TenantId
            fullName = "$($_.FullName)"
            phoneNumber = if ([string]::IsNullOrWhiteSpace("$($_.PhoneNumber)")) { "" } else { "$($_.PhoneNumber)" }
            email = if ([string]::IsNullOrWhiteSpace("$($_.Email)")) { "" } else { "$($_.Email)" }
            universityName = To-NullableString $_.UniversityName
            createdAt = To-IsoDate $_.CreatedAt
            isArchived = ([int]$_.IsArchived -eq 1)
        }
    })
    tenancies = @($tenanciesRows | ForEach-Object {
        [ordered]@{
            oldId = [int]$_.TenancyId
            oldHouseId = [int]$_.HouseId
            oldTenantId = [int]$_.TenantId
            moveInDate = To-IsoDate $_.MoveInDate
            moveOutDate = To-IsoNullableDate $_.MoveOutDate
            rentStartMonth = To-NullableInt $_.RentStartMonth
            rentStartYear = To-NullableInt $_.RentStartYear
            rentAmountMonthly = [decimal]$_.RentAmountMonthly
            depositAmount = [decimal]$_.DepositAmount
            paymentDueDay = [int]$_.PaymentDueDay
            status = if ([string]::IsNullOrWhiteSpace("$($_.Status)")) { "Active" } else { "$($_.Status)" }
            notes = To-NullableString $_.Notes
        }
    })
    expenses = @($expensesRows | ForEach-Object {
        [ordered]@{
            oldId = [int]$_.HouseExpenseId
            oldHouseId = [int]$_.HouseId
            dateIncurred = To-IsoDate $_.DateIncurred
            category = if ([string]::IsNullOrWhiteSpace("$($_.Category)")) { "General" } else { "$($_.Category)" }
            amount = [decimal]$_.Amount
            vendor = To-NullableString $_.Vendor
            notes = To-NullableString $_.Notes
            oldReceiptDocumentId = To-NullableInt $_.ReceiptDocumentId
        }
    })
    documents = @($documentsRows | ForEach-Object {
        $docNormalized = Normalize-DocumentMimeAndPath -fileMimeType $_.FileMimeType -storagePath $_.StoragePath
        [ordered]@{
            oldId = [int]$_.DocumentId
            oldTenantId = To-NullableInt $_.TenantId
            oldTenancyId = To-NullableInt $_.TenancyId
            oldHouseId = To-NullableInt $_.HouseId
            type = if ([string]::IsNullOrWhiteSpace("$($_.Type)")) { "General" } else { "$($_.Type)" }
            displayName = if ([string]::IsNullOrWhiteSpace("$($_.DisplayName)")) { "$($_.FileName)" } else { "$($_.DisplayName)" }
            fileName = "$($_.FileName)"
            fileMimeType = $docNormalized.Mime
            storagePath = $docNormalized.Path
            source = To-NullableString $_.Source
            uploadedAt = To-IsoDate $_.UploadedAt
            validFrom = To-IsoNullableDate $_.ValidFrom
            validTo = To-IsoNullableDate $_.ValidTo
            version = [int]$_.Version
            isActive = ([int]$_.IsActive -eq 1)
        }
    })
    rentPayments = @($rentPaymentsRows | ForEach-Object {
        [ordered]@{
            oldId = [int]$_.RentPaymentId
            oldTenancyId = [int]$_.TenancyId
            datePaid = To-IsoDate $_.PaidOn
            amountPaid = [decimal]$_.AmountPaid
            paymentMethod = if ([string]::IsNullOrWhiteSpace("$($_.Method)")) { "Unknown" } else { "$($_.Method)" }
            referenceNumber = To-NullableString $_.Reference
            notes = To-NullableString $_.Notes
            collectedBy = To-NullableString $_.CollectedBy
        }
    })
    depositPayments = @($depositPaymentsRows | ForEach-Object {
        [ordered]@{
            oldId = [int]$_.DepositPaymentId
            oldTenancyId = [int]$_.TenancyId
            datePaid = To-IsoDate $_.PaidOn
            amountPaid = [decimal]$_.AmountPaid
            paymentMethod = if ([string]::IsNullOrWhiteSpace("$($_.Method)")) { "Unknown" } else { "$($_.Method)" }
            protectionScheme = $null
            protectionReference = To-NullableString $_.Reference
            notes = To-NullableString $_.Notes
        }
    })
    notificationTemplates = @($templateRows | ForEach-Object {
        [ordered]@{
            oldId = [int]$_.TemplateId
            name = "$($_.Name)"
            channel = "$($_.Channel)"
            type = "$($_.Type)"
            subjectTemplate = To-NullableString $_.SubjectTemplate
            bodyTemplate = if ([string]::IsNullOrWhiteSpace("$($_.BodyTemplate)")) { "" } else { "$($_.BodyTemplate)" }
            isDefault = ([int]$_.IsDefault -eq 1)
            createdAt = To-IsoDate $_.CreatedAt
        }
    })
    notifications = @($notificationRows | ForEach-Object {
        [ordered]@{
            oldId = [int]$_.NotificationId
            oldTenantId = [int]$_.TenantId
            oldTenancyId = To-NullableInt $_.TenancyId
            channel = "$($_.Channel)"
            type = "$($_.Type)"
            toAddress = "$($_.ToAddress)"
            subject = To-NullableString $_.Subject
            body = "$($_.Body)"
            scheduledFor = To-IsoDate $_.ScheduledFor
            sentAt = To-IsoNullableDate $_.SentAt
            status = "$($_.Status)"
            providerMessageId = To-NullableString $_.ProviderMessageId
            error = To-NullableString $_.Error
            oldTemplateId = To-NullableInt $_.TemplateId
        }
    })
    notificationAttempts = @($attemptRows | ForEach-Object {
        [ordered]@{
            oldId = [int]$_.AttemptId
            oldNotificationId = [int]$_.NotificationId
            attemptedAt = To-IsoDate $_.AttemptedAt
            status = "$($_.Status)"
            error = To-NullableString $_.Error
            providerMessageId = To-NullableString $_.ProviderMessageId
        }
    })
}

$payloadJson = $payload | ConvertTo-Json -Depth 15

$response = $null
try {
    $response = Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + "/api/import") -Method Post -Headers $headersWithOrg -ContentType "application/json" -Body $payloadJson
}
catch {
    Write-Host "Import failed for org $OrgId" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host $errorBody -ForegroundColor Yellow
    }
    throw
}

Write-Host "Import completed for org $OrgId" -ForegroundColor Green
if ($response.stats) {
    $importMessage = [string]::Format(
        "Imported: Houses={0}, Tenants={1}, Tenancies={2}, Expenses={3}, Documents={4}, RentPayments={5}, DepositPayments={6}",
        $response.stats.housesImported,
        $response.stats.tenantsImported,
        $response.stats.tenanciesImported,
        $response.stats.expensesImported,
        $response.stats.documentsImported,
        $response.stats.rentPaymentsImported,
        $response.stats.depositPaymentsImported)
    Write-Host $importMessage
}

if ($response.errors -and $response.errors.Count -gt 0) {
    Write-Host "Import reported warnings/errors:" -ForegroundColor Yellow
    $response.errors | Select-Object -First 25 | ForEach-Object { Write-Host " - $_" -ForegroundColor Yellow }
}

# Fallback: if bulk import dropped documents, create them via documents API using returned ID mappings
$apiDocuments = Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + "/api/documents") -Method Get -Headers $headersWithOrg
$apiDocumentCount = if ($apiDocuments -is [System.Array]) { $apiDocuments.Count } elseif ($apiDocuments.value) { $apiDocuments.value.Count } else { 1 }

if ($apiDocumentCount -lt $documentsRows.Count) {
    Write-Host "Document fallback import: local=$($documentsRows.Count), api=$apiDocumentCount" -ForegroundColor Yellow

    $houseMap = @{}
    $tenantMap = @{}
    $tenancyMap = @{}

    if ($response.idMappings) {
        if ($response.idMappings.houses) {
            foreach ($p in $response.idMappings.houses.PSObject.Properties) { $houseMap[[int]$p.Name] = "$($p.Value)" }
        }
        if ($response.idMappings.tenants) {
            foreach ($p in $response.idMappings.tenants.PSObject.Properties) { $tenantMap[[int]$p.Name] = "$($p.Value)" }
        }
        if ($response.idMappings.tenancies) {
            foreach ($p in $response.idMappings.tenancies.PSObject.Properties) { $tenancyMap[[int]$p.Name] = "$($p.Value)" }
        }
    }

    $createdDocs = 0
    foreach ($doc in $documentsRows) {
        $docNormalized = Normalize-DocumentMimeAndPath -fileMimeType $doc.FileMimeType -storagePath $doc.StoragePath
        $oldTenantId = To-NullableInt $doc.TenantId
        $oldTenancyId = To-NullableInt $doc.TenancyId
        $oldHouseId = To-NullableInt $doc.HouseId

        $apiTenantId = $null
        $apiTenancyId = $null
        $apiHouseId = $null

        if ($oldTenantId -and $tenantMap.ContainsKey($oldTenantId)) { $apiTenantId = $tenantMap[$oldTenantId] }
        if ($oldTenancyId -and $tenancyMap.ContainsKey($oldTenancyId)) { $apiTenancyId = $tenancyMap[$oldTenancyId] }
        if ($oldHouseId -and $houseMap.ContainsKey($oldHouseId)) { $apiHouseId = $houseMap[$oldHouseId] }

        if (-not $apiTenantId -and -not $apiTenancyId -and -not $apiHouseId) {
            continue
        }

        $docRequest = @{
            tenantId = $apiTenantId
            tenancyId = $apiTenancyId
            houseId = $apiHouseId
            type = if ([string]::IsNullOrWhiteSpace("$($doc.Type)")) { "General" } else { "$($doc.Type)" }
            displayName = if ([string]::IsNullOrWhiteSpace("$($doc.DisplayName)")) { "$($doc.FileName)" } else { "$($doc.DisplayName)" }
            fileName = "$($doc.FileName)"
            fileMimeType = $docNormalized.Mime
            source = To-NullableString $doc.Source
            uploadedAt = To-IsoDate $doc.UploadedAt
            validFrom = To-IsoNullableDate $doc.ValidFrom
            validTo = To-IsoNullableDate $doc.ValidTo
            fileContent = $null
        }

        try {
            Invoke-RestMethod -Uri ($ApiBaseUrl.TrimEnd('/') + "/api/documents") -Method Post -Headers $headersWithOrg -ContentType "application/json" -Body ($docRequest | ConvertTo-Json -Depth 6) | Out-Null
            $createdDocs++
        }
        catch {
            Write-Host "Document fallback failed for DocumentId=$($doc.DocumentId)" -ForegroundColor Yellow
            if ($_.Exception.Response) {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $docErrorBody = $reader.ReadToEnd()
                if (-not [string]::IsNullOrWhiteSpace($docErrorBody)) {
                    Write-Host $docErrorBody -ForegroundColor Yellow
                }
            }
        }
    }

    Write-Host "Document fallback created $createdDocs document(s)." -ForegroundColor Green
}

Write-Output $OrgId
