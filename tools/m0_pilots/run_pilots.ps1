#requires -Version 5.1
<#
    M0-00 preparation lab: compiles the real CubeGame.Core sources plus the isolated pilot
    programs with the Roslyn compiler shipped with the installed Unity, then runs the pilots
    without starting the Unity Editor.

    It does not change Assets/, Packages/ or ProjectSettings/ and does not touch Alpha.
    Artifacts go to .unity-install\m0_pilots\ (gitignored).
#>

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
while ($projectRoot -and -not (Test-Path (Join-Path $projectRoot 'Assets\CubeGame\Core'))) {
    $parent = Split-Path -Parent $projectRoot
    if (-not $parent -or $parent -eq $projectRoot) { break }
    $projectRoot = $parent
}
if (-not $projectRoot) { throw 'CubeGame project root not found.' }

$coreDir = Join-Path $projectRoot 'Assets\CubeGame\Core'
$pilotsDir = Join-Path $projectRoot 'tools\m0_pilots'
$outDir = Join-Path $projectRoot '.unity-install\m0_pilots'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$unityData = 'D:\Unity\Hub\Editor\6000.3.27f2\Editor\Data'
$csc = Join-Path $unityData 'DotNetSdkRoslyn\csc.dll'
$mono = Join-Path $unityData 'MonoBleedingEdge\lib\mono\4.5'
if (-not (Test-Path $csc)) { throw "Roslyn not found: $csc" }
if (-not (Test-Path (Join-Path $mono 'mscorlib.dll'))) { throw "Mono assemblies not found: $mono" }

$sources = @()
$sources += Get-ChildItem $coreDir -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$sources += Get-ChildItem $pilotsDir -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\Program\.cs$' } | ForEach-Object { $_.FullName }
$sources += (Join-Path $pilotsDir 'Program.cs')

$exe = Join-Path $outDir 'M0Pilots.exe'
$log = Join-Path $outDir 'm0_pilots.log'

$cscArgs = @(
    $csc,
    '/nologo',
    '/langversion:latest',
    '/target:exe',
    "/out:$exe",
    "/r:$(Join-Path $mono 'mscorlib.dll')",
    "/r:$(Join-Path $mono 'System.dll')",
    "/r:$(Join-Path $mono 'System.Core.dll')"
) + $sources

Write-Host "Compiling Core + M0 pilots ($($sources.Count) sources) -> $exe"
& dotnet @cscArgs 2>&1 | Tee-Object -FilePath $log
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) {
    Write-Host 'Compilation failed - see log.' -ForegroundColor Red
    exit 20
}

$monoExe = Join-Path $unityData 'MonoBleedingEdge\bin\mono.exe'
if (-not (Test-Path $monoExe)) { throw "Mono runtime not found: $monoExe" }

Write-Host "`nRunning pilots under Unity Mono:"
& $monoExe $exe 2>&1 | Tee-Object -FilePath $log -Append
$code = $LASTEXITCODE
Write-Host "`nResult: exit=$code, log: $log"
exit $code
