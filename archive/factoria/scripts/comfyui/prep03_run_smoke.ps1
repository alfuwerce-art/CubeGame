# PREP-03: воспроизводимый запуск ComfyUI (portable) + измерение RAM/VRAM + API smoke test.
#
# Что делает:
#   1. фиксирует базовую RAM/VRAM до запуска;
#   2. запускает D:\programs\ComfyUI\python_embeded\python.exe -s ComfyUI\main.py ... --reserve-vram <N>;
#   3. ждёт /system_stats (первый холодный старт ~150 c: миграция БД и скан ассетов);
#   4. запускает scripts\comfyui\prep03_comfyui_smoke.py и параллельно сэмплирует
#      рабочий набор процесса ComfyUI, свободную RAM и занятую VRAM (общую и процесса);
#   5. корректно останавливает сервер, печатает пики и путь к отчёту.
#
# Запуск (Windows PowerShell 5.1):
#   powershell -ExecutionPolicy Bypass -File "D:\Projects\CubeGame Factory\scripts\comfyui\prep03_run_smoke.ps1"
#   powershell -ExecutionPolicy Bypass -File "...\prep03_run_smoke.ps1" -CpuMode
param(
    [string]$Install = 'D:\programs\ComfyUI',
    [string]$Project = 'D:\Projects\CubeGame Factory',
    [int]$Port = 8188,
    [string]$ReserveVram = '5',
    [switch]$CpuMode
)

$ErrorActionPreference = 'Stop'
$python = Join-Path $Install 'python_embeded\python.exe'
$mainPy = Join-Path $Install 'ComfyUI\main.py'
if (-not (Test-Path $python)) { throw "python not found: $python" }
if (-not (Test-Path $mainPy)) { throw "main.py not found: $mainPy" }

$stamp = Get-Date -Format 'yyyy-MM-dd'
$tag = if ($CpuMode) { 'cpu' } else { "gpu_reserve$ReserveVram" }
$serverLog = Join-Path $Project "logs\PREP-03_comfyui_server_$($stamp)_$tag.txt"
$serverErr = Join-Path $Project "logs\PREP-03_comfyui_server_stderr_$($stamp)_$tag.txt"
$smokeLog  = Join-Path $Project "logs\PREP-03_comfyui_smoke_$($stamp)_$tag.txt"
$smokeErr  = Join-Path $Project "logs\PREP-03_comfyui_smoke_stderr_$($stamp)_$tag.txt"
New-Item -ItemType Directory -Force -Path (Split-Path $serverLog) | Out-Null

function Get-GpuUsedMB {
    try {
        $v = & nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits 2>$null | Select-Object -First 1
        if ($v) { return [int]$v.Trim() }
    } catch { }
    return -1
}

function Get-FreeRamGB {
    $os = Get-CimInstance Win32_OperatingSystem
    return [math]::Round($os.FreePhysicalMemory / 1MB, 2)
}

$ramBefore = Get-FreeRamGB
$vramBefore = Get-GpuUsedMB
$swAll = [Diagnostics.Stopwatch]::StartNew()

$argList = @('-s', $mainPy, '--windows-standalone-build', '--port', "$Port", '--disable-auto-launch')
if ($CpuMode) { $argList += '--cpu' } else { $argList += @('--reserve-vram', $ReserveVram) }

