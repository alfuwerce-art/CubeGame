# run_transfer.ps1 — one command for the Alpha -> Unity animation transfer pilot (S05A-2).
# Creates/updates an ISOLATED Unity 6000.3.27f2 project under .scratch, copies the transfer code,
# prepares models + clips, then runs self-tests, the transfer verifier and PlayMode.
#
# Verdicts are decided by BOTH the report files (TransferOutput/*.txt) AND the Unity exit codes:
#   a PASS requires the marker == PASS AND the Unity exit code == 0.
# Stale TransferOutput is isolated (moved to TransferOutput_history/<timestamp>) before every run,
# so a leftover PASS from an earlier run can never mask a failure of the current run.
#
# Negative self-tests of the verdict/isolation logic (no Unity launch):  -Test
param(
  [string]$Project = 'D:\Projects\CubeGame\.scratch\s05a2\unity\AlphaTransfer',
  [string]$Db      = 'D:\Projects\CubeGame\.scratch\alphatest2\data1.db',
  [string]$Clips   = 'D:\Projects\CubeGame\.scratch\s05a2\clips',
  [string]$Models  = 'D:\Projects\CubeGame\tools\alpha_models\transfer\models.s05a1_greatsword.json',
  [string]$Unity   = 'D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe',
  # Universal pipeline: a job file (kind, seed variant, equipment) replaces -Models/-Clips; the
  # rig, models and motion-family clips are then prepared by tools.alpha_models.universal.pipeline.
  [string]$UniversalJob = '',
  [string]$Python = 'python',
  [ValidateSet(30,60,120)][int]$ControllerFps = 60,
  [switch]$CaptureController,
  [switch]$SkipPlayMode,
  [switch]$Test
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$src  = Join-Path $PSScriptRoot 'unity'

# --------------------------------------------------------------------- verdict logic
# Single source of truth for PASS/FAIL: a step passes only when the marker file exists,
# contains "<name>=PASS", and the Unity exit code is exactly 0.
function Resolve-Verdict {
  param([string]$MarkerFile, [string]$MarkerName, [int]$ExitCode)
  $problems = [System.Collections.Generic.List[string]]::new()
  if ($ExitCode -ne 0) { $problems.Add("UNITY_EXIT_CODE=$ExitCode (expected 0)") }
  if (-not (Test-Path -LiteralPath $MarkerFile)) { $problems.Add("MARKER_FILE_MISSING $MarkerFile") }
  else {
    $line = Select-String -Path $MarkerFile -Pattern "^$MarkerName=(\w+)" | Select-Object -First 1
    if (-not $line) { $problems.Add("MARKER_NOT_FOUND $MarkerName in $MarkerFile") }
    else {
      $value = $line.Matches[0].Groups[1].Value
      if ($value -ne 'PASS') { $problems.Add("MARKER_VALUE=$value") }
    }
  }
  if ($problems.Count -eq 0) { return [pscustomobject]@{ Verdict = 'PASS'; Problems = @() } }
  return [pscustomobject]@{ Verdict = 'FAIL'; Problems = $problems }
}

# --------------------------------------------------------------------- stale output
function Isolate-StaleOutput {
  param([string]$OutDir)
  if (Test-Path -LiteralPath $OutDir) {
    $history = Join-Path (Split-Path $OutDir -Parent) 'TransferOutput_history'
    New-Item -ItemType Directory -Force -Path $history | Out-Null
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
    $dest = Join-Path $history $stamp
    if (Test-Path -LiteralPath $dest) { $dest = Join-Path $history ($stamp + '_' + [guid]::NewGuid().ToString('N').Substring(0, 6)) }
    Move-Item -LiteralPath $OutDir -Destination $dest
    Write-Output "ISOLATED_PREVIOUS_OUTPUT=$dest"
  }
}

# --------------------------------------------------------------------- negative tests
function Invoke-NegativeTests {
  $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ('run_transfer_selftest_' + [guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Force -Path $tmp | Out-Null
  $results = [System.Collections.Generic.List[pscustomobject]]::new()
  function Assert([string]$Name, [bool]$Cond) {
    $results.Add([pscustomobject]@{ Name = $Name; Pass = $Cond })
    Write-Output ("  {0}  {1}" -f ($(if ($Cond) { 'PASS' } else { 'FAIL' })), $Name)
  }
  try {
    $fPass = Join-Path $tmp 'pass.txt'
    Set-Content -LiteralPath $fPass -Value 'SELFTEST_RESULT=PASS cases=19 failed=0'
    Assert 'pass_marker_with_exit0_is_pass' ((Resolve-Verdict $fPass 'SELFTEST_RESULT' 0).Verdict -eq 'PASS')
    Assert 'pass_marker_with_exit1_is_fail' ((Resolve-Verdict $fPass 'SELFTEST_RESULT' 1).Verdict -eq 'FAIL')
    Assert 'pass_marker_with_exit2_is_fail' ((Resolve-Verdict $fPass 'SELFTEST_RESULT' 2).Verdict -eq 'FAIL')
    Assert 'pass_marker_with_exit3_is_fail' ((Resolve-Verdict $fPass 'SELFTEST_RESULT' 3).Verdict -eq 'FAIL')
    Assert 'pass_marker_with_crash_exit_is_fail' ((Resolve-Verdict $fPass 'SELFTEST_RESULT' -1073741819).Verdict -eq 'FAIL')
    Assert 'missing_marker_file_is_fail' ((Resolve-Verdict (Join-Path $tmp 'missing.txt') 'SELFTEST_RESULT' 0).Verdict -eq 'FAIL')
    $fFail = Join-Path $tmp 'fail.txt'
    Set-Content -LiteralPath $fFail -Value 'SELFTEST_RESULT=FAIL cases=19 failed=1'
    Assert 'fail_marker_is_fail' ((Resolve-Verdict $fFail 'SELFTEST_RESULT' 0).Verdict -eq 'FAIL')
    $fGarbage = Join-Path $tmp 'garbage.txt'
    Set-Content -LiteralPath $fGarbage -Value 'no marker here'
    Assert 'missing_marker_line_is_fail' ((Resolve-Verdict $fGarbage 'SELFTEST_RESULT' 0).Verdict -eq 'FAIL')

    # isolation: a stale PASS must be moved out of the output dir before a run
    $staleOut = Join-Path $tmp 'TransferOutput'
    New-Item -ItemType Directory -Force -Path $staleOut | Out-Null
    Set-Content -LiteralPath (Join-Path $staleOut 'selftest.txt') -Value 'SELFTEST_RESULT=PASS'
    Isolate-StaleOutput $staleOut
    Assert 'isolation_removes_stale_output' (-not (Test-Path -LiteralPath (Join-Path $staleOut 'selftest.txt')))
    Assert 'isolation_keeps_history_copy' ((Get-ChildItem -Path (Join-Path $tmp 'TransferOutput_history') -Recurse -Filter 'selftest.txt').Count -eq 1)
  }
  finally {
    $resolvedTmp = [System.IO.Path]::GetFullPath($tmp)
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if (-not $resolvedTmp.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $resolvedTmp -Leaf) -notlike 'run_transfer_selftest_*') { throw 'Unsafe self-test cleanup path' }
    Remove-Item -LiteralPath $resolvedTmp -Recurse -Force -ErrorAction SilentlyContinue
  }
  $failed = ($results | Where-Object { -not $_.Pass }).Count
  Write-Output ("NEGATIVE_TESTS_RESULT={0} cases={1} failed={2}" -f ($(if ($failed -eq 0) { 'PASS' } else { 'FAIL' })), $results.Count, $failed)
  if ($failed -gt 0) { exit 1 }
  exit 0
}

# --------------------------------------------------------------------- main
if ($Test) { Invoke-NegativeTests }

$Project = [System.IO.Path]::GetFullPath($Project)
$scratchRoot = [System.IO.Path]::GetFullPath((Join-Path $repo '.scratch')) + [System.IO.Path]::DirectorySeparatorChar
if (-not $Project.StartsWith($scratchRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Transfer project must stay inside repository .scratch' }
$lockPath = Join-Path $Project 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
  try { $staleLock = [System.IO.File]::Open($lockPath, 'Open', 'ReadWrite', 'None'); $staleLock.Dispose() }
  catch { throw 'Unity project is already open; close it before running this project' }
  Remove-Item -LiteralPath $lockPath # only an unlocked stale file, inside the verified project
}

if (-not (Test-Path -LiteralPath $Unity)) { throw "Unity 6000.3.27f2 not found at $Unity (do not use 6.6)" }

# 1. isolated project skeleton (idempotent)
New-Item -ItemType Directory -Force -Path "$Project\Assets\AlphaTransfer\Scripts\Runtime", "$Project\Assets\AlphaTransfer\Scripts\Editor", "$Project\Packages", "$Project\ProjectSettings" | Out-Null
if (-not (Test-Path -LiteralPath "$Project\Packages\manifest.json")) {
  '{ "dependencies": { "com.unity.modules.imageconversion": "1.0.0", "com.unity.modules.jsonserialize": "1.0.0" } }' | Set-Content -Encoding ascii "$Project\Packages\manifest.json"
}
if (-not (Test-Path -LiteralPath "$Project\ProjectSettings\ProjectVersion.txt")) {
  "m_EditorVersion: 6000.3.27f2" | Set-Content -Encoding ascii "$Project\ProjectSettings\ProjectVersion.txt"
}
Copy-Item -Force "$src\Runtime\*" "$Project\Assets\AlphaTransfer\Scripts\Runtime\"
Copy-Item -Force "$src\Editor\*"  "$Project\Assets\AlphaTransfer\Scripts\Editor\"

# 2. data (local only: .scratch is git-ignored)
Push-Location $repo
try {
  if ($UniversalJob) {
    & $Python -m tools.alpha_models.universal.pipeline prepare-unity --job $UniversalJob --project $Project --db $Db
  } else {
    & $Python -m tools.alpha_models.transfer.prepare_unity --db $Db --project $Project --clips $Clips --models $Models
  }
  if ($LASTEXITCODE -ne 0) { throw "PREPARE_RESULT=FAIL (python exit $LASTEXITCODE)" }
} finally { Pop-Location }

# 3. isolate stale results, then run Unity steps with exit-code + marker checks
$out = Join-Path $Project 'TransferOutput'
Isolate-StaleOutput $out
New-Item -ItemType Directory -Force -Path $out | Out-Null

function Invoke-Unity([string]$method, [string]$log, [switch]$NoQuit) {
  $ua = @('-batchmode', '-nographics', '-projectPath', $Project, '-executeMethod', $method, '-logFile', $log)
  if (-not $NoQuit) { $ua = @('-quit') + $ua }
  # PlayMode and screenshots need a graphics device: drop -nographics for them
  if ($method -like '*PlayMode*' -or $method -like '*AlphaTransferEditor.Run') { $ua = $ua | Where-Object { $_ -ne '-nographics' } }
  if ($method -like '*AlphaControllerPlayMode*') { $ua += @('-controllerFps', $ControllerFps) }
  if ($method -like '*AlphaControllerPlayMode*' -and $CaptureController) { $ua += '-controllerCapture' }
  $p = Start-Process -FilePath $Unity -ArgumentList $ua -Wait -PassThru -WindowStyle Hidden
  return [int]$p.ExitCode
}

function Show-Verdict([string]$label, $verdict, [object]$exit) {
  Write-Output ("{0}={1} exit={2}" -f $label, $verdict.Verdict, $exit)
  foreach ($p in $verdict.Problems) { Write-Output ("  - {0}" -f $p) }
}

$e1 = Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaTransferSelfTests.Run' "$out\selftest.log"
$v1 = Resolve-Verdict "$out\selftest.txt" 'SELFTEST_RESULT' $e1
if ($v1.Verdict -ne 'PASS') { Show-Verdict 'SELFTEST_RESULT' $v1 $e1; Write-Output 'RESULT=FAIL'; exit 1 }
$e2 = Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaTransferEditor.Run' "$out\transfer.log"
$v2 = Resolve-Verdict "$out\transfer_report.txt" 'TRANSFER_RESULT' $e2
if ($v2.Verdict -ne 'PASS') { Show-Verdict 'TRANSFER_RESULT' $v2 $e2; Write-Output 'RESULT=FAIL'; exit 1 }
$v3 = [pscustomobject]@{ Verdict = 'NOT_RUN'; Problems = @() }; $e3 = 'NOT_RUN'
if (-not $SkipPlayMode) {
  $e3 = Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaTransferPlayMode.Run' "$out\playmode.log" -NoQuit
  $v3 = Resolve-Verdict "$out\playmode.txt" 'PLAYMODE_RESULT' $e3
}

# parametric walk (job "locomotion": "formula" -> Data/locomotion.json): formula checks + driver PlayMode
$loco = Test-Path -LiteralPath "$Project\Assets\AlphaTransfer\Data\locomotion.json"
$v4 = [pscustomobject]@{ Verdict = 'NOT_RUN'; Problems = @() }; $e4 = 'NOT_RUN'
$v5 = [pscustomobject]@{ Verdict = 'NOT_RUN'; Problems = @() }; $e5 = 'NOT_RUN'
if ($loco) {
  $e4 = Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaParamWalkCheck.Run' "$out\paramwalk.log"
  $v4 = Resolve-Verdict "$out\paramwalk.txt" 'PARAMWALK_RESULT' $e4
  if (-not $SkipPlayMode) {
    $e5 = Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaParamWalkPlayMode.Run' "$out\locomotion_playmode.log" -NoQuit
    $v5 = Resolve-Verdict "$out\locomotion_playmode.txt" 'LOCOMOTION_PLAYMODE_RESULT' $e5
  }
}

$controller = Test-Path -LiteralPath "$Project\Assets\AlphaTransfer\Data\actions.json"
$v6 = [pscustomobject]@{ Verdict = 'NOT_RUN'; Problems = @() }; $e6 = 'NOT_RUN'
if ($controller -and -not $SkipPlayMode) {
  $e6 = Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaControllerPlayMode.Run' "$out\controller_playmode.log" -NoQuit
  $v6 = Resolve-Verdict "$out\controller_playmode.txt" 'CONTROLLER_PLAYMODE_RESULT' $e6
}

$native=Test-Path -LiteralPath "$Project\Assets\AlphaTransfer\Data\lifecycle.json"
$v7=[pscustomobject]@{Verdict='NOT_RUN';Problems=@()};$e7='NOT_RUN'
$v8=[pscustomobject]@{Verdict='NOT_RUN';Problems=@()};$e8='NOT_RUN'
if($native){
  $e7=Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaLifecycleChecks.Run' "$out\native_lifecycle.log"
  $v7=Resolve-Verdict "$out\native_lifecycle.txt" 'NATIVE_LIFECYCLE_RESULT' $e7
  if(-not $SkipPlayMode){
    $e8=Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaControllerPlayMode.RunNative' "$out\native_lifecycle_playmode.log" -NoQuit
    $v8=Resolve-Verdict "$out\native_lifecycle_playmode.txt" 'NATIVE_LIFECYCLE_PLAYMODE_RESULT' $e8
  }
}

Show-Verdict 'SELFTEST_RESULT' $v1 $e1
Show-Verdict 'TRANSFER_RESULT' $v2 $e2
Show-Verdict 'PLAYMODE_RESULT' $v3 $e3
if ($loco) {
  Show-Verdict 'PARAMWALK_RESULT' $v4 $e4
  Show-Verdict 'LOCOMOTION_PLAYMODE_RESULT' $v5 $e5
}
if ($controller) { Show-Verdict 'CONTROLLER_PLAYMODE_RESULT' $v6 $e6 }
if($native){Show-Verdict 'NATIVE_LIFECYCLE_RESULT' $v7 $e7;Show-Verdict 'NATIVE_LIFECYCLE_PLAYMODE_RESULT' $v8 $e8}
Write-Output "REPORTS=$out"

$allPass = ($v1.Verdict -eq 'PASS') -and ($v2.Verdict -eq 'PASS') -and ($SkipPlayMode -or ($v3.Verdict -eq 'PASS'))
if ($loco) { $allPass = $allPass -and ($v4.Verdict -eq 'PASS') -and ($SkipPlayMode -or ($v5.Verdict -eq 'PASS')) }
if ($controller) { $allPass = $allPass -and $loco -and ($SkipPlayMode -or ($v6.Verdict -eq 'PASS')) }
if($native){$allPass=$allPass -and $controller -and ($v7.Verdict -eq 'PASS') -and ($SkipPlayMode -or ($v8.Verdict -eq 'PASS'))}
if (-not $allPass) { Write-Output "RESULT=FAIL"; exit 1 }
Write-Output "RESULT=PASS"
exit 0
