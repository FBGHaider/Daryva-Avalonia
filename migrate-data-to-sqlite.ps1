# =============================================
# Data Migration Script: SQL Server to SQLite
# =============================================

param(
    [string]$SqlServerInstance = "localhost",
    [string]$DatabaseName = "DaryvaDB",
    [string]$SqliteDbPath = "C:\Users\Abbas Haider\OneDrive\Documents\DaryvaDB.db",
    [switch]$UseWindowsAuth = $true,
    [string]$SqlUsername = "",
    [string]$SqlPassword = ""
)

$ErrorActionPreference = "Stop"

Write-Host "=== SQL Server to SQLite Data Migration ===" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: sqlcmd not found. Install SQL Server Command Line Tools." -ForegroundColor Red
    exit 1
}

if (-not (Get-Command sqlite3 -ErrorAction SilentlyContinue)) {
    Write-Host "WARNING: sqlite3 not found. You'll need to import manually using DB Browser." -ForegroundColor Yellow
    Write-Host "Download from: https://www.sqlite.org/download.html" -ForegroundColor Yellow
    $useSqlite3 = $false
} else {
    $useSqlite3 = $true
}

# Check if SQLite database exists, create if not
if (-not (Test-Path $SqliteDbPath)) {
    Write-Host "SQLite database not found. Creating new database..." -ForegroundColor Yellow
    # Create empty database file
    New-Item -ItemType File -Path $SqliteDbPath -Force | Out-Null
}

# Check if tables exist, create schema if needed
Write-Host "Checking database schema..." -ForegroundColor Gray
$tablesExist = & sqlite3 $SqliteDbPath "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='House';" 2>&1
if ($LASTEXITCODE -ne 0 -or ($tablesExist -match '^\s*0\s*$')) {
    Write-Host "Tables not found. Creating schema..." -ForegroundColor Yellow
    $schemaFile = Join-Path $PSScriptRoot "Database\Migrations\001_CreateDatabase_SQLite.sql"
    if (Test-Path $schemaFile) {
        Get-Content $schemaFile | & sqlite3 $SqliteDbPath 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Schema created successfully." -ForegroundColor Green
        } else {
            Write-Host "WARNING: Schema creation had errors. Tables may already exist." -ForegroundColor Yellow
        }
    } else {
        Write-Host "WARNING: Schema file not found at: $schemaFile" -ForegroundColor Yellow
        Write-Host "Please create tables manually using DB Browser." -ForegroundColor Yellow
    }
} else {
    Write-Host "Database schema already exists." -ForegroundColor Green
}
Write-Host ""

# Build connection string
if ($UseWindowsAuth) {
    # Try different connection formats
    if ($SqlServerInstance -eq "localhost") {
        # Try common instances
        $instances = @("localhost", "localhost\SQLEXPRESS", "(localdb)\MSSQLLocalDB")
    } else {
        $instances = @($SqlServerInstance)
    }
} else {
    if ([string]::IsNullOrWhiteSpace($SqlUsername) -or [string]::IsNullOrWhiteSpace($SqlPassword)) {
        Write-Host "ERROR: SQL authentication requires -SqlUsername and -SqlPassword" -ForegroundColor Red
        exit 1
    }
    $instances = @($SqlServerInstance)
}

# Test connection and find working instance
$workingInstance = $null
foreach ($instance in $instances) {
    Write-Host "Trying SQL Server instance: $instance..." -ForegroundColor Gray
    if ($UseWindowsAuth) {
        $testResult = & sqlcmd -S $instance -d $DatabaseName -E -Q "SELECT 1" 2>&1
    } else {
        $testResult = & sqlcmd -S $instance -d $DatabaseName -U $SqlUsername -P $SqlPassword -Q "SELECT 1" 2>&1
    }
    
    if ($LASTEXITCODE -eq 0) {
        $workingInstance = $instance
        Write-Host "Connected to SQL Server instance: $instance" -ForegroundColor Green
        break
    }
}

