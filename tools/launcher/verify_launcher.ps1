#requires -Version 5.1
<#
    Verifies that the installed launcher is still the frozen one.

    Reads launcher.sha256 next to the launcher, recomputes SHA-256 of CubeGameLauncher.exe and
    fails on any difference. The launcher is immutable (D91): a mismatch is a contract
    violation, not a reason to rewrite the hash file.

    Usage:
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\launcher\verify_launcher.ps1
#>

[CmdletBinding()]
param(
    [string]$LauncherRoot = 'D:\Games\CubeGame'
)

$ErrorActionPreference = 'Stop'

$launcher = Join-Path $LauncherRoot 'CubeGameLauncher.exe'
$hashFile = Join-Path $LauncherRoot 'launcher.sha256'
if (-not (Test-Path $launcher)) { throw "Launcher not found: $launcher" }
if (-not (Test-Path $hashFile)) { throw "Hash file not found: $hashFile" }

$expected = ((Get-Content -Raw -LiteralPath $hashFile).Trim() -split '\s+')[0].ToLowerInvariant()
$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $launcher).Hash.ToLowerInvariant()

Write-Host "LAUNCHER=$launcher"
Write-Host "LAUNCHER_SHA256_EXPECTED=$expected"
Write-Host "LAUNCHER_SHA256_ACTUAL=$actual"
if ($actual -ne $expected) {
    Write-Host 'LAUNCHER_IMMUTABLE=FAIL'
    exit 1
}

Write-Host 'LAUNCHER_IMMUTABLE=PASS'
