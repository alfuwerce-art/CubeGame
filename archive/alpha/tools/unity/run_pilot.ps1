#requires -Version 5.1
<#
    CubeGame S01-PILOT - automated Unity 6000.3.27f2 verification.

    This file is intentionally ASCII-only: Windows PowerShell 5.1 reads a UTF-8 file
    without a BOM as ANSI, and non-ASCII text then breaks parsing. Keep it ASCII.

    What it does:
      0. License: reads "unity license status --json" instead of guessing from log lines.
      1. Import/compile: PASS only when exit code is 0 AND the log has no "error CS" /
         "Scripts have compiler errors" / other failure markers.
      2. EditMode and PlayMode tests through the Unity CLI, verified by parsing the NUnit
         XML (total > 0, failed = 0, passed > 0). The exit code alone is not a PASS.
      3. Scene build: marker SCENE_SAVED plus an existing scene file.
      4. Windows player build: marker BUILD_SUCCEEDED, .exe and _Data folder.
      5. Player run: screenshot file plus "[Pilot] screenshot requested" and "[Pilot] quitting".

    It does NOT install software, change the Editor version, open Unity 6.6, touch other
    projects or delete anything outside .unity-install\.

    Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools\unity\run_pilot.ps1
#>

[CmdletBinding()]
param(
    [string]$EditorVersion = '6000.3.27f2',
    [switch]$SkipPlayerRun
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# ---------------------------------------------------------------- paths ----
$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
while ($projectRoot -and -not (Test-Path (Join-Path $projectRoot 'Assets\CubeGame'))) {
    $parent = Split-Path -Parent $projectRoot
    if (-not $parent -or $parent -eq $projectRoot) { break }
    $projectRoot = $parent
}
if (-not $projectRoot -or -not (Test-Path (Join-Path $projectRoot 'Assets\CubeGame'))) {
    throw "Unity project not found (expected Assets\CubeGame). Run this script from the CubeGame repository."
}

$install = Join-Path $projectRoot '.unity-install'
$logs = Join-Path $install 'logs'
$results = Join-Path $install 'results'
$shots = Join-Path $install 'shots'
$buildDir = Join-Path $install 'build'
foreach ($dir in @($logs, $results, $shots, $buildDir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$projectVersionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
$summaryPath = Join-Path $install 'run_summary.json'
$steps = New-Object System.Collections.Generic.List[object]
$blockers = New-Object System.Collections.Generic.List[string]

# ------------------------------------------------------------ helpers ----
function Add-Step {
    param(
        [string]$Name,
        [string]$Status,
        [int]$ExitCode = -1,
        [double]$Seconds = 0,
        [string]$Evidence = '',
        [string]$Log = ''
    )
    $steps.Add([pscustomobject]@{
            Step     = $Name
            Status   = $Status
            ExitCode = $ExitCode
            Seconds  = [math]::Round($Seconds, 1)
            Evidence = $Evidence
            Log      = $Log
        })
    $color = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } default { 'Yellow' } }
    Write-Host ("[{0}] {1} exit={2} {3}" -f $Status, $Name, $ExitCode, $Evidence) -ForegroundColor $color
}

function Quote-Arg {
    param([string]$Value)
    if ($Value -match '[\s"]') {
        return '"' + ($Value -replace '"', '\"') + '"'
    }
    return $Value
}

function ConvertTo-ArgumentList {
    param([string[]]$Arguments)
    return (($Arguments | ForEach-Object { Quote-Arg $_ }) -join ' ')
}

function Invoke-UnityStep {
    <# Runs Unity.exe and WAITS for it. A direct "& Unity.exe" call returns immediately for
       this GUI-subsystem executable, so Start-Process -Wait is required here. #>
    param(
        [string]$Name,
        [string[]]$UnityArgs,
        [int[]]$AcceptExitCodes = @(0)
    )

    $log = Join-Path $logs "$Name.log"
    $fullArgs = (ConvertTo-ArgumentList (@('-logFile', $log) + $UnityArgs))
    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    Write-Host "Unity.exe $fullArgs"

    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $editorExe -ArgumentList $fullArgs -PassThru -Wait -NoNewWindow
    $timer.Stop()
    $code = $process.ExitCode
    $problems = Get-LogProblems -Path $log
    $ok = ($AcceptExitCodes -contains $code) -and ($problems.Count -eq 0)
    $evidence = if ($problems.Count -gt 0) { 'log problems: ' + ($problems -join ' | ') } else { '' }
    Add-Step -Name $Name -Status $(if ($ok) { 'PASS' } else { 'FAIL' }) -ExitCode $code `
        -Seconds $timer.Elapsed.TotalSeconds -Evidence $evidence -Log $log
    return [pscustomobject]@{ Ok = $ok; ExitCode = $code; Log = $log; Problems = $problems }
}

function Get-LogProblems {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return @('log file missing') }
    $patterns = 'error CS\d{4}|Scripts have compiler errors|Aborting batchmode due to failure|' +
    'No valid Unity Editor license|Fatal error|BATCH_FAIL|executeMethod.*not found|Build completed with a result of .Failed.'
    $matches = Select-String -Path $Path -Pattern $patterns -Encoding UTF8 -ErrorAction SilentlyContinue
    if (-not $matches) { return @() }
    return @($matches | Select-Object -First 5 | ForEach-Object { $_.Line.Trim() })
}

function Get-EditorVersionFromLog {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    $line = Select-String -Path $Path -Pattern 'Initialize engine version: (\S+)' -Encoding UTF8 -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $line -or $line.Line -notmatch 'Initialize engine version: (\S+)') { return $null }
    return $Matches[1]
}

function Get-TestSummary {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    try {
        [xml]$xml = Get-Content -Path $Path -Raw -Encoding UTF8
    }
    catch {
        return $null
    }
    $run = $xml.'test-run'
    if ($null -eq $run) { return $null }
    return [pscustomobject]@{
        Total   = [int]$run.total
        Passed  = [int]$run.passed
        Failed  = [int]$run.failed
        Skipped = [int]$run.skipped
        Result  = [string]$run.result
    }
}

function Test-TestSummary {
    param([object]$Summary)
    return ($null -ne $Summary) -and ($Summary.Total -gt 0) -and ($Summary.Failed -eq 0) -and ($Summary.Passed -gt 0)
}

function Invoke-Cli {
    <# Runs the Unity CLI with separate stdout/stderr. A direct "& unity.exe ..." call fails
       under $ErrorActionPreference='Stop' as soon as the CLI writes a service line to stderr. #>
    param([string[]]$Arguments, [string]$Name)

    $outLog = Join-Path $logs "$Name.out.log"
    $errLog = Join-Path $logs "$Name.err.log"
    $process = Start-Process -FilePath $cliExe -ArgumentList (ConvertTo-ArgumentList $Arguments) `
        -PassThru -Wait -NoNewWindow -RedirectStandardOutput $outLog -RedirectStandardError $errLog
    $out = if (Test-Path $outLog) { Get-Content $outLog -Raw -Encoding UTF8 } else { '' }
    $err = if (Test-Path $errLog) { Get-Content $errLog -Raw -Encoding UTF8 } else { '' }
    return [pscustomobject]@{ ExitCode = $process.ExitCode; Out = "$out"; Err = "$err"; OutLog = $outLog; ErrLog = $errLog }
}

function Invoke-CliTest {
    param([string]$Mode, [string]$XmlPath)

    $cliArgs = @('test', $projectRoot, '--mode', $Mode, '--output', $XmlPath,
        '--editor-version', $EditorVersion, '--timeout', '1200',
        '--no-banner', '--non-interactive', '--', '-nographics')

    Write-Host "`n=== tests $Mode (Unity CLI) ===" -ForegroundColor Cyan
    Write-Host ("unity.exe " + ($cliArgs -join ' '))
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $run = Invoke-Cli -Arguments $cliArgs -Name ("2_tests_{0}_cli" -f $Mode.ToLowerInvariant())
    $code = $run.ExitCode
    $timer.Stop()

    $summary = Get-TestSummary -Path $XmlPath
    $ok = ($code -eq 0) -and (Test-TestSummary -Summary $summary)
    $evidence = if ($null -eq $summary) {
        "NUnit XML missing/unreadable (cli exit=$code)"
    }
    else {
        "total=$($summary.Total) passed=$($summary.Passed) failed=$($summary.Failed) skipped=$($summary.Skipped) result=$($summary.Result)"
    }
    if (-not $ok -and $run.Err) {
        $evidence = $evidence + ' | stderr: ' + (($run.Err -split "`n" | Select-Object -First 3) -join ' / ')
    }
    Add-Step -Name ("2_tests_{0}" -f $Mode.ToLowerInvariant()) `
        -Status $(if ($ok) { 'PASS' } else { 'FAIL' }) -ExitCode $code `
        -Seconds $timer.Elapsed.TotalSeconds -Evidence $evidence -Log $run.OutLog
    return [pscustomobject]@{ Ok = $ok; ExitCode = $code; Summary = $summary; Log = $run.OutLog; ErrorLog = $run.ErrLog }
}

# --------------------------------------------------------- preconditions ----
Write-Host '=== 0. Preconditions ===' -ForegroundColor Cyan

$cliExe = Join-Path $env:LOCALAPPDATA 'Unity\bin\unity.exe'
if (-not (Test-Path $cliExe)) {
    $cmd = Get-Command unity -ErrorAction SilentlyContinue
    if ($cmd) { $cliExe = $cmd.Source } else { $cliExe = $null }
}
if (-not $cliExe) {
    Add-Step -Name '0_cli' -Status 'FAIL' -Evidence 'Unity CLI not found (AppData\Local\Unity\bin\unity.exe)'
    throw 'Unity CLI not found. It is needed to check the license and the exact Editor version.'
}
$versionRun = Invoke-Cli -Arguments @('--version', '--no-banner') -Name '0_cli_version'
$cliVersion = ($versionRun.Out.Trim() -split "`n" | Where-Object { $_.Trim() } | Select-Object -First 1)
Add-Step -Name '0_cli' -Status 'PASS' -ExitCode $versionRun.ExitCode -Evidence "version=$cliVersion path=$cliExe"

$projectVersionLine = if (Test-Path $projectVersionFile) {
    (Select-String -Path $projectVersionFile -Pattern '^m_EditorVersion:\s*(\S+)' |
        Select-Object -First 1).Matches.Groups[1].Value
} else { '' }
if ($projectVersionLine -ne $EditorVersion) {
    $blockers.Add("ProjectSettings/ProjectVersion.txt = '$projectVersionLine', expected '$EditorVersion'")
    Add-Step -Name '0_project_version' -Status 'FAIL' -Evidence "ProjectVersion='$projectVersionLine'"
    throw "Project editor version '$projectVersionLine' does not match the approved '$EditorVersion'. Refusing to open the project in another Unity version."
}
Add-Step -Name '0_project_version' -Status 'PASS' -ExitCode 0 -Evidence "ProjectVersion=$projectVersionLine"

$editorRun = Invoke-Cli -Arguments @('editors', 'path', $EditorVersion, '--no-banner') -Name '0_editor_path'
$editorRoot = ($editorRun.Out.Trim() -split "`n" | Where-Object { $_.Trim() } | Select-Object -Last 1)
if ($editorRun.ExitCode -ne 0 -or -not $editorRoot) {
    Add-Step -Name '0_editor' -Status 'FAIL' -Evidence "unity editors path $EditorVersion returned no path"
    throw "Unity CLI does not know the path of Editor $EditorVersion."
}
$editorRoot = $editorRoot.Trim()
$editorExe = Join-Path $editorRoot 'Editor\Unity.exe'
if (-not (Test-Path $editorExe)) {
    Add-Step -Name '0_editor' -Status 'FAIL' -Evidence "missing file $editorExe"
    throw "Editor $EditorVersion not found: $editorExe"
}
Add-Step -Name '0_editor' -Status 'PASS' -ExitCode 0 -Evidence "path=$editorExe"

# License: the only reliable signal is the CLI JSON, not "no error line in the log".
$licenseStatus = 'UNKNOWN'
try {
    $licenseRun = Invoke-Cli -Arguments @('license', 'status', '--json', '--no-banner') -Name '0_license'
    $licenseJson = $licenseRun.Out | ConvertFrom-Json
    if ($licenseRun.ExitCode -eq 0 -and $licenseJson.success -and $licenseJson.data.active -eq $true -and $licenseJson.data.signedIn -eq $true) {
        $licenseStatus = 'ACTIVE'
        $products = ($licenseJson.data.licenses | ForEach-Object { $_.product }) -join ', '
        Add-Step -Name '0_license' -Status 'PASS' -ExitCode $licenseRun.ExitCode -Evidence "active=true signedIn=true licenses=$products"
    }
    else {
        Add-Step -Name '0_license' -Status 'FAIL' -ExitCode $licenseRun.ExitCode -Evidence 'CLI license status: active=false or signedIn=false'
        $blockers.Add('Unity license is not active: sign in to Unity Hub (Unity Personal) and re-run.')
    }
}
catch {
    Add-Step -Name '0_license' -Status 'FAIL' -Evidence "cannot read unity license status: $($_.Exception.Message)"
    $blockers.Add('Could not verify the Unity license through the Unity CLI.')
}
if ($licenseStatus -ne 'ACTIVE') {
    Write-Host 'License not confirmed - stopping so that no false PASS is reported.' -ForegroundColor Red
    exit 21
}

# ----------------------------------------------------------- 1. import ----
$import = Invoke-UnityStep -Name '1_project_import' -UnityArgs @(
    '-batchmode', '-nographics', '-quit', '-projectPath', $projectRoot
)
$engineVersion = Get-EditorVersionFromLog -Path $import.Log
if ($import.Ok -and $engineVersion -ne $EditorVersion) {
    Add-Step -Name '1_editor_version_check' -Status 'FAIL' -Evidence "log says 'Initialize engine version: $engineVersion'"
    $blockers.Add("Wrong Editor started: '$engineVersion' instead of '$EditorVersion'.")
    $import.Ok = $false
}
elseif ($import.Ok) {
    Add-Step -Name '1_editor_version_check' -Status 'PASS' -ExitCode 0 -Evidence "engine=$engineVersion"
}

if (-not $import.Ok) {
    $blockers.Add('Import/compilation failed - tests and builds were not started.')
    Write-Host 'Import/compilation failed, remaining steps skipped.' -ForegroundColor Red
}

# ------------------------------------------------------------ 2. tests ----
$editMode = $null
$playMode = $null
if ($import.Ok) {
    $editMode = Invoke-CliTest -Mode 'EditMode' -XmlPath (Join-Path $results 'editmode.xml')
    $playMode = Invoke-CliTest -Mode 'PlayMode' -XmlPath (Join-Path $results 'playmode.xml')
    if (-not $editMode.Ok) { $blockers.Add('EditMode tests not confirmed (see NUnit XML).') }
    if (-not $playMode.Ok) { $blockers.Add('PlayMode tests not confirmed (see NUnit XML).') }
}

# ------------------------------------------------------- 3. scene/build ----
$scenePath = Join-Path $projectRoot 'Assets\Scenes\Pilot.unity'
$sceneBuild = $null
$playerBuild = $null
if ($import.Ok) {
    $sceneBuild = Invoke-UnityStep -Name '3_scene_build' -UnityArgs @(
        '-batchmode', '-nographics', '-quit', '-projectPath', $projectRoot,
        '-executeMethod', 'CubeGame.EditorTools.PilotSceneBuilder.Build'
    )
    $sceneLog = if (Test-Path $sceneBuild.Log) { Get-Content $sceneBuild.Log -Raw -Encoding UTF8 } else { '' }
    $sceneFileOk = (Test-Path $scenePath) -and ((Get-Item $scenePath).Length -gt 0)
    $sceneOk = $sceneBuild.Ok -and ($sceneLog -match 'SCENE_SAVED') -and $sceneFileOk
    if ($sceneOk) {
        Add-Step -Name '3_scene_artifact' -Status 'PASS' -ExitCode 0 -Evidence ("file={0} bytes={1}" -f $scenePath, (Get-Item $scenePath).Length)
    }
    else {
        Add-Step -Name '3_scene_artifact' -Status 'FAIL' -Evidence 'no SCENE_SAVED marker or no scene file'
        $blockers.Add('Pilot.unity scene not confirmed (SCENE_SAVED marker/file).')
        Write-Host 'Scene not built - player build skipped.' -ForegroundColor Red
    }

    if ($sceneOk) {
        $playerBuild = Invoke-UnityStep -Name '4_player_build' -UnityArgs @(
            '-batchmode', '-nographics', '-quit', '-projectPath', $projectRoot,
            '-executeMethod', 'CubeGame.EditorTools.PilotBuild.BuildStandalone'
        )
        $playerExe = Join-Path $buildDir 'Pilot\CubeGamePilot.exe'
        $playerData = Join-Path $buildDir 'Pilot\CubeGamePilot_Data'
        $logText = if (Test-Path $playerBuild.Log) { Get-Content $playerBuild.Log -Raw -Encoding UTF8 } else { '' }
        $buildOk = $playerBuild.Ok -and ($logText -match 'BUILD_SUCCEEDED') -and
        (Test-Path $playerExe) -and ((Get-Item $playerExe).Length -gt 100KB) -and (Test-Path $playerData)
        if (-not $buildOk) {
            Add-Step -Name '4_player_artifact' -Status 'FAIL' -Evidence 'no BUILD_SUCCEEDED marker or missing .exe/_Data'
            $blockers.Add('Windows player build not confirmed (BUILD_SUCCEEDED/.exe).')
        }
        else {
            Add-Step -Name '4_player_artifact' -Status 'PASS' -ExitCode 0 -Evidence ("exe={0} bytes={1}" -f $playerExe, (Get-Item $playerExe).Length)
        }
    }
}

# ------------------------------------------------------ 5. player run ----
if ($playerBuild -and $playerBuild.Ok -and -not $SkipPlayerRun) {
    $playerExe = Join-Path $buildDir 'Pilot\CubeGamePilot.exe'
    if (Test-Path $playerExe) {
        $shot = Join-Path $shots 'pilot_run.png'
        $playerLog = Join-Path $logs '5_player_run.log'
        # Remove only our own artifacts: a stale screenshot would otherwise be a false PASS.
        foreach ($artifact in @($shot, $playerLog)) {
            if (Test-Path $artifact) { Remove-Item $artifact -Force }
        }

        Write-Host "`n=== 5. Player run ===" -ForegroundColor Cyan
        # Start-Process hangs for this GUI player in the agent environment, and "& player.exe"
        # returns immediately, so wait for the player's own completion marker in its log.
        $playerArgs = @('-cubeSeed', '666666', '-cubeChunks', '8', '-cubeScreenshot', $shot,
            '-cubeFrames', '120', '-logFile', $playerLog)
        & $playerExe @playerArgs

        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $finished = $false
        while ($watch.Elapsed.TotalSeconds -lt 180) {
            if (Test-Path $playerLog) {
                $text = Get-Content $playerLog -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
                if ($text -match '\[Pilot\] quitting') { $finished = $true; break }
            }
            Start-Sleep -Milliseconds 400
        }
        $watch.Stop()

        # ScreenCapture writes the PNG at the end of a frame, slightly after the "[Pilot] quitting"
        # marker, so wait for a stable screenshot file instead of checking it immediately.
        $shotDeadline = (Get-Date).AddSeconds(20)
        $lastSize = -1
        while ((Get-Date) -lt $shotDeadline) {
            if (Test-Path $shot) {
                $size = (Get-Item $shot).Length
                if ($size -gt 1KB -and $size -eq $lastSize) { break }
                $lastSize = $size
            }
            Start-Sleep -Milliseconds 300
        }

        $logText = if (Test-Path $playerLog) { Get-Content $playerLog -Raw -Encoding UTF8 } else { '' }
        $shotOk = (Test-Path $shot) -and ((Get-Item $shot).Length -gt 1KB)
        $logOk = ($logText -match '\[Pilot\] screenshot requested') -and
        ($logText -match '\[Pilot\] quitting') -and ($logText -notmatch 'Exception')
        $ok = $finished -and $shotOk -and $logOk
        $evidence = "finished={0} shot={1} bytes={2}" -f $finished, $shotOk,
        $(if (Test-Path $shot) { (Get-Item $shot).Length } else { 0 })
        if ($logText -match '\[Pilot\] seed=([^\r\n]+)') { $evidence = $evidence + ' | ' + $Matches[1].Trim() }
        Add-Step -Name '5_player_run' -Status $(if ($ok) { 'PASS' } else { 'FAIL' }) `
            -Seconds $watch.Elapsed.TotalSeconds -Evidence $evidence -Log $playerLog
        if (-not $ok) { $blockers.Add('Player run/screenshot not confirmed.') }
    }
}
elseif ($SkipPlayerRun) {
    Add-Step -Name '5_player_run' -Status 'SKIPPED' -Evidence 'requested -SkipPlayerRun'
}

# --------------------------------------------------------------- summary ----
$statuses = @{}
foreach ($step in $steps) { $statuses[$step.Step] = $step.Status }
$failedSteps = @($steps | Where-Object { $_.Status -eq 'FAIL' })

function Get-Status {
    param([string]$Name)
    if ($statuses.ContainsKey($Name)) { return $statuses[$Name] }
    return 'NOT_RUN'
}

$gitBranch = (& git -C $projectRoot rev-parse --abbrev-ref HEAD 2>$null)
$gitCommit = (& git -C $projectRoot rev-parse --short HEAD 2>$null)
$installedRun = Invoke-Cli -Arguments @('editors', '--installed', '--no-banner') -Name '0_editors_installed'
$installedEditors = $installedRun.Out
$hasSixSix = $installedEditors -match '6000\.6\.7f1'

$report = [ordered]@{
    CLI_VERSION              = $cliVersion
    EDITOR_REGISTERED_6000_3 = if (Test-Path $editorExe) { 'YES' } else { 'NO' }
    EDITOR_6000_3_PATH       = $editorRoot
    EDITOR_REGISTERED_6000_6 = if ($hasSixSix) { 'YES' } else { 'NO' }
    PROJECT_EDITOR           = $projectVersionLine
    UNITY_LICENSE            = $licenseStatus
    UNITY_IMPORT             = Get-Status '1_project_import'
    UNITY_EDITMODE           = Get-Status '2_tests_editmode'
    UNITY_PLAYMODE           = Get-Status '2_tests_playmode'
    SCENE_BUILD              = Get-Status '3_scene_build'
    PLAYER_BUILD             = Get-Status '4_player_build'
    PLAYER_RUN               = Get-Status '5_player_run'
    PIPELINE                 = 'NOT_INSTALLED'
    BLOCKERS                 = ($blockers -join '; ')
    BRANCH                   = "$gitBranch"
    COMMIT                   = "$gitCommit"
    GENERATED_AT             = (Get-Date).ToString('s')
    Steps                    = $steps
}
$report | ConvertTo-Json -Depth 5 | Set-Content -Path $summaryPath -Encoding UTF8

Write-Host "`n=== SUMMARY ===" -ForegroundColor Cyan
$steps | Format-Table Step, Status, ExitCode, Seconds, Evidence -AutoSize | Out-String | Write-Host

Write-Host '--- report block ---' -ForegroundColor Cyan
foreach ($key in @('CLI_VERSION', 'EDITOR_REGISTERED_6000_3', 'EDITOR_6000_3_PATH', 'PROJECT_EDITOR',
        'UNITY_LICENSE', 'UNITY_IMPORT', 'UNITY_EDITMODE', 'UNITY_PLAYMODE', 'SCENE_BUILD',
        'PLAYER_BUILD', 'PLAYER_RUN', 'PIPELINE', 'BLOCKERS', 'BRANCH', 'COMMIT')) {
    Write-Host ("{0}={1}" -f $key, $report[$key])
}
Write-Host "`nSummary: $summaryPath"
Write-Host "Logs:    $logs"
Write-Host "Tests:   $results"
Write-Host "Shots:   $shots"

if ($failedSteps.Count -gt 0) { exit 1 }
exit 0
