# alpha_capture_session.ps1 — drive one idle / walk / attack sequence in the LOCAL research copy
# while pose_capture.py records the real part matrices.
#   1. starts pose_capture.py in the background (it attaches as a debugger, read-only + INT3)
#   2. idle 2 s -> hold W (-WalkMs) -> idle 2.5 s -> -Clicks left clicks every -ClickGapMs -> wait
#   3. waits for the capture to finish and checks that the game is still alive
# Input timestamps are written to <Out>.events.json (wall clock, ms since session start).
param(
  [string]$Out = 'D:\Projects\CubeGame\.scratch\s05a2\cap\session.jsonl',
  [double]$Seconds = 16,
  [int]$WalkMs = 4000,
  [int]$Clicks = 1,
  [int]$ClickGapMs = 300
)
. "$PSScriptRoot\alpha_window.ps1"
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$alphaPid = (Get-Process Cube -ErrorAction Stop | Select-Object -First 1).Id
$log = [IO.Path]::ChangeExtension($Out, '.log')
$proc = Start-Process -FilePath python -ArgumentList @('-m','tools.alpha_models.runtime.pose_capture',"$alphaPid",$Out,'--records','100000','--seconds',"$Seconds") -WorkingDirectory $repo -RedirectStandardOutput $log -PassThru -WindowStyle Hidden
$sw = [Diagnostics.Stopwatch]::StartNew()
$events = New-Object System.Collections.ArrayList
function Ev([string]$n) { [void]$events.Add([pscustomobject]@{ event = $n; t_ms = [math]::Round($sw.Elapsed.TotalMilliseconds, 1) }) }
Start-Sleep -Milliseconds 1200
if (-not (Focus-Alpha)) { Ev 'focus-failed' } else { Ev 'focus-ok' }
Start-Sleep -Milliseconds 2000
Ev 'w-down'; [void][AlphaWin]::Key(0x57, $false)
Start-Sleep -Milliseconds $WalkMs
[void][AlphaWin]::Key(0x57, $true); Ev 'w-up'
Start-Sleep -Milliseconds 2500
for ($i = 0; $i -lt $Clicks; $i++) { Ev 'lmb-down'; Mouse-Alpha left 60; Ev 'lmb-up'; Start-Sleep -Milliseconds $ClickGapMs }
$proc.WaitForExit()
Ev 'capture-exit'
Start-Sleep -Milliseconds 1500
$alive = [bool](Get-Process -Id $alphaPid -ErrorAction SilentlyContinue)
$events | ConvertTo-Json | Set-Content -Encoding utf8 ([IO.Path]::ChangeExtension($Out, '.events.json'))
Get-Content $log
"CUBE_ALIVE=$alive pid=$alphaPid"
