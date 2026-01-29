# Insert Default Notification Templates into SQLite Database

param(
    [string]$DbPath = "C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Inserting Default Notification Templates ===" -ForegroundColor Cyan
Write-Host ""

# Check if sqlite3 is available
if (-not (Get-Command sqlite3 -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: sqlite3 not found. Please install SQLite first." -ForegroundColor Red
    exit 1
}

# Check if database exists
if (-not (Test-Path $DbPath)) {
    Write-Host "ERROR: Database not found at: $DbPath" -ForegroundColor Red
    exit 1
}

Write-Host "Database: $DbPath" -ForegroundColor Green
Write-Host ""

# Check if templates already exist
$existingCount = & sqlite3 $DbPath "SELECT COUNT(*) FROM NotificationTemplate;" 2>&1
if ($LASTEXITCODE -eq 0 -and [int]$existingCount -gt 0) {
    Write-Host "WARNING: $existingCount templates already exist." -ForegroundColor Yellow
    # Delete and recreate to ensure correct format
    & sqlite3 $DbPath "DELETE FROM NotificationTemplate;" 2>&1 | Out-Null
    Write-Host "Deleted existing templates. Recreating with correct format..." -ForegroundColor Green
}

Write-Host "Inserting default templates..." -ForegroundColor Gray

# Template bodies - Use {Currency}{AmountDue} instead of hardcoded £ to avoid encoding issues
$rentDueBody = "Dear {TenantName}," + [Environment]::NewLine + [Environment]::NewLine + "This is a reminder that your rent payment of {Currency}{AmountDue} for {Month} is due on {DueDate}." + [Environment]::NewLine + [Environment]::NewLine + "Property: {HouseAddress}" + [Environment]::NewLine + [Environment]::NewLine + "Please ensure payment is made by the due date." + [Environment]::NewLine + [Environment]::NewLine + "Payment Instructions: {PayInstructions}" + [Environment]::NewLine + [Environment]::NewLine + "Thank you."

$rentOverdueBody = "Dear {TenantName}," + [Environment]::NewLine + [Environment]::NewLine + "This is to inform you that your rent payment of {Currency}{AmountDue} for {Month} is now overdue." + [Environment]::NewLine + [Environment]::NewLine + "Property: {HouseAddress}" + [Environment]::NewLine + "Due Date: {DueDate}" + [Environment]::NewLine + [Environment]::NewLine + "Please arrange payment immediately to avoid further action." + [Environment]::NewLine + [Environment]::NewLine + "Payment Instructions: {PayInstructions}" + [Environment]::NewLine + [Environment]::NewLine + "Thank you."

$missingDocBody = "Dear {TenantName}," + [Environment]::NewLine + [Environment]::NewLine + "This is a reminder that we are still missing your Student Confirmation Letter." + [Environment]::NewLine + [Environment]::NewLine + "Property: {HouseAddress}" + [Environment]::NewLine + [Environment]::NewLine + "Please provide this document at your earliest convenience." + [Environment]::NewLine + [Environment]::NewLine + "Thank you."

$generalBody = "Dear {TenantName}," + [Environment]::NewLine + [Environment]::NewLine + "{Message}" + [Environment]::NewLine + [Environment]::NewLine + "Property: {HouseAddress}" + [Environment]::NewLine + [Environment]::NewLine + "Thank you."

# Escape single quotes for SQL
function Escape-SqlString($str) {
    return $str -replace "'", "''"
}

# Insert templates
$templates = @(
    @{
        Name = "Rent Due Reminder"
        Channel = "Email"
        Type = "RentDue"
        Subject = "Rent Due Reminder - {Month}"
        Body = $rentDueBody
        IsDefault = 1
    },
    @{
        Name = "Rent Overdue"
        Channel = "Email"
        Type = "RentOverdue"
        Subject = "URGENT: Rent Overdue - {Month}"
        Body = $rentOverdueBody
        IsDefault = 1
    },
    @{
        Name = "Missing Student Letter"
        Channel = "Email"
        Type = "MissingDocuments"
        Subject = "Missing Student Confirmation Letter"
        Body = $missingDocBody
        IsDefault = 1
    },
    @{
        Name = "General Message"
        Channel = "Email"
        Type = "General"
        Subject = "Message from Landlord"
        Body = $generalBody
        IsDefault = 1
    }
)

foreach ($template in $templates) {
    $name = Escape-SqlString $template.Name
    $subject = Escape-SqlString $template.Subject
    $body = Escape-SqlString $template.Body
    
    $sql = "INSERT INTO NotificationTemplate (Name, Channel, Type, SubjectTemplate, BodyTemplate, IsDefault, CreatedAt) VALUES ('$name', '$($template.Channel)', '$($template.Type)', '$subject', '$body', $($template.IsDefault), datetime('now'));"
    
    $result = $sql | & sqlite3 $DbPath 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Inserted: $($template.Name)" -ForegroundColor Green
    } else {
        Write-Host "  Failed: $($template.Name) - $result" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Verification ===" -ForegroundColor Cyan
$count = & sqlite3 $DbPath "SELECT COUNT(*) FROM NotificationTemplate;" 2>&1
Write-Host "Total templates: $count" -ForegroundColor Green

Write-Host ""
Write-Host "Templates:" -ForegroundColor Cyan
& sqlite3 $DbPath "SELECT TemplateId, Name, Channel, Type, IsDefault FROM NotificationTemplate ORDER BY Name;" 2>&1

Write-Host ""
Write-Host "=== Complete ===" -ForegroundColor Green
