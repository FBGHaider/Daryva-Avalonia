# Convenience launcher: run the dev restart script with all arguments passed through.
& (Join-Path $PSScriptRoot "restart-dev.ps1") @args
