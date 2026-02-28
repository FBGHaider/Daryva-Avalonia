param(
    [int]$ApiPort = 5000,
    [int]$ApiStartupTimeoutSeconds = 45,
    [int]$UiStartupTimeoutSeconds = 30,
    [switch]$ApiOnly,
    [bool]$ShowApiTerminal = $true,
    [switch]$ShowTerminals,
    [switch]$SkipMigrations,
    [switch]$UseLocalApi  # If set, UI ApiBaseUrl is set to http://localhost:ApiPort; otherwise left unchanged (e.g. https://api.daryva.com)
)

$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return Split-Path -Parent $PSScriptRoot
    }

    $invokedPath = $MyInvocation.MyCommand.Path
    if (-not [string]::IsNullOrWhiteSpace($invokedPath)) {
        return Split-Path -Parent (Split-Path -Parent $invokedPath)
    }

    $cwd = (Get-Location).Path
    if (Test-Path (Join-Path $cwd "Daryva-Avalonia.sln")) {
        return $cwd
    }

    if (Test-Path (Join-Path $cwd "Scripts\restart-dev.ps1")) {
        return $cwd
    }

    throw "Could not determine repository root. Run this script from the repo root or invoke it by full path."
}

function Stop-DaryvaProcesses {
    param(
        [string]$RepoRoot,
        [bool]$ApiOnly
    )

    Write-Host "Stopping existing Daryva/API processes..." -ForegroundColor Yellow

    $pidsToStop = New-Object System.Collections.Generic.HashSet[int]

    # 1) Stop direct executable names if present
    $processNames = if ($ApiOnly) { @("Daryva.Api") } else { @("Daryva", "Daryva.Api") }
    foreach ($name in $processNames) {
        $namedProcesses = Get-Process -Name $name -ErrorAction SilentlyContinue
        foreach ($p in $namedProcesses) {
            [void]$pidsToStop.Add($p.Id)
        }
    }

    # 2) Stop dotnet processes running this repo's UI/API projects
    $apiProjectMarker = [IO.Path]::GetFullPath((Join-Path $RepoRoot "src\Daryva.Api"))
    $uiProjectMarker = [IO.Path]::GetFullPath((Join-Path $RepoRoot "src\Daryva.UI"))

    $dotnetProcesses = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'"
    foreach ($proc in $dotnetProcesses) {
        $cmd = $proc.CommandLine
        if ([string]::IsNullOrWhiteSpace($cmd)) {
            continue
        }

        if ($cmd -match [regex]::Escape($apiProjectMarker) -or (-not $ApiOnly -and $cmd -match [regex]::Escape($uiProjectMarker))) {
            [void]$pidsToStop.Add([int]$proc.ProcessId)
        }
    }

    # 3) Stop script-launched PowerShell terminals still running dotnet for this repo
    $powershellProcesses = Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'"
    foreach ($proc in $powershellProcesses) {
        $cmd = $proc.CommandLine
        if ([string]::IsNullOrWhiteSpace($cmd)) {
            continue
        }

        $isRepoTerminal = $cmd -match [regex]::Escape($apiProjectMarker) -or (-not $ApiOnly -and $cmd -match [regex]::Escape($uiProjectMarker))
        $isDotnetRunTerminal = $cmd -match "dotnet\s+run"

        if ($isRepoTerminal -and $isDotnetRunTerminal) {
            [void]$pidsToStop.Add([int]$proc.ProcessId)
        }
    }

    if ($pidsToStop.Count -eq 0) {
        Write-Host "No running Daryva/API processes found." -ForegroundColor DarkGray
        return
    }

    foreach ($processId in $pidsToStop) {
        try {
            Stop-Process -Id $processId -Force -ErrorAction Stop
            Write-Host "Stopped PID $processId" -ForegroundColor DarkYellow
        }
        catch {
            Write-Host ("Could not stop PID {0}: {1}" -f $processId, $_.Exception.Message) -ForegroundColor Red
        }
    }

    Start-Sleep -Seconds 1
}

