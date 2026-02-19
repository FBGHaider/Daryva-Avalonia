param(
    [int]$ApiPort = 5000,
    [int]$ApiStartupTimeoutSeconds = 45,
    [switch]$ApiOnly,
    [switch]$ShowTerminals
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

if (-not (Test-Path $uiProjectPath)) {
    throw "UI project not found at $uiProjectDir"
}

Stop-DaryvaProcesses -RepoRoot $repoRoot -ApiOnly:$ApiOnly

$launchWithVisibleTerminals = $ShowTerminals -or (-not $ApiOnly)

Write-Host "Starting API..." -ForegroundColor Cyan
$logsDir = Join-Path $repoRoot "artifacts\logs"
if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir | Out-Null
}

$apiStdOutLog = Join-Path $logsDir "api-stdout.log"
$apiStdErrLog = Join-Path $logsDir "api-stderr.log"

$apiProcess = $null
if ($launchWithVisibleTerminals) {
    $apiCmd = "Set-Location -Path '$($apiProjectDir.Replace("'","''"))'; dotnet run --project $apiProjectFile"
    $apiTerminal = Start-Process -FilePath "powershell" -ArgumentList @("-NoExit", "-ExecutionPolicy", "Bypass", "-Command", $apiCmd) -PassThru
    Write-Host "API terminal started (PID $($apiTerminal.Id))" -ForegroundColor DarkCyan
}
else {
    $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", $apiProjectFile) -WorkingDirectory $apiProjectDir -PassThru -RedirectStandardOutput $apiStdOutLog -RedirectStandardError $apiStdErrLog
    Write-Host "API started (PID $($apiProcess.Id))" -ForegroundColor DarkCyan
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

        Start-Sleep -Seconds 4
        $uiStarted = (Get-Process -Name "Daryva" -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0
        if (-not $uiStarted) {
            Write-Host "UI process not detected yet. Check the UI terminal window for build/runtime errors." -ForegroundColor Yellow
        }
    }

    if (-not $launchWithVisibleTerminals) {
        $uiExePath = Join-Path $repoRoot "src\Daryva.UI\bin\Debug\net8.0\Daryva.exe"
        if (Test-Path $uiExePath) {
            $uiExeProcess = Start-Process -FilePath $uiExePath -WorkingDirectory (Split-Path -Parent $uiExePath) -PassThru
            Start-Sleep -Seconds 3
            $uiExeProcess.Refresh()

            if (-not $uiExeProcess.HasExited) {
                $uiStarted = $true
                Write-Host "UI started via executable (PID $($uiExeProcess.Id))" -ForegroundColor DarkCyan
            }
            else {
                Write-Host "UI executable exited immediately (ExitCode=$($uiExeProcess.ExitCode)). Falling back to dotnet run." -ForegroundColor Yellow
            }
        }

        if (-not $uiStarted) {
            Write-Host "UI executable not found. Falling back to dotnet run (hidden)." -ForegroundColor Yellow
            $uiStdOutLog = Join-Path $logsDir "ui-stdout.log"
            $uiStdErrLog = Join-Path $logsDir "ui-stderr.log"
            $uiProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", $uiProjectFile) -WorkingDirectory $uiProjectDir -PassThru -RedirectStandardOutput $uiStdOutLog -RedirectStandardError $uiStdErrLog
            Start-Sleep -Seconds 3
            $uiProcess.Refresh()

            if ($uiProcess.HasExited) {
                Write-Host "UI process exited immediately (ExitCode=$($uiProcess.ExitCode))." -ForegroundColor Red
                Show-LogTail -Path $uiStdErrLog -Label "UI stderr"
                Show-LogTail -Path $uiStdOutLog -Label "UI stdout"
                throw "Could not start UI. See UI logs at $uiStdOutLog and $uiStdErrLog"
            }
            else {
                $uiStarted = $true
                Write-Host "UI started via dotnet run (PID $($uiProcess.Id))" -ForegroundColor DarkCyan
            }
        }
    }

    if (-not $uiStarted -and -not $launchWithVisibleTerminals) {
        throw "UI did not start successfully."
    }
}

if ($ApiOnly) {
    Write-Host "Done. Fresh API instance launched." -ForegroundColor Green
}
else {
    Write-Host "Done. Fresh API + UI instances launched." -ForegroundColor Green
}
