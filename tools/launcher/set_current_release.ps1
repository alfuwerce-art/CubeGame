#requires -Version 5.1
<#
    Selects the active CubeGame build by rewriting ONLY current.txt.

    This is the whole update mechanism of the frozen launcher:
        current.txt -> releases\<build-id>\CubeGame.exe
    The launcher binary and the desktop shortcut stay untouched.

    Usage:
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\launcher\set_current_release.ps1 -BuildId 60f24be
#>

[CmdletBinding()]
param(
    [string]$LauncherRoot = 'D:\Games\CubeGame',
    [Parameter(Mandatory = $true)]
    [string]$BuildId
)

$ErrorActionPreference = 'Stop'

$releaseDir = Join-Path $LauncherRoot (Join-Path 'releases' $BuildId)
$executable = Join-Path $releaseDir 'CubeGame.exe'
if (-not (Test-Path $executable)) { throw "Build $BuildId is not installed: $executable" }

$entry = "releases\$BuildId\CubeGame.exe"
$currentPath = Join-Path $LauncherRoot 'current.txt'
Set-Content -LiteralPath $currentPath -Value $entry -Encoding ASCII -NoNewline

$written = (Get-Content -Raw -LiteralPath $currentPath).Trim()
Write-Host "CURRENT_TARGET=$written"
if ($written -ne $entry) { throw "current.txt mismatch: '$written' != '$entry'" }
