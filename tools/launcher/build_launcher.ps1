#requires -Version 5.1
<#
    CubeGame stable launcher build script (F5).

    Compiles tools/launcher/CubeGameLauncher.cs with the .NET Framework compiler that ships
    with Windows. No Unity, no SDK, no extra runtime installation is needed.

    The launcher is immutable once frozen (see docs/decisions/D91_STABLE_LAUNCHER_CONTRACT.md):
    run this only while preparing a launcher, never after LAUNCHER_STATUS=PASS is reported.

    Usage:
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\launcher\build_launcher.ps1 `
        -OutputPath D:\Games\CubeGame\CubeGameLauncher.exe
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $PSCommandPath
$source = Join-Path $scriptRoot 'CubeGameLauncher.cs'
if (-not (Test-Path $source)) { throw "Launcher source not found: $source" }

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found: $csc" }

$outputFull = [System.IO.Path]::GetFullPath($OutputPath)
$outputDir = Split-Path -Parent $outputFull
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$arguments = @(
    '/nologo',
    '/noconfig',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    "/out:$outputFull",
    '/r:System.dll',
    '/r:System.Windows.Forms.dll',
    $source
)

& $csc $arguments
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
if (-not (Test-Path $outputFull)) { throw "Launcher was not produced: $outputFull" }

$hash = (Get-FileHash -Algorithm SHA256 -Path $outputFull).Hash.ToLowerInvariant()
$size = (Get-Item $outputFull).Length
Write-Host "LAUNCHER_BUILT path=$outputFull size=$size sha256=$hash"
