# entity_contact_sheet.ps1 — local contact sheet of REAL Unity PlayMode frames written by
# AlphaEntityProbe (TransferOutput/entity_frames + entity_frames.tsv) for several pilot projects.
# One row per project: frames sampled evenly per scenario stage (walk/stop/idle/action/after),
# labelled with kind, category, controller mode, stage and phase. Built-in System.Drawing only.
# Output stays local (.scratch): Alpha-derived images are never published.
#   powershell -NoProfile -STA -File tools/alpha_models/transfer/entity_contact_sheet.ps1 `
#     -Projects D:/Projects/CubeGame/.scratch/universal/unity/Pilot_0x20,... -Out D:/Projects/CubeGame/.scratch/universal/visuals
param(
  [Parameter(Mandatory = $true)][string[]]$Projects,
  [string]$Out = 'D:\Projects\CubeGame\.scratch\universal\visuals',
  [int]$PerRow = 8,
  [int]$Cell = 180
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$scratch = [System.IO.Path]::GetFullPath((Join-Path $repo '.scratch')) + [System.IO.Path]::DirectorySeparatorChar
$Out = [System.IO.Path]::GetFullPath($Out)
if (-not $Out.StartsWith($scratch, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Output must stay inside repository .scratch' }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Projects = $Projects | ForEach-Object { $_ -split ',' } | Where-Object { $_ }

$rows = @()
foreach ($p in $Projects) {
  $to = Join-Path $p 'TransferOutput'
  $tsv = Join-Path $to 'entity_frames.tsv'
  $rep = Join-Path $to 'entity_playmode.txt'
  if (-not (Test-Path -LiteralPath $tsv) -or -not (Test-Path -LiteralPath $rep)) { throw "missing entity frames in $to" }
  $report = Get-Content -LiteralPath $rep
  if ($report[0] -ne 'ENTITY_PLAYMODE_RESULT=PASS') { throw "entity playmode not PASS in $to" }
  $head = ($report | Where-Object { $_ -like 'kind=*' } | Select-Object -First 1)
  $mode = ($report | Where-Object { $_ -like 'controller_mode=*' } | Select-Object -First 1)
  $lines = Get-Content -LiteralPath $tsv | ForEach-Object { $c = $_ -split "`t"; [pscustomobject]@{ File = $c[0]; Stage = $c[1]; Phase = $c[2]; Action = $c[3] } }
  $pick = @()
  $stages = $lines | Select-Object -ExpandProperty Stage -Unique
  $quota = [Math]::Max(1, [Math]::Floor($PerRow / [Math]::Max(1, $stages.Count)))
  foreach ($s in $stages) {
    $g = @($lines | Where-Object { $_.Stage -eq $s })
    for ($i = 0; $i -lt [Math]::Min($quota, $g.Count); $i++) { $pick += $g[[int][Math]::Floor($i * $g.Count / [Math]::Min($quota, $g.Count))] }
  }
  # spare cells go to the longest stage (e.g. the action), evenly spaced, in timeline order
  $spare = $PerRow - $pick.Count
  if ($spare -gt 0) {
    $longest = $lines | Group-Object Stage | Sort-Object Count -Descending | Select-Object -First 1
    $g = @($longest.Group | Where-Object { $pick -notcontains $_ })
    for ($i = 0; $i -lt [Math]::Min($spare, $g.Count); $i++) { $pick += $g[[int][Math]::Floor(($i + 0.5) * $g.Count / [Math]::Min($spare, $g.Count))] }
  }
  $pick = $pick | Sort-Object { [int]($_.File -replace '\D', '') }
  $rows += [pscustomobject]@{ Dir = (Join-Path $to 'entity_frames'); Title = "$head | $mode"; Frames = ($pick | Select-Object -First $PerRow) }
}

$labelH = 22; $titleH = 20
$w = $PerRow * $Cell; $h = $rows.Count * ($Cell + $labelH + $titleH)
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(24, 24, 28))
$font = New-Object System.Drawing.Font 'Consolas', 8
$brush = [System.Drawing.Brushes]::WhiteSmoke
$y = 0
foreach ($r in $rows) {
  $g.DrawString($r.Title, $font, [System.Drawing.Brushes]::Khaki, 4, $y + 3)
  $x = 0
  foreach ($f in $r.Frames) {
    $img = [System.Drawing.Image]::FromFile((Join-Path $r.Dir $f.File))
    $g.DrawImage($img, $x, $y + $titleH, $Cell, $Cell); $img.Dispose()
    $g.DrawString("$($f.Stage) $($f.Phase) $($f.Action)", $font, $brush, $x + 2, $y + $titleH + $Cell + 3)
    $x += $Cell
  }
  $y += $Cell + $labelH + $titleH
}
$file = Join-Path $Out 'entity_contact_sheet.png'
$bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "CONTACT_SHEET=$file rows=$($rows.Count)"
