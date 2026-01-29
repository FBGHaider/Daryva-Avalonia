# Simple Data Migration: SQL Server to SQLite
# Exports data to CSV files for manual import

param(
    [string]$SqlServerInstance = "localhost\SQLEXPRESS",
    [string]$DatabaseName = "DaryvaDB",
    [string]$OutputDir = "$env:USERPROFILE\Desktop\DaryvaMigration"
)

$ErrorActionPreference = "Stop"

Write-Host "=== SQL Server to SQLite Data Export ===" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: sqlcmd not found." -ForegroundColor Red
    exit 1
}

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Write-Host "Exporting to: $OutputDir" -ForegroundColor Green
Write-Host ""

# Tables in dependency order
$tables = @(
    "House",
    "Tenant",
    "Tenancy",
    "RentCharge",
    "RentPayment",
    "DepositPayment",
    "Document",
    "HouseExpense",
    "Notification",
    "NotificationTemplate",
    "NotificationAttempt",
    "AppSettings"
)

# Export each table
foreach ($table in $tables) {
    Write-Host "Exporting: $table..." -ForegroundColor Yellow
    
    $csvFile = Join-Path $OutputDir "$table.csv"
    $sqlQuery = "SELECT * FROM [$table]"
    
    # Export to CSV
    & sqlcmd -S $SqlServerInstance -d $DatabaseName -E -Q $sqlQuery -s "," -W -o $csvFile -h -1 2>&1 | Out-Null
    
    if (Test-Path $csvFile) {
        # Remove the "(X rows affected)" line
        $content = Get-Content $csvFile
        $cleanContent = $content | Where-Object { $_ -notmatch '^\s*\(\d+\s+rows?\s+affected\)' -and $_.Trim().Length -gt 0 }
        $cleanContent | Set-Content $csvFile -Encoding UTF8
        
        $rowCount = ($cleanContent | Measure-Object -Line).Lines
        Write-Host "  Exported $rowCount lines to $csvFile" -ForegroundColor Green
    } else {
        Write-Host "  No data found" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=== Export Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Open DB Browser for SQLite" -ForegroundColor White
Write-Host "2. Open database: C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db" -ForegroundColor White
Write-Host "3. Import each CSV file:" -ForegroundColor White
Write-Host "   - File -> Import -> Table from CSV file" -ForegroundColor White
Write-Host "   - Check 'Column names in first line'" -ForegroundColor White
Write-Host "4. Import in this order:" -ForegroundColor White
Write-Host "   House, Tenant, Tenancy, RentCharge, RentPayment, DepositPayment," -ForegroundColor White
Write-Host "   Document, HouseExpense, Notification, NotificationTemplate," -ForegroundColor White
Write-Host "   NotificationAttempt, AppSettings" -ForegroundColor White
Write-Host ""
Write-Host "CSV files are in: $OutputDir" -ForegroundColor Cyan
