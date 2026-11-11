#requires -Version 5.1
<#
    CubeGame Windows release build (M0-F5).

    Builds the committed project exactly as it is (Pilot scene from EditorBuildSettings, no
    gameplay changes) into
        <LauncherRoot>\releases\<build-id>\CubeGame.exe
    where build-id is the short git SHA of the build commit. The stable launcher selects that
    build through current.txt; this script never touches the launcher binary.

    Usage:
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\release\build_windows_release.ps1
      powershell ... -LauncherRoot D:\Games\CubeGame

    PASS requires: exit code 0, markers [ReleaseBuild] BUILD_SUCCEEDED and RELEASE_READY, CubeGame.exe,
    CubeGame_Data and UnityPlayer.dll present, no error CS / failed build result, and the local Alpha
    content staged into CubeGame_Data\StreamingAssets\CubeGame\AlphaContent (build FAILS without it).
#>

[CmdletBinding()]
param(
    [string]$LauncherRoot = 'D:\Games\CubeGame',
    [string]$EditorVersion = '6000.3.27f2',
    [string]$BuildId = '',
    # M0 runtime recovery: build into an explicit folder instead of the launcher releases folder.
    [string]$OutputDir = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
if (-not (Test-Path (Join-Path $repoRoot 'Assets\CubeGame'))) {
    throw "Unity project not found at $repoRoot"
}

$projectVersionFile = Join-Path $repoRoot 'ProjectSettings\ProjectVersion.txt'
$projectVersion = ((Select-String -Path $projectVersionFile -Pattern '^m_EditorVersion:\s*(\S+)').Matches |
    Select-Object -First 1).Groups[1].Value
if ($projectVersion -ne $EditorVersion) {
    throw "Project editor version '$projectVersion' does not match approved '$EditorVersion'"
}

$cli = Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'
if (-not (Test-Path $cli)) { throw "Unity CLI not found: $cli" }
$editorRoot = (& $cli editors path $EditorVersion --no-banner | Where-Object { $_.Trim() } | Select-Object -Last 1)
$editorExe = if ($editorRoot) { Join-Path $editorRoot.Trim() 'Editor\Unity.exe' } else { '' }
if (-not $editorExe -or -not (Test-Path $editorExe)) {
    # The hub CLI only reports editors it installed itself; a hub moved to another drive is not in that list.
    $candidates = @()
    if ($env:CUBEGAME_UNITY_EDITOR) { $candidates += $env:CUBEGAME_UNITY_EDITOR }
    $candidates += @(
        (Join-Path (Join-Path (Join-Path 'D:\Unity\Hub\Editor' $EditorVersion) 'Editor') 'Unity.exe'),
        (Join-Path (Join-Path (Join-Path 'C:\Program Files\Unity\Hub\Editor' $EditorVersion) 'Editor') 'Unity.exe'),
        (Join-Path (Join-Path (Join-Path 'D:\Program Files\Unity\Hub\Editor' $EditorVersion) 'Editor') 'Unity.exe')
    )
    $found = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $found) {
        throw "Unity Editor not found for $EditorVersion (CLI list is empty; checked CUBEGAME_UNITY_EDITOR and the hub folders on C: and D:)"
    }
    $editorExe = $found
    Write-Host "BUILD editor_source=fallback"
}

# The Alpha interface font is a closed local asset (resource1.dat): stage it into Resources before the import so
# the Player keeps the Alpha typography. A missing local installation must not break the build, but it must be
# visible in the output and in the Player log (ALPHA_FONT=ABSENT).
$fontStage = Join-Path $repoRoot 'tools\alpha_content\stage_alpha_ui_font.ps1'
if (Test-Path $fontStage) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $fontStage -AllowAbsent | ForEach-Object { Write-Host "BUILD font $_" }
}

if (-not $BuildId) {
    $BuildId = (& git -C $repoRoot rev-parse --short=7 HEAD).Trim()
}
if (-not $BuildId) { throw 'Cannot determine build id (git rev-parse failed)' }

$releaseDir = if ($OutputDir) { $OutputDir } else { Join-Path $LauncherRoot (Join-Path 'releases' $BuildId) }
$logs = Join-Path $repoRoot '.unity-install\logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $logs "f5_build_$($BuildId)_$stamp.log"

Write-Host "BUILD repo=$repoRoot"
Write-Host "BUILD editor=$editorExe"
Write-Host "BUILD commit=$(& git -C $repoRoot rev-parse HEAD)"
Write-Host "BUILD id=$BuildId"
Write-Host "BUILD output=$releaseDir"
Write-Host "BUILD log=$logPath"

$env:CUBEGAME_RELEASE_DIR = $releaseDir
$env:CUBEGAME_BUILD_COMMIT = (& git -C $repoRoot rev-parse HEAD).Trim()
$arguments = @(
    '-batchmode',
    '-quit',
    '-nographics',
    '-projectPath', $repoRoot,
    '-executeMethod', 'CubeGame.EditorTools.ReleaseBuild.BuildWindows64',
    '-logFile', $logPath
)

$process = Start-Process -FilePath $editorExe -ArgumentList $arguments -Wait -PassThru
$exitCode = $process.ExitCode
$text = if (Test-Path $logPath) { Get-Content -Raw -LiteralPath $logPath } else { '' }

$succeeded = ($text -match '\[ReleaseBuild\] BUILD_SUCCEEDED') -and ($text -match '\[ReleaseBuild\] RELEASE_READY')
$contentGate = if ($text -match 'CONTENT_GATE_FAIL|Alpha content missing') { 'FAIL' } elseif ($text -match '\[ReleaseBuild\] CONTENT_STAGED') { 'PASS' } else { 'UNKNOWN' }
Write-Host "BUILD content_gate=$contentGate"
$failedResult = $text -match "Build completed with a result of 'Failed'"
$compileErrors = ([regex]::Matches($text, 'error CS\d+')).Count
$executable = Join-Path $releaseDir 'CubeGame.exe'
$dataDir = Join-Path $releaseDir 'CubeGame_Data'
$unityPlayer = Join-Path $releaseDir 'UnityPlayer.dll'

Write-Host "BUILD exit=$exitCode marker=$succeeded failedResult=$failedResult errorCS=$compileErrors"

if ($exitCode -ne 0 -or -not $succeeded -or $failedResult -or $compileErrors -gt 0) {
    Write-Host 'BUILD_RESULT=FAIL'
    exit 1
}
if (-not (Test-Path $executable) -or -not (Test-Path $dataDir) -or -not (Test-Path $unityPlayer)) {
    Write-Host "BUILD_RESULT=FAIL missing player files: exe=$(Test-Path $executable) data=$(Test-Path $dataDir) unityplayer=$(Test-Path $unityPlayer)"
    exit 1
}

$totalBytes = (Get-ChildItem -Recurse -File -LiteralPath $releaseDir | Measure-Object -Sum Length).Sum
Write-Host "BUILD_RESULT=PASS output=$executable exe_size=$((Get-Item $executable).Length) total_size=$totalBytes"
Write-Host "BUILD_RELEASE_DIR=$releaseDir"
Write-Host "BUILD_CURRENT_ENTRY=releases\$BuildId\CubeGame.exe"
