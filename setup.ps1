param(
    [string]$InstanceName = "SQLEXPRESS",
    [string]$DatabaseName = "DaryvaDB"
)

$ErrorActionPreference = "Stop"

# --- Paths ---
$repoRoot      = "C:\Users\Abbas Haider\Repo\Daryva"
$migrationsDir = Join-Path $repoRoot "Database\Migrations"
$appConfigLocalPath = Join-Path $repoRoot "Daryva-Avalonia\App.config.local"
$backupFolder  = "C:\Backups\Daryva"

$serverName = "localhost\$InstanceName"

Write-Host "Using SQL instance: $serverName" -ForegroundColor Cyan

# --- Check tools ---
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw "sqlcmd not found. Make sure SQL Server Express and command-line tools are installed."
}

if (-not (Test-Path $migrationsDir)) {
    throw "Migrations folder not found: $migrationsDir"
}

# --- Create database if missing ---
Write-Host "Ensuring database '$DatabaseName' exists..." -ForegroundColor Cyan
$sqlCreateDb = @"
IF DB_ID('$DatabaseName') IS NULL
BEGIN
    CREATE DATABASE [$DatabaseName];
END
"@
sqlcmd -S $serverName -E -Q $sqlCreateDb

# --- Run migrations in order ---
Write-Host "Running migrations from $migrationsDir..." -ForegroundColor Cyan
Get-ChildItem -Path $migrationsDir -Filter "*.sql" |
    Sort-Object Name |
    ForEach-Object {
        Write-Host "  Executing $($_.Name)..." -ForegroundColor Yellow
        sqlcmd -S $serverName -E -d $DatabaseName -i $_.FullName
    }

# --- Ensure backup folder + permissions ---
Write-Host "Ensuring backup folder $backupFolder exists..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $backupFolder | Out-Null

# SQL Server Express service account (default)
$serviceAccount = "NT SERVICE\MSSQL`$SQLEXPRESS"

Write-Host "Granting permissions to $serviceAccount on $backupFolder..." -ForegroundColor Cyan
# Use ${serviceAccount} to avoid PowerShell treating ':' as part of the variable name
icacls $backupFolder /grant "${serviceAccount}:(OI)(CI)(F)" /T | Out-Null

# --- Update App.config.local connection string ---
Write-Host "Updating App.config.local connection string..." -ForegroundColor Cyan
if (-not (Test-Path $appConfigLocalPath)) {
    throw "App.config.local not found at $appConfigLocalPath"
}

[xml]$config = Get-Content $appConfigLocalPath

# Ensure <configuration> root
if (-not $config.configuration) {
    $config.AppendChild($config.CreateElement("configuration")) | Out-Null
}

$configuration = $config.configuration

# Ensure <connectionStrings>
if (-not $configuration.connectionStrings) {
    $connectionStringsElement = $config.CreateElement("connectionStrings")
    [void]$configuration.AppendChild($connectionStringsElement)
}

$connectionStrings = $configuration.connectionStrings

# Find or create <add name="DefaultConnection" ... />
$addNode = $connectionStrings.add | Where-Object { $_.name -eq "DefaultConnection" } | Select-Object -First 1
if (-not $addNode) {
    $addNode = $config.CreateElement("add")
    $nameAttr = $config.CreateAttribute("name")
    $nameAttr.Value = "DefaultConnection"
    [void]$addNode.Attributes.Append($nameAttr)
    [void]$connectionStrings.AppendChild($addNode)
}

$cs = "Server=$serverName;Database=$DatabaseName;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
$provider = "Microsoft.Data.SqlClient"

$csAttr = $addNode.Attributes["connectionString"]
if (-not $csAttr) {
    $csAttr = $config.CreateAttribute("connectionString")
    [void]$addNode.Attributes.Append($csAttr)
}
$csAttr.Value = $cs

$provAttr = $addNode.Attributes["providerName"]
if (-not $provAttr) {
    $provAttr = $config.CreateAttribute("providerName")
    [void]$addNode.Attributes.Append($provAttr)
}
$provAttr.Value = $provider

$config.Save($appConfigLocalPath)

Write-Host "Done. Local SQL Server is ready and App.config.local now points to $serverName / $DatabaseName." -ForegroundColor Green
Write-Host "You can now run the app with NO Docker. Set backup location to C:\Backups\Daryva and use 'Backup Now'." -ForegroundColor Green