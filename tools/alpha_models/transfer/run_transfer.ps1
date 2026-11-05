# run_transfer.ps1 — one command for the Alpha -> Unity animation transfer pilot (S05A-2).
# Creates/updates an ISOLATED Unity 6000.3.27f2 project under .scratch, copies the transfer code,
# prepares models + clips, then runs self-tests, the transfer verifier and PlayMode.
# Verdicts are read from the report FILES (TransferOutput/*.txt), never from the exit code alone.
param(
  [string]$Project = 'D:\Projects\CubeGame\.scratch\s05a2\unity\AlphaTransfer',
  [string]$Db      = 'D:\Projects\CubeGame\.scratch\alphatest2\data1.db',
  [string]$Clips   = 'D:\Projects\CubeGame\.scratch\s05a2\clips',
  [string]$Models  = 'D:\Projects\CubeGame\tools\alpha_models\transfer\models.s05a1_greatsword.json',
  [string]$Unity   = 'D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe',
  [switch]$SkipPlayMode
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$src  = Join-Path $PSScriptRoot 'unity'
if (-not (Test-Path $Unity)) { throw "Unity 6000.3.27f2 not found at $Unity (do not use 6.6)" }

# 1. isolated project skeleton (idempotent)
New-Item -ItemType Directory -Force -Path "$Project\Assets\AlphaTransfer\Scripts\Runtime", "$Project\Assets\AlphaTransfer\Scripts\Editor", "$Project\Packages", "$Project\ProjectSettings" | Out-Null
if (-not (Test-Path "$Project\Packages\manifest.json")) {
  '{ "dependencies": { "com.unity.modules.imageconversion": "1.0.0", "com.unity.modules.jsonserialize": "1.0.0" } }' | Set-Content -Encoding ascii "$Project\Packages\manifest.json"
}
if (-not (Test-Path "$Project\ProjectSettings\ProjectVersion.txt")) {
  "m_EditorVersion: 6000.3.27f2" | Set-Content -Encoding ascii "$Project\ProjectSettings\ProjectVersion.txt"
}
Copy-Item -Force "$src\Runtime\*" "$Project\Assets\AlphaTransfer\Scripts\Runtime\"
Copy-Item -Force "$src\Editor\*"  "$Project\Assets\AlphaTransfer\Scripts\Editor\"

# 2. data (local only: .scratch is git-ignored)
Push-Location $repo
try {
  python -m tools.alpha_models.transfer.prepare_unity --db $Db --project $Project --clips $Clips --models $Models
  if ($LASTEXITCODE -ne 0) { throw "PREPARE_RESULT=FAIL" }
} finally { Pop-Location }

$out = Join-Path $Project 'TransferOutput'
New-Item -ItemType Directory -Force -Path $out | Out-Null
function Invoke-Unity([string]$method, [string]$log, [switch]$NoQuit) {
  $ua = @('-batchmode', '-nographics', '-projectPath', $Project, '-executeMethod', $method, '-logFile', $log)
  if (-not $NoQuit) { $ua = @('-quit') + $ua }
  # PlayMode and screenshots need a graphics device: drop -nographics for them
  if ($method -like '*PlayMode*' -or $method -like '*AlphaTransferEditor.Run') { $ua = $ua | Where-Object { $_ -ne '-nographics' } }
  $p = Start-Process -FilePath $Unity -ArgumentList $ua -Wait -PassThru -NoNewWindow
  return $p.ExitCode
}
function Marker([string]$file, [string]$name) {
  if (-not (Test-Path $file)) { return 'MISSING' }
  $line = Select-String -Path $file -Pattern "^$name=(\w+)" | Select-Object -First 1
  if ($line) { return $line.Matches[0].Groups[1].Value } else { return 'MISSING' }
}

$e1 = Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaTransferSelfTests.Run' "$out\selftest.log"
$self = Marker "$out\selftest.txt" 'SELFTEST_RESULT'
$e2 = Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaTransferEditor.Run' "$out\transfer.log"
$tr = Marker "$out\transfer_report.txt" 'TRANSFER_RESULT'
$pm = 'NOT_RUN'; $e3 = ''
if (-not $SkipPlayMode) {
  if (Test-Path "$out\playmode.txt") { Remove-Item "$out\playmode.txt" }
  $e3 = Invoke-Unity 'CubeGame.AlphaTransfer.Editor.AlphaTransferPlayMode.Run' "$out\playmode.log" -NoQuit
  $pm = Marker "$out\playmode.txt" 'PLAYMODE_RESULT'
}
"SELFTEST_RESULT=$self exit=$e1"
"TRANSFER_RESULT=$tr exit=$e2"
"PLAYMODE_RESULT=$pm exit=$e3"
"REPORTS=$out"
if ($self -ne 'PASS' -or $tr -ne 'PASS' -or ($pm -ne 'PASS' -and -not $SkipPlayMode)) { exit 1 }
