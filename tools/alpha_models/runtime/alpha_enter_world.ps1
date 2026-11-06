# alpha_enter_world.ps1 - start the LOCAL research copy and walk the menus into the world.
# Verified 2026-11-05 on the 784x561 client: START GAME -> character card -> SELECT -> world SELECT.
# Every step saves a screenshot to -ShotDir so the state can be checked (no blind clicking).
param([string]$ShotDir = 'D:\Projects\CubeGame\.scratch\s05a2\nav')
. "$PSScriptRoot\alpha_window.ps1"
$alphaPid = Start-Alpha
Start-Sleep -Seconds 8
[void](Focus-Alpha)
Save-AlphaShot (Join-Path $ShotDir 'enter_0_menu.png') | Out-Null
$c = Get-AlphaClient
# Client-relative positions scaled from the verified 784x561 layout.
function P([double]$x, [double]$y) { @([int]($x / 784.0 * $c.W), [int]($y / 561.0 * $c.H)) }
$p = P 390 354; Click-Alpha $p[0] $p[1]; Start-Sleep -Seconds 3      # START GAME
Save-AlphaShot (Join-Path $ShotDir 'enter_1_chars.png') | Out-Null
$p = P 390 170; Click-Alpha $p[0] $p[1]; Start-Sleep -Seconds 2      # character card
$p = P 571 220; Click-Alpha $p[0] $p[1]; Start-Sleep -Seconds 6      # SELECT (character)
Save-AlphaShot (Join-Path $ShotDir 'enter_2_worlds.png') | Out-Null
$p = P 571 220; Click-Alpha $p[0] $p[1]; Start-Sleep -Seconds 20     # SELECT (world)
Save-AlphaShot (Join-Path $ShotDir 'enter_3_world.png') | Out-Null
"ALPHA_PID=$alphaPid client=$($c.W)x$($c.H)"