function Test-UiStarted {
    param(
        [string]$RepoRoot,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $uiProcesses = Get-Process -Name "Daryva" -ErrorAction SilentlyContinue
        foreach ($uiProcess in $uiProcesses) {
            try {
                $uiProcess.Refresh()
                if (-not $uiProcess.HasExited -and $uiProcess.MainWindowHandle -and $uiProcess.MainWindowHandle -ne 0) {
                    return $true
                }
            }
            catch {
            }
        }

        Start-Sleep -Milliseconds 500
    }

    return $false
}

function Wait-ForUiWindow {
    param(
        [System.Diagnostics.Process]$UiProcess,
        [int]$TimeoutSeconds
    )

    if ($null -eq $UiProcess) {
        return $false
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $UiProcess.Refresh()
            if ($UiProcess.HasExited) {
                return $false
            }

            if ($UiProcess.MainWindowHandle -and $UiProcess.MainWindowHandle -ne 0) {
                return $true
            }
        }
        catch {
            return $false
        }

        Start-Sleep -Milliseconds 500
    }

    return $false
}

function Wait-ForApi {
    param(
        [int]$Port,
        [int]$TimeoutSeconds,
        [System.Diagnostics.Process]$ApiProcess,
        [string]$ApiStdOutLog,
        [string]$ApiStdErrLog
    )

    $healthUrl = "http://localhost:$($Port)/health"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        if ($null -ne $ApiProcess) {
            $ApiProcess.Refresh()
        }

        if ($null -ne $ApiProcess -and $ApiProcess.HasExited) {
            Write-Host "API process exited before health check succeeded (ExitCode=$($ApiProcess.ExitCode))." -ForegroundColor Red
            Show-LogTail -Path $ApiStdErrLog -Label "API stderr"
            Show-LogTail -Path $ApiStdOutLog -Label "API stdout"
            return $false
        }

        try {
            if (Test-ApiHealthStrict -HealthUrl $healthUrl) {
                Write-Host "API is responding at $healthUrl" -ForegroundColor Green
                return $true
            }
        }
        catch {
            Start-Sleep -Milliseconds 700
        }
    }

    Write-Host "API health endpoint did not respond within $TimeoutSeconds seconds." -ForegroundColor Yellow
    Show-LogTail -Path $ApiStdErrLog -Label "API stderr"
    Show-LogTail -Path $ApiStdOutLog -Label "API stdout"
    return $false
}

function Test-ApiHealthStrict {
    param(
        [string]$HealthUrl
    )

    try {
        $health = Invoke-RestMethod -Uri $HealthUrl -Method Get -TimeoutSec 2

        if ($null -eq $health) {
            return $false
        }

        if ($health -is [string]) {
            return $health.Trim().ToLowerInvariant().Contains("healthy")
        }

        if ($health.PSObject -and ($health.PSObject.Properties.Name -contains "status")) {
            return [string]::Equals("$($health.status)", "healthy", [System.StringComparison]::OrdinalIgnoreCase)
        }

        return $false
    }
    catch {
        return $false
    }
}

