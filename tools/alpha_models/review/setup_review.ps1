# setup_review.ps1 - builds the S05B visual review project (isolated Unity 6000.3.27f2 project in .scratch).
#   1. project skeleton + copy of the transfer runtime (tools/alpha_models/transfer/unity/Runtime) and the
#      review code (tools/alpha_models/review/unity)
#   2. review data: build_review.py -> Assets/StreamingAssets/Review (existing pipeline, existing batch jobs)
#   3. Unity batch: Build (scene) -> Check (EditMode checks + contact sheets) -> RunPlayMode (smoke tour)
# Verdict of each Unity step = marker file AND exit code 0 (same rule as run_transfer.ps1).
# Alpha-derived data stays in .scratch (git-ignored); nothing is published.
param(
  [string]$Project = 'D:\Projects\CubeGame\.scratch\review\unity\S05B_VisualReview',
  [string]$Unity   = 'D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe',
  [string]$Python  = 'python',
  [switch]$SkipData,
  [switch]$SkipPlayMode
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$Project = [System.IO.Path]::GetFullPath($Project)
$scratchRoot = [System.IO.Path]::GetFullPath((Join-Path $repo '.scratch')) + [System.IO.Path]::DirectorySeparatorChar
if (-not $Project.StartsWith($scratchRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Review project must stay inside repository .scratch' }
if (-not (Test-Path -LiteralPath $Unity)) { throw "Unity 6000.3.27f2 not found at $Unity (do not use 6.6)" }
$lockPath = Join-Path $Project 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
  try { $l = [System.IO.File]::Open($lockPath, 'Open', 'ReadWrite', 'None'); $l.Dispose() }
  catch { throw 'The review project is open in Unity; close it before running setup_review.ps1' }
  Remove-Item -LiteralPath $lockPath
}

# 1. skeleton + code
$scripts = "$Project\Assets\Review\Scripts"
New-Item -ItemType Directory -Force -Path "$scripts\Transfer", "$scripts\Runtime", "$scripts\Editor", "$Project\Packages", "$Project\ProjectSettings" | Out-Null
'{ "dependencies": { "com.unity.modules.imageconversion": "1.0.0", "com.unity.modules.jsonserialize": "1.0.0", "com.unity.modules.imgui": "1.0.0" } }' |
  Set-Content -Encoding ascii "$Project\Packages\manifest.json"
if (-not (Test-Path -LiteralPath "$Project\ProjectSettings\ProjectVersion.txt")) {
  "m_EditorVersion: 6000.3.27f2" | Set-Content -Encoding ascii "$Project\ProjectSettings\ProjectVersion.txt"
}
# Unity Search indexes the project on editor startup; in this fresh isolated project its default-database
# lookup throws ArgumentOutOfRangeException (SearchDatabase.EnumerateAll <- SearchInit.IndexationOnStartup).
# The review never uses Search, so startup indexing is switched off for THIS project only.
New-Item -ItemType Directory -Force -Path "$Project\UserSettings" | Out-Null
$searchSettings = "$Project\UserSettings\Search.settings"
$ss = @{}
if (Test-Path -LiteralPath $searchSettings) {
  try { $o = Get-Content -Raw -LiteralPath $searchSettings | ConvertFrom-Json; if ($o) { foreach ($p in $o.PSObject.Properties) { $ss[$p.Name] = $p.Value } } } catch { $ss = @{} }
}
$ss['indexOnEditorStartup'] = $false
$ss['onBoardingDoNotAskAgain'] = $true
($ss | ConvertTo-Json -Depth 8) | Set-Content -Encoding ascii -LiteralPath $searchSettings
Copy-Item -Force "$repo\tools\alpha_models\transfer\unity\Runtime\*" "$scripts\Transfer\"
Copy-Item -Force "$PSScriptRoot\unity\Runtime\*" "$scripts\Runtime\"
Copy-Item -Force "$PSScriptRoot\unity\Editor\*" "$scripts\Editor\"

# 2. data
if (-not $SkipData) {
  Push-Location $repo
  try {
    & $Python -m tools.alpha_models.review.build_review --project $Project
    if ($LASTEXITCODE -ne 0) { throw "REVIEW_DATA_RESULT=FAIL (python exit $LASTEXITCODE)" }
  } finally { Pop-Location }
}

# 3. Unity steps
$out = Join-Path $Project 'ReviewOutput'
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

function Invoke-Unity([string]$method, [string]$log, [switch]$NoQuit, [switch]$Graphics) {
  $ua = @('-batchmode', '-projectPath', $Project, '-executeMethod', $method, '-logFile', $log)
  if (-not $NoQuit) { $ua = @('-quit') + $ua }
  if (-not $Graphics) { $ua += '-nographics' }
  $p = Start-Process -FilePath $Unity -ArgumentList $ua -Wait -PassThru -WindowStyle Hidden
  return [int]$p.ExitCode
}
function Verdict([string]$file, [string]$marker, [int]$exit) {
  $line = if (Test-Path -LiteralPath $file) { Select-String -Path $file -Pattern "^$marker=(\w+)" | Select-Object -First 1 } else { $null }
  $v = if ($line) { $line.Matches[0].Groups[1].Value } else { 'MISSING' }
  $ok = ($v -eq 'PASS') -and ($exit -eq 0)
  Write-Host ("{0}={1} exit={2}" -f $marker, $(if ($ok) { 'PASS' } else { "FAIL($v)" }), $exit)
  return [bool]$ok
}

# Windows PowerShell 5.1 parse/encoding check of every tools\alpha_models script
$ps51 = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
& $ps51 -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\check_ps51.ps1" | Set-Content -Encoding ascii "$out\ps51_check.txt"
$ok51 = Verdict "$out\ps51_check.txt" 'PS51_CHECK_RESULT' $LASTEXITCODE

$ns = 'CubeGame.AlphaTransfer.Review.Editor.ReviewSceneBuilder'
$e1 = Invoke-Unity "$ns.Build" "$out\build.log"
$ok = (Verdict "$out\build.txt" 'REVIEW_BUILD_RESULT' $e1) -and $ok51
if ($ok) {
  $e2 = Invoke-Unity "$ns.Check" "$out\check.log" -Graphics
  $ok = (Verdict "$out\review_check.txt" 'REVIEW_CHECK_RESULT' $e2) -and $ok
}
if ($ok -and -not $SkipPlayMode) {
  $e3 = Invoke-Unity "$ns.RunPlayMode" "$out\playmode.log" -NoQuit -Graphics
  $ok = (Verdict "$out\review_playmode.txt" 'REVIEW_PLAYMODE_RESULT' $e3) -and $ok
}
Write-Output "PROJECT=$Project"
Write-Output "SCENE=Assets/Review/S05B_VisualReview.unity"
Write-Output "REPORTS=$out"
if ($ok) { Write-Output 'REVIEW_SETUP_RESULT=PASS'; exit 0 }
Write-Output 'REVIEW_SETUP_RESULT=FAIL'; exit 1
