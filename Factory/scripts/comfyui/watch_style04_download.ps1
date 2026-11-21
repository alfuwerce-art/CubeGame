# FACTORIA / ComfyUI - live progress for the COMFY-STYLE-04 model downloads.
#
# Read-only: it only looks at file sizes on disk (assembled target + "<target>.parts").
# The downloader itself (fetch_style04_models.ps1) may run in another window/session.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\comfyui\watch_style04_download.ps1
#   powershell -ExecutionPolicy Bypass -File scripts\comfyui\watch_style04_download.ps1 -Once
param(
    [string]$Install = 'D:\programs\ComfyUI',
    [string]$Project = 'D:\Projects\CubeGame Factory',
    [int]$RefreshSeconds = 5,
    [switch]$Once
)

$models = Join-Path $Install 'ComfyUI\models'

# target subdir + file name + expected byte size (same list as fetch_style04_models.ps1)
$manifest = @(
    [pscustomobject]@{ dir = 'checkpoints'; file = 'DreamShaperXL_Turbo_v2_1.safetensors';                 size = 6939220250 },
    [pscustomobject]@{ dir = 'clip_vision'; file = 'CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors';           size = 2528373448 },
    [pscustomobject]@{ dir = 'ipadapter';   file = 'ip-adapter-plus_sdxl_vit-h.safetensors';                size = 847517512  },
    [pscustomobject]@{ dir = 'controlnet';  file = 'controlnet-union-sdxl-1.0.safetensors';                 size = 2512030408 },
    [pscustomobject]@{ dir = 'loras';       file = 'VoxelXL_v1.safetensors';                                size = 912554516  }
)

function Get-Progress($item) {
    $target = Join-Path (Join-Path $models $item.dir) $item.file
    $done = 0
    if (Test-Path $target -PathType Leaf) {
        $len = (Get-Item $target).Length
        if ($len -eq $item.size) {
            return [pscustomobject]@{ Item = $item; Bytes = $item.size; State = 'COMPLETE' }
        }
    }
    $parts = "$target.parts"
    if (Test-Path $parts) {
        $done = (Get-ChildItem $parts -File -ErrorAction SilentlyContinue |
                Measure-Object Length -Sum).Sum
        if (-not $done) { $done = 0 }
    }
    $state = if ($done -le 0) { 'queued' } else { 'downloading' }
    return [pscustomobject]@{ Item = $item; Bytes = [int64]$done; State = $state }
}

function Show-Bar([double]$fraction, [int]$width = 24) {
    $n = [int][math]::Round($fraction * $width)
    if ($n -gt $width) { $n = $width }
    return ('#' * $n) + ('.' * ($width - $n))
}

$prevTotal = $null
$prevTime = Get-Date
$log = Join-Path $Project ("logs\COMFY-STYLE-04_download_" + (Get-Date -Format 'yyyy-MM-dd') + ".log")

while ($true) {
    $rows = @($manifest | ForEach-Object { Get-Progress $_ })
    $totalBytes = ($rows | Measure-Object -Property Bytes -Sum).Sum
    $totalSize = ($manifest | Measure-Object -Property size -Sum).Sum
    $now = Get-Date
    $rate = 0
    if ($prevTotal -ne $null) {
        $dt = ($now - $prevTime).TotalSeconds
        if ($dt -gt 0) { $rate = ($totalBytes - $prevTotal) / $dt }
    }
    $prevTotal = $totalBytes
    $prevTime = $now

    Clear-Host
    "FACTORIA COMFY-STYLE-04  |  {0}" -f $now.ToString('HH:mm:ss')
    "Лог: {0}" -f $log
    "Загрузчики curl: {0}" -f ((Get-Process curl -ErrorAction SilentlyContinue | Measure-Object).Count)
    ""
    foreach ($r in $rows) {
        $pct = if ($r.Item.size -gt 0) { $r.Bytes / $r.Item.size } else { 0 }
        "{0,-24} {1} {2,6:N1}%  {3,7:N0} / {4,7:N0} MB  {5}" -f `
            $r.Item.file.Substring(0, [Math]::Min(24, $r.Item.file.Length)),
            (Show-Bar $pct), ($pct * 100), ($r.Bytes / 1MB), ($r.Item.size / 1MB), $r.State
    }
    ""
    $totalPct = if ($totalSize -gt 0) { $totalBytes / $totalSize } else { 0 }
    "ВСЕГО: {0} {1,6:N1}%  {2:N0} / {3:N0} MB  ({4:N1} GB)" -f `
        (Show-Bar $totalPct), ($totalPct * 100), ($totalBytes / 1MB), ($totalSize / 1MB), ($totalSize / 1GB)
    if ($rate -gt 0) {
        $left = $totalSize - $totalBytes
        "Скорость: {0:N2} MB/s   Осталось: {1:N0} MB   ETA: {2:N0} мин" -f `
            ($rate / 1MB), ($left / 1MB), (($left / $rate) / 60)
    }
    else {
        "Скорость: нет данных (первый замер) или загрузка не идёт"
    }
    if ($Once) { break }
    Start-Sleep -Seconds $RefreshSeconds
}