function Show-LogTail {
    param(
        [string]$Path,
        [string]$Label
    )

    if (-not (Test-Path $Path)) {
        return
    }

    Write-Host "----- $Label (last 40 lines) -----" -ForegroundColor DarkYellow
    try {
        Get-Content -Path $Path -Tail 40 | ForEach-Object { Write-Host $_ }
    }
    catch {
        Write-Host "Could not read $Label log: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

function Set-UiApiBaseUrl {
    param(
        [string]$UiProjectDir,
        [int]$ApiPort
    )

    $apiBaseUrl = "http://localhost:$ApiPort"
    $projectLocalConfig = Join-Path $UiProjectDir "app.config.local.json"
    $exampleConfig = Join-Path $UiProjectDir "app.config.local.example.json"
    $outputLocalConfig = Join-Path $UiProjectDir "bin\Debug\net8.0\app.config.local.json"

    $configPath = $projectLocalConfig
    if (-not (Test-Path $configPath)) {
        if (Test-Path $exampleConfig) {
            Copy-Item -Path $exampleConfig -Destination $configPath -Force
            Write-Host "Created UI config from example at $configPath" -ForegroundColor DarkGray
        }
        else {
            Write-Host "No app.config.local.json or example found; UI will use default ApiBaseUrl." -ForegroundColor Yellow
            return
        }
    }

    try {
        $json = Get-Content -Raw -Path $configPath -Encoding UTF8
        $config = $json | ConvertFrom-Json
        if (-not $config.PSObject.Properties["AppSettings"]) {
            $config | Add-Member -NotePropertyName "AppSettings" -NotePropertyValue (New-Object PSObject) -Force
        }
        $config.AppSettings.ApiBaseUrl = $apiBaseUrl
        $config | ConvertTo-Json -Depth 10 | Set-Content -Path $configPath -Encoding UTF8 -NoNewline
        Write-Host "UI ApiBaseUrl set to $apiBaseUrl" -ForegroundColor Green
    }
    catch {
        Write-Host "Could not update UI ApiBaseUrl: $($_.Exception.Message)" -ForegroundColor Yellow
        return
    }

    if (Test-Path $outputLocalConfig) {
        try {
            Copy-Item -Path $configPath -Destination $outputLocalConfig -Force
        }
        catch {
            # Non-fatal
        }
    }
}

function Update-ApiDatabase {
    param(
        [string]$RepoRoot,
        [string]$ApiProjectPath
    )

    Write-Host "Applying API database migrations..." -ForegroundColor Cyan

    $migrationArgs = @(
        "ef", "database", "update",
        "--project", $ApiProjectPath,
        "--startup-project", $ApiProjectPath
    )

    $migrationOutput = & dotnet @migrationArgs 2>&1
    $exitCode = $LASTEXITCODE

    if ($migrationOutput) {
        $migrationOutput | ForEach-Object { Write-Host $_ }
    }

    if ($exitCode -ne 0) {
        throw "Failed to apply API migrations (exit code $exitCode)."
    }

    Write-Host "API migrations applied successfully." -ForegroundColor Green
}

$repoRoot = Get-RepoRoot
$apiProjectDir = Join-Path $repoRoot "src\Daryva.Api"
$uiProjectDir = Join-Path $repoRoot "src\Daryva.UI"
$apiProjectPath = Join-Path $apiProjectDir "Daryva.Api.csproj"
$uiProjectPath = Join-Path $uiProjectDir "Daryva.csproj"
$apiProjectFile = "Daryva.Api.csproj"
$uiProjectFile = "Daryva.csproj"

if (-not (Test-Path $apiProjectPath)) {
    throw "API project not found at $apiProjectDir"
}

if (-not $ApiOnly -and -not (Test-Path $uiProjectPath)) {
    throw "UI project not found at $uiProjectDir"
}

Stop-DaryvaProcesses -RepoRoot $repoRoot -ApiOnly:$ApiOnly

if (-not $SkipMigrations) {
    Update-ApiDatabase -RepoRoot $repoRoot -ApiProjectPath $apiProjectPath
}
else {
    Write-Host "Skipping API migrations (--SkipMigrations)." -ForegroundColor Yellow
}

$launchWithVisibleTerminals = $ShowTerminals
$launchWithVisibleApiTerminal = $ShowApiTerminal -or $ShowTerminals

if ($launchWithVisibleApiTerminal) {
    Write-Host "API launch: visible terminal" -ForegroundColor DarkGray
}
else {
    Write-Host "API launch: background" -ForegroundColor DarkGray
}

if ($launchWithVisibleTerminals) {
    Write-Host "UI launch: visible terminal" -ForegroundColor DarkGray
}
else {
    Write-Host "UI launch: executable first, then visible terminal fallback" -ForegroundColor DarkGray
}

Write-Host "Starting API..." -ForegroundColor Cyan
$logsDir = Join-Path $repoRoot "artifacts\logs"
if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir | Out-Null
}

$apiStdOutLog = Join-Path $logsDir "api-stdout.log"
$apiStdErrLog = Join-Path $logsDir "api-stderr.log"
$apiVisibleLog = Join-Path $logsDir "api-visible.log"

$apiProcess = $null
if ($launchWithVisibleApiTerminal) {
    $apiCmd = "Set-Location -Path '$($apiProjectDir.Replace("'","''"))'; dotnet run --project $apiProjectFile *>&1 | Tee-Object -FilePath '$($apiVisibleLog.Replace("'","''"))'; exit `$LASTEXITCODE"
    $apiTerminal = Start-Process -FilePath "powershell" -ArgumentList @("-ExecutionPolicy", "Bypass", "-Command", $apiCmd) -PassThru
    $apiProcess = $apiTerminal
    Write-Host "API terminal started (PID $($apiTerminal.Id))" -ForegroundColor DarkCyan
}
else {
    $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", $apiProjectFile) -WorkingDirectory $apiProjectDir -PassThru -RedirectStandardOutput $apiStdOutLog -RedirectStandardError $apiStdErrLog
    Write-Host "API started (PID $($apiProcess.Id))" -ForegroundColor DarkCyan
}

# When using local API, point UI at it; otherwise leave UI config (e.g. VPS) unchanged.
if ($UseLocalApi) {
    Set-UiApiBaseUrl -UiProjectDir $uiProjectDir -ApiPort $ApiPort
}

$apiReady = Wait-ForApi -Port $ApiPort -TimeoutSeconds $ApiStartupTimeoutSeconds -ApiProcess $apiProcess -ApiStdOutLog $apiStdOutLog -ApiStdErrLog $apiStdErrLog
if (-not $apiReady) {
    throw "API failed to become healthy. See logs at $apiStdOutLog and $apiStdErrLog"
}

if (-not $ApiOnly) {
    Write-Host "Starting UI app..." -ForegroundColor Cyan
    $uiStarted = $false

    if ($launchWithVisibleTerminals) {
        $uiCmd = "Set-Location -Path '$($uiProjectDir.Replace("'","''"))'; dotnet run --project $uiProjectFile"
        $uiTerminal = Start-Process -FilePath "powershell" -ArgumentList @("-NoExit", "-ExecutionPolicy", "Bypass", "-Command", $uiCmd) -PassThru
        Write-Host "UI terminal started (PID $($uiTerminal.Id))" -ForegroundColor DarkCyan

        $uiStarted = Test-UiStarted -RepoRoot $repoRoot -TimeoutSeconds $UiStartupTimeoutSeconds
        if (-not $uiStarted) {
            throw "UI process was not detected within $UiStartupTimeoutSeconds seconds. Check the UI terminal window for build/runtime errors."
        }
    }

    if (-not $launchWithVisibleTerminals) {
        $uiExePath = Join-Path $repoRoot "src\Daryva.UI\bin\Debug\net8.0\Daryva.exe"
        if (Test-Path $uiExePath) {
            $uiExeProcess = Start-Process -FilePath $uiExePath -WorkingDirectory (Split-Path -Parent $uiExePath) -PassThru
            $uiWindowVisible = Wait-ForUiWindow -UiProcess $uiExeProcess -TimeoutSeconds $UiStartupTimeoutSeconds

            if ($uiWindowVisible) {
                $uiStarted = $true
                Write-Host "UI started via executable (PID $($uiExeProcess.Id), window visible)" -ForegroundColor DarkCyan
            }
            else {
                try {
                    $uiExeProcess.Refresh()
                    if (-not $uiExeProcess.HasExited) {
                        Stop-Process -Id $uiExeProcess.Id -Force -ErrorAction SilentlyContinue
                    }
                }
                catch {
                }

                Write-Host "UI executable did not show a window in time. Falling back to visible dotnet run terminal." -ForegroundColor Yellow
            }
        }

        if (-not $uiStarted) {
            $uiCmd = "Set-Location -Path '$($uiProjectDir.Replace("'","''"))'; dotnet run --project $uiProjectFile"
            $uiTerminal = Start-Process -FilePath "powershell" -ArgumentList @("-NoExit", "-ExecutionPolicy", "Bypass", "-Command", $uiCmd) -PassThru
            Write-Host "UI terminal started (fallback, PID $($uiTerminal.Id))" -ForegroundColor DarkCyan

            $uiStarted = Test-UiStarted -RepoRoot $repoRoot -TimeoutSeconds $UiStartupTimeoutSeconds
            if (-not $uiStarted) {
                throw "Could not start UI. Check the UI terminal window for errors."
            }
        }
    }

    if (-not $uiStarted) {
        throw "UI did not start successfully."
    }
}

if ($ApiOnly) {
    Write-Host "Done. Fresh API instance launched." -ForegroundColor Green
}
else {
    Write-Host "Done. Fresh API + UI instances launched." -ForegroundColor Green
}
