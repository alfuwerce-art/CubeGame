# FACTORIA / ComfyUI - reproducible launch with resource measurement.
#
# Presets (approved for COMFY-STYLE-02/03):
#   safe  -> --novram --reserve-vram 5   (SAFE_BASELINE: minimal extra VRAM)
#   cpu   -> --cpu                       (CPU_FALLBACK: no GPU inference)
#   gpu   -> --reserve-vram 5            (reference, PREP-03 baseline)
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts\comfyui\factoria_launch.ps1 -Preset safe -KeepRunning
#   powershell -ExecutionPolicy Bypass -File scripts\comfyui\factoria_launch.ps1 -Preset safe -MeasureIdle
#
# The script never installs anything and does not touch global Python/PATH/CUDA.
param(
    [ValidateSet('safe', 'cpu', 'gpu', 'xl', 'xl_safe')]
    [string]$Preset = 'safe',
    [string]$Install = 'D:\programs\ComfyUI',
    [string]$Project = 'D:\Projects\CubeGame Factory',
    [int]$Port = 8188,
    [switch]$KeepRunning,
    [switch]$MeasureIdle,
    [switch]$DisableAssets,
    [int]$IdleSeconds = 10,
    [string]$LogPrefix = 'COMFY-STYLE'
)

$ErrorActionPreference = 'Stop'
$python = Join-Path $Install 'python_embeded\python.exe'
$mainPy = Join-Path $Install 'ComfyUI\main.py'
if (-not (Test-Path $python)) { throw "python not found: $python" }
if (-not (Test-Path $mainPy)) { throw "main.py not found: $mainPy" }

$stamp = Get-Date -Format 'yyyy-MM-dd'
$logDir = Join-Path $Project 'logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$serverLog = Join-Path $logDir "$($LogPrefix)_server_$($stamp)_$Preset.log"
$serverErr = Join-Path $logDir "$($LogPrefix)_server_stderr_$($stamp)_$Preset.log"

function Get-GpuUsedMB {
    $v = & nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits 2>$null | Select-Object -First 1
    if ($v) { return [int]$v.Trim() }
    return -1
}
function Get-FreeRamGB {
    $os = Get-CimInstance Win32_OperatingSystem
    return [math]::Round($os.FreePhysicalMemory / 1MB, 2)
}

$presetArgs = switch ($Preset) {
    'safe'    { @('--novram', '--reserve-vram', '5') }
    'cpu'     { @('--cpu') }
    'gpu'     { @('--reserve-vram', '5') }
    # SDXL on 8 GB: dynamic VRAM with 1 GB reserved for the OS/other apps.
    'xl'      { @('--reserve-vram', '1') }
    # SDXL while keeping the SD1.5-era behaviour (weights in RAM, minimal VRAM).
    'xl_safe' { @('--novram', '--reserve-vram', '5') }
}
$argList = @('-s', $mainPy, '--windows-standalone-build', '--disable-auto-launch', '--port', "$Port") + $presetArgs
# A second copy of ComfyUI cannot share user\comfyui.db with the owner's instance:
# --disable-assets gives this test instance a DB-free start.
if ($DisableAssets) { $argList += '--disable-assets' }

$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'

$ramBefore = Get-FreeRamGB
$vramBefore = Get-GpuUsedMB
"preset=$Preset"
"args=$($argList -join ' ')"
"ram_free_before_GB=$ramBefore vram_used_before_MiB=$vramBefore"

$proc = Start-Process -FilePath $python -ArgumentList $argList -WorkingDirectory $Install -PassThru `
    -RedirectStandardOutput $serverLog -RedirectStandardError $serverErr -WindowStyle Hidden
"comfyui_pid=$($proc.Id)"

$base = "http://127.0.0.1:$Port"
$sw = [Diagnostics.Stopwatch]::StartNew()
$ready = $false
while ($sw.Elapsed.TotalSeconds -lt 400) {
    if ($proc.HasExited) { break }
    try {
        $r = Invoke-WebRequest -Uri "$base/system_stats" -UseBasicParsing -TimeoutSec 5
        if ($r.StatusCode -eq 200) { $ready = $true; break }
    } catch { Start-Sleep -Milliseconds 1500 }
}
"server_ready=$ready startup_s=$([math]::Round($sw.Elapsed.TotalSeconds,1))"
if (-not $ready) {
    "SERVER_FAILED_TO_START"
    "--- stderr tail ---"
    Get-Content $serverErr -Tail 25 -ErrorAction SilentlyContinue
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    exit 2
}

if ($MeasureIdle) {
    # Idle = server up, no workflow queued. Sample process working set + card VRAM.
    Start-Sleep -Seconds $IdleSeconds
    $sp = Get-Process -Id $proc.Id
    $vramIdle = Get-GpuUsedMB
    $stats = (Invoke-WebRequest -Uri "$base/system_stats" -UseBasicParsing -TimeoutSec 10).Content | ConvertFrom-Json
    "idle_working_set_MB=$([math]::Round($sp.WorkingSet64 / 1MB, 0))"
    "idle_free_ram_GB=$(Get-FreeRamGB)"
    "idle_vram_used_MiB=$vramIdle  (delta vs before: $($vramIdle - $vramBefore) MiB)"
    "comfy_total_ram_GB=$([math]::Round($stats.system.ram_total/1073741824,1))"
    "comfy_total_vram_GB=$([math]::Round($stats.system.vram_total/1073741824,1))"
    "comfy_torch_vram_free_GB=$([math]::Round($stats.devices[0].vram_free/1073741824,2))"
    "device=$($stats.devices[0].name)"
}

if ($KeepRunning) {
    "server_left_running pid=$($proc.Id) url=$base"
} else {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force; Start-Sleep -Seconds 2 }
    "server_stopped=$($proc.HasExited)"
}
"server_log=$serverLog"
"server_stderr=$serverErr"
if ($ready) { exit 0 } else { exit 1 }