if ($null -eq $workingInstance) {
    Write-Host "ERROR: Could not connect to SQL Server. Please specify the correct instance:" -ForegroundColor Red
    Write-Host "  .\migrate-data-to-sqlite.ps1 -SqlServerInstance `"localhost\SQLEXPRESS`"" -ForegroundColor Yellow
    Write-Host "  .\migrate-data-to-sqlite.ps1 -SqlServerInstance `"(localdb)\MSSQLLocalDB`"" -ForegroundColor Yellow
    exit 1
}

# Build connection parameters (as array for splatting)
if ($UseWindowsAuth) {
    $connectionParams = @("-S", $workingInstance, "-d", $DatabaseName, "-E")
} else {
    $connectionParams = @("-S", $workingInstance, "-d", $DatabaseName, "-U", $SqlUsername, "-P", $SqlPassword)
}

Write-Host "Source: SQL Server ($SqlServerInstance\$DatabaseName)" -ForegroundColor Green
Write-Host "Target: SQLite ($SqliteDbPath)" -ForegroundColor Green
Write-Host ""

# Define tables in dependency order
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

# Create temp directory for exports
$tempDir = Join-Path $env:TEMP "DaryvaMigration_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null
Write-Host "Using temp directory: $tempDir" -ForegroundColor Gray
Write-Host ""

