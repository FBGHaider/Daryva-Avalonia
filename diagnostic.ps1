# PowerShell script to debug SQLite database
$dbPath = "$env:APPDATA\Daryva\Database\DaryvaDB.db"

if (!(Test-Path $dbPath)) {
    Write-Host "ERROR: Database not found at $dbPath"
    exit 1
}

Write-Host "Database found: $dbPath" -ForegroundColor Green
Write-Host "File size: $((Get-Item $dbPath).Length / 1MB) MB"
Write-Host ""

# Test if sqlite3 is available
if (Get-Command sqlite3 -ErrorAction SilentlyContinue) {
    Write-Host "Using sqlite3 command-line tool"
    
    # Show all tables
    Write-Host ""
    Write-Host "=== TABLES ===" -ForegroundColor Cyan
    sqlite3 "$dbPath" ".tables"
    
    # Count data
    Write-Host ""
    Write-Host "=== DATA COUNTS ===" -ForegroundColor Cyan
    sqlite3 "$dbPath" @"
SELECT 'House' as TableName, COUNT(*) as Count FROM House
UNION ALL
SELECT 'Tenant', COUNT(*) FROM Tenant
UNION ALL
SELECT 'Tenancy', COUNT(*) FROM Tenancy
UNION ALL
SELECT 'Expense', COUNT(*) FROM Expense
UNION ALL
SELECT 'Document', COUNT(*) FROM Document
UNION ALL
SELECT 'RentPayment', COUNT(*) FROM RentPayment
UNION ALL
SELECT 'DepositPayment', COUNT(*) FROM DepositPayment;
"@
    
    # Show sample houses
    Write-Host ""
    Write-Host "=== SAMPLE HOUSES ===" -ForegroundColor Cyan
    sqlite3 "$dbPath" "SELECT HouseId, Name, AddressLine1, City FROM House LIMIT 3;" 
    
    # Show sample tenants
    Write-Host ""
    Write-Host "=== SAMPLE TENANTS ===" -ForegroundColor Cyan
    sqlite3 "$dbPath" "SELECT TenantId, FullName, Email FROM Tenant LIMIT 10;"
    
    # Show all 11 tenant IDs
    Write-Host ""
    Write-Host "=== ALL TENANT IDS ===" -ForegroundColor Cyan
    sqlite3 "$dbPath" "SELECT TenantId, FullName FROM Tenant ORDER BY TenantId;"
    
} else {
    Write-Host "sqlite3 command-line tool not found in PATH" -ForegroundColor Red
    Write-Host "Trying alternative method..."
    
    # Try using .NET's built-in SQLite support
    Add-Type -AssemblyName System.Data.SQLite
    $conn = New-Object System.Data.SQLite.SQLiteConnection
    $conn.ConnectionString = "Data Source=$dbPath"
    $conn.Open()
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"
    $reader = $cmd.ExecuteReader()
    
    Write-Host "Tables found:"
    while ($reader.Read()) {
        Write-Host "  - $($reader[0])"
    }
    
    $reader.Close()
    $conn.Close()
}
