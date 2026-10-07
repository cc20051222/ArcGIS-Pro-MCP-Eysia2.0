# ArcGIS Pro MCP - dev environment PATH helper (current PowerShell session only)
#
# Prepends the user-level .NET 8 SDK directory to the current session PATH.
# It does NOT modify machine-level or user-level environment variables,
# and does NOT touch the existing system PATH.
#
# Usage (PowerShell):
#     . .\scripts\dev-env.ps1
#
# Effect lasts only for the current PowerShell process.

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$dotnetExe = Join-Path $dotnetDir 'dotnet.exe'

if (Test-Path $dotnetExe) {
    if ($env:PATH -notlike "*$dotnetDir*") {
        $env:PATH = "$dotnetDir;$env:PATH"
        Write-Host "[dev-env] PATH prepended: $dotnetDir" -ForegroundColor Green
    } else {
        Write-Host "[dev-env] PATH already contains: $dotnetDir" -ForegroundColor Green
    }
    & $dotnetExe --version
} else {
    Write-Host "[dev-env] user-level .NET SDK not found: $dotnetExe" -ForegroundColor Red
    Write-Host "[dev-env] install it first to $dotnetDir"
}