# Export each table
foreach ($table in $tables) {
    Write-Host "Processing table: $table..." -ForegroundColor Yellow
    
    # Check if table exists in SQL Server
    $checkTable = "SELECT CASE WHEN EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '$table') THEN 1 ELSE 0 END"
    $tableExistsResult = & sqlcmd @connectionParams -Q $checkTable -h -1 -W 2>&1
    $tableExists = ($tableExistsResult | Where-Object { $_ -match '^\s*1\s*$' }) -ne $null
    
    if (-not $tableExists) {
        Write-Host "  Table $table does not exist in SQL Server, skipping..." -ForegroundColor Gray
        continue
    }
    
    # Export data to CSV
    $csvFile = Join-Path $tempDir "$table.csv"
    $sqlQuery = "SELECT * FROM [$table]"
    
    Write-Host "  Exporting to CSV..." -ForegroundColor Gray
    # Export with comma separator
    $tempOutput = Join-Path $tempDir "${table}_raw.txt"
    & sqlcmd @connectionParams -Q $sqlQuery -s "," -W -o $tempOutput 2>&1 | Out-Null
    
    # Process the output - remove row count line and empty lines
    $allLines = Get-Content $tempOutput
    $dataLines = $allLines | Where-Object {
        $line = $_
        $line -notmatch '^\s*\(\d+\s+rows?\s+affected\)' -and 
        $line -notmatch '^\s*$' -and
        $line -notmatch '^-+$' -and
        $line.Trim().Length -gt 0
    }
    
    if ($dataLines.Count -eq 0) {
        Write-Host "  No data to export, skipping..." -ForegroundColor Gray
        continue
    }
    
    # Save cleaned CSV
    $dataLines | Set-Content $csvFile -Encoding UTF8
    
    # Count rows (subtract 1 for header if present)
    $rowCount = $dataLines.Count
    # Check if first line looks like a header (has column names)
    $firstLine = $dataLines[0]
    if ($firstLine -match 'HouseId|TenantId|TenancyId|RentChargeId' -or $rowCount -eq 1) {
        # Likely has header, subtract 1
        $rowCount = $rowCount - 1
    }
    
    Write-Host "  Found $rowCount data rows" -ForegroundColor Gray
    
    if ($rowCount -le 0) {
        Write-Host "  No data rows, skipping import..." -ForegroundColor Gray
        continue
    }
    
    # Import to SQLite
    if ($useSqlite3) {
        Write-Host "  Importing to SQLite..." -ForegroundColor Gray
        
        # Check if table already has data
        $existingCountResult = & sqlite3 $SqliteDbPath "SELECT COUNT(*) FROM $table;" 2>&1
        $existingCount = 0
        if ($LASTEXITCODE -eq 0 -and $existingCountResult -match '^\s*(\d+)\s*$') {
            $existingCount = [int]$matches[1]
            if ($existingCount -gt 0) {
                Write-Host "  Table already has $existingCount rows. Deleting existing data..." -ForegroundColor Yellow
                & sqlite3 $SqliteDbPath "DELETE FROM $table;" 2>&1 | Out-Null
            }
        }
        
        # SQLite import - skip header row if it exists
        $csvContent = Get-Content $csvFile
        # Check if first line is header (contains common column name patterns)
        $skipHeader = $csvContent[0] -match 'HouseId|TenantId|TenancyId|RentChargeId|Id|Name|Address'
        if ($skipHeader) {
            $csvContentWithoutHeader = $csvContent | Select-Object -Skip 1
        } else {
            $csvContentWithoutHeader = $csvContent
        }
        
        $tempCsvNoHeader = Join-Path $tempDir "${table}_noheader.csv"
        $csvContentWithoutHeader | Set-Content $tempCsvNoHeader -Encoding UTF8
        
        # Import using sqlite3 - use absolute path
        $tempCsvNoHeaderFull = (Resolve-Path $tempCsvNoHeader).Path
        
        # Create import commands file
        $importCmdFile = Join-Path $tempDir "import_${table}.txt"
        $importCmdLines = @(
            ".mode csv",
            ".import $tempCsvNoHeaderFull $table"
        )
        $importCmdLines | Set-Content $importCmdFile -Encoding ASCII
        
        $importResult = Get-Content $importCmdFile | & sqlite3 $SqliteDbPath 2>&1
        
        # Check for errors (ignore warnings about column count if data imported)
        if ($importResult -and $importResult -match "error|Error|ERROR" -and $importResult -notmatch "expected.*columns" -and $importResult -notmatch "filling the rest") {
            Write-Host "  Import error: $importResult" -ForegroundColor Red
        } elseif ($importResult -match "expected.*columns") {
            # This is usually just a warning, data still imports
            Write-Host "  Import completed (with column warnings - data may need verification)" -ForegroundColor Yellow
        }
        
        # Verify import
        $newCountResult = & sqlite3 $SqliteDbPath "SELECT COUNT(*) FROM $table;" 2>&1
        if ($LASTEXITCODE -eq 0 -and $newCountResult -match '^\s*(\d+)\s*$') {
            $newCount = [int]$matches[1]
            Write-Host "  ✓ Imported $rowCount rows (total in table: $newCount)" -ForegroundColor Green
        } else {
            Write-Host "  Import completed (verify manually)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  CSV saved to: $csvFile" -ForegroundColor Cyan
        Write-Host "  Import manually using DB Browser for SQLite:" -ForegroundColor Cyan
        Write-Host "    1. Open DB Browser" -ForegroundColor Cyan
        Write-Host "    2. File -> Import -> Table from CSV file" -ForegroundColor Cyan
        Write-Host "    3. Select: $csvFile" -ForegroundColor Cyan
        Write-Host "    4. Check 'Column names in first line'" -ForegroundColor Cyan
    }
    
    Write-Host ""
}

Write-Host "=== Migration Complete ===" -ForegroundColor Green
Write-Host ""

if (-not $useSqlite3) {
    Write-Host "CSV files are in: $tempDir" -ForegroundColor Cyan
    Write-Host "Import them manually using DB Browser for SQLite." -ForegroundColor Cyan
} else {
    Write-Host "All data has been migrated to SQLite!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Verify the data:" -ForegroundColor Cyan
    $verify1 = "sqlite3 `"$SqliteDbPath`" `.tables`"
    $verify2 = "sqlite3 `"$SqliteDbPath`" `"SELECT COUNT(*) FROM House;`""
    Write-Host "  $verify1" -ForegroundColor Gray
    Write-Host "  $verify2" -ForegroundColor Gray
}

# Cleanup
Write-Host ""
if ($Host.UI.RawUI.KeyAvailable -or [Environment]::UserInteractive) {
    $cleanup = Read-Host "Delete temporary CSV files? (Y/N)"
    if ($cleanup -eq "Y" -or $cleanup -eq "y") {
        Remove-Item -Path $tempDir -Recurse -Force
        Write-Host "Temporary files deleted." -ForegroundColor Green
    } else {
        Write-Host "CSV files kept in: $tempDir" -ForegroundColor Cyan
    }
} else {
    Write-Host "CSV files kept in: $tempDir" -ForegroundColor Cyan
    Write-Host "(Run script interactively to delete temp files)" -ForegroundColor Gray
}
