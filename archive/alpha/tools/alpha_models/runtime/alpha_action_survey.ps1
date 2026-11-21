# alpha_action_survey.ps1 - one research session that drives MANY player actions in the LOCAL
# copy while pose_capture.py --full records every drawModel input (creature struct, this, dt).
# Purpose: reference frames for the offline drawModel emulator (drawmodel_emu.py) across states,
# not clips. Sequence (each step logged to <Out>.events.json):
#   idle -> walk (W) -> 3x left click (combo) -> right click -> space (jump) -> shift -> walk+jump
#   -> keys 1..4 (skills) -> idle.  Unknown/unused keys simply do nothing in Alpha.
param(
  [string]$Out = 'D:\Projects\CubeGame\.scratch\animsys\cap\survey_actions.jsonl',
  [double]$Seconds = 40
)
. "$PSScriptRoot\alpha_window.ps1"
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
New-Item -ItemType Directory -Force -Path (Split-Path $Out -Parent) | Out-Null
$alphaPid = (Get-Process Cube -ErrorAction Stop | Select-Object -First 1).Id
$log = [IO.Path]::ChangeExtension($Out, '.log')
$proc = Start-Process -FilePath python -ArgumentList @('-m','tools.alpha_models.runtime.pose_capture',"$alphaPid",$Out,'--full','--records','100000','--seconds',"$Seconds") -WorkingDirectory $repo -RedirectStandardOutput $log -PassThru -WindowStyle Hidden
$sw = [Diagnostics.Stopwatch]::StartNew()
$events = New-Object System.Collections.ArrayList
function Ev([string]$n) { [void]$events.Add([pscustomobject]@{ event = $n; t_ms = [math]::Round($sw.Elapsed.TotalMilliseconds, 1) }) }
function Hold([int]$vk, [int]$ms, [string]$name) { Ev "$name-down"; [void][AlphaWin]::Key($vk, $false); Start-Sleep -Milliseconds $ms; [void][AlphaWin]::Key($vk, $true); Ev "$name-up" }
Start-Sleep -Milliseconds 1500
if (-not (Focus-Alpha)) { Ev 'focus-failed' } else { Ev 'focus-ok' }
Start-Sleep -Milliseconds 1500
Hold 0x57 2500 'w'
Start-Sleep -Milliseconds 1500
for ($i = 0; $i -lt 3; $i++) { Ev 'lmb'; Mouse-Alpha left 60; Start-Sleep -Milliseconds 350 }
Start-Sleep -Milliseconds 2000
Ev 'rmb'; Mouse-Alpha right 400
Start-Sleep -Milliseconds 2500
Hold 0x20 120 'space'
Start-Sleep -Milliseconds 2000
Hold 0x10 120 'shift'
Start-Sleep -Milliseconds 2000
Ev 'w+space'; [void][AlphaWin]::Key(0x57, $false); Start-Sleep -Milliseconds 600; Hold 0x20 120 'space'; Start-Sleep -Milliseconds 900; [void][AlphaWin]::Key(0x57, $true)
Start-Sleep -Milliseconds 1500
foreach ($k in 0x31, 0x32, 0x33, 0x34) { Hold $k 100 ("key" + ($k - 0x30)); Start-Sleep -Milliseconds 2000 }
$proc.WaitForExit()
Ev 'capture-exit'
Start-Sleep -Milliseconds 1500
$alive = [bool](Get-Process -Id $alphaPid -ErrorAction SilentlyContinue)
$events | ConvertTo-Json | Set-Content -Encoding utf8 ([IO.Path]::ChangeExtension($Out, '.events.json'))
Get-Content $log
"CUBE_ALIVE=$alive pid=$alphaPid"