$proc = Start-Process -FilePath $python -ArgumentList $argList -WorkingDirectory $Install -PassThru `
    -RedirectStandardOutput $serverLog -RedirectStandardError $serverErr -WindowStyle Hidden
"comfyui_pid=$($proc.Id) mode=$tag"
"ram_before_GB=$ramBefore vram_before_MiB=$vramBefore"

# --- ожидание сервера ------------------------------------------------------
$base = "http://127.0.0.1:$Port"
$ready = $false
$t0 = [Diagnostics.Stopwatch]::StartNew()
while ($t0.Elapsed.TotalSeconds -lt 600) {
    if ($proc.HasExited) { break }
    try {
        $r = Invoke-WebRequest -Uri "$base/system_stats" -UseBasicParsing -TimeoutSec 5
        if ($r.StatusCode -eq 200) { $ready = $true; break }
    } catch { Start-Sleep -Milliseconds 1500 }
}
"server_ready=$ready after_s=$([math]::Round($t0.Elapsed.TotalSeconds,1))"
if (-not $ready) {
    "SERVER_FAILED_TO_START; last 40 lines of stderr:"
    Get-Content $serverErr -Tail 40 -ErrorAction SilentlyContinue
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    exit 2
}

# --- API smoke + замер ресурсов (сэмплируем в основном потоке) --------------
$smokeScript = Join-Path $Project 'scripts\comfyui\prep03_comfyui_smoke.py'
$quotedSmoke = '"' + $smokeScript + '"'
$smokeOut = Join-Path $Project 'outputs\comfyui'
$smokeReport = Join-Path $Project "outputs\reports\prep03_comfyui_smoke_$tag.json"
Remove-Item $smokeReport -Force -ErrorAction SilentlyContinue
$smokeProc = Start-Process -FilePath 'python' -ArgumentList @($quotedSmoke, '--url', $base, '--out', ('"' + $smokeOut + '"'), '--report', ('"' + $smokeReport + '"')) -PassThru `
    -RedirectStandardOutput $smokeLog -RedirectStandardError $smokeErr -WindowStyle Hidden

$peakWs = 0; $peakGpuTotal = -1; $peakGpuProc = 0; $minFreeRam = 999.0; $samples = 0
$sw = [Diagnostics.Stopwatch]::StartNew()
while (-not $smokeProc.HasExited) {
    try {
        $sp = Get-Process -Id $proc.Id -ErrorAction Stop
        if ($sp.WorkingSet64 -gt $peakWs) { $peakWs = $sp.WorkingSet64 }
    } catch { }
    try {
        $v = & nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits 2>$null | Select-Object -First 1
        if ($v) { $vi = [int]$v.Trim(); if ($vi -gt $peakGpuTotal) { $peakGpuTotal = $vi } }
    } catch { }
    try {
        $ca = & nvidia-smi --query-compute-apps=pid,used_memory --format=csv,noheader,nounits 2>$null
        foreach ($line in $ca) {
            $parts = $line -split ','
            if ($parts.Count -ge 2 -and $parts[0].Trim() -eq "$($proc.Id)") {
                $mi = [int]$parts[1].Trim()
                if ($mi -gt $peakGpuProc) { $peakGpuProc = $mi }
            }
        }
    } catch { }
    try {
        $fr = Get-FreeRamGB
        if ($fr -lt $minFreeRam) { $minFreeRam = $fr }
    } catch { }
    $samples++
    Start-Sleep -Milliseconds 300
}
$smokeProc.WaitForExit()
$smokeProc.Refresh()
$smokeExit = $smokeProc.ExitCode
$reportResult = 'FAIL'
if (Test-Path $smokeReport) {
    try { $reportResult = (Get-Content $smokeReport -Raw | ConvertFrom-Json).result } catch { }
}
"report_result=$reportResult smoke_seconds=$([math]::Round($sw.Elapsed.TotalSeconds,1)) samples=$samples"
"peak_working_set_MB=$([math]::Round($peakWs / 1MB, 0))"
"peak_gpu_used_MiB=$peakGpuTotal (вся карта, включая прочие приложения; до теста было $vramBefore)"
"peak_gpu_pid_MiB=$peakGpuProc (память процесса ComfyUI по nvidia-smi compute-apps)"
"min_free_ram_GB=$minFreeRam"

# --- остановка -------------------------------------------------------------
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force; Start-Sleep -Seconds 2 }
"server_stopped=$($proc.HasExited)"
"ram_after_GB=$(Get-FreeRamGB) vram_after_MiB=$(Get-GpuUsedMB)"
"total_seconds=$([math]::Round($swAll.Elapsed.TotalSeconds,1))"
"server_log=$serverLog"
"smoke_log=$smokeLog"
Get-Content $smokeLog
if (Test-Path $smokeErr) { $e = Get-Content $smokeErr -Raw; if ($e) { "smoke_stderr=$e" } }
if ($reportResult -eq 'PASS') { exit 0 } else { exit 1 }
