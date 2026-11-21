# FACTORIA / ComfyUI - restartable, watchdog-protected parallel downloader.
#
# Diagnosis that led to this version (COMFY-STYLE-04 recovery, 2026-11-14):
#   * HuggingFace CDN sometimes accepts a TCP connection and then never sends body bytes
#     ("idle established"). Observed: 6 of 12 range connections ESTABLISHED to the HF edge,
#     0 bytes read for >40 min, while a fresh single range request to the SAME file returned
#     HTTP 206 with data within seconds.
#   * curl without --connect-timeout and with a 1-hour --max-time waits forever on such a
#     connection, and --speed-limit/--speed-time does not fire before the transfer starts.
#   * The previous version waited for ALL connections (WaitForExit) before doing anything,
#     so a single hung connection froze the whole file.
#
# What this version does:
#   * Range support is verified up front (GET bytes 0-0 -> expect 206 + content-range).
#   * A per-connection watchdog kills only the stalled curl (no output growth for -StallSeconds)
#     and the part is retried with limited exponential backoff.
#   * Every finished/partial part is kept; nothing is re-downloaded (byte-level resume via
#     <part>.tail), completed parts are validated by exact size before reuse.
#   * Progress lines show downloaded MB, rate, ETA and the time of the last received data.
#   * Default parallelism lowered to 4 connections (12 was measurably unstable on this host).
#   * Final assembly is verified by SHA-256; the target file is replaced atomically.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File fetch_checkpoint_parallel.ps1 `
#       -Url <url> -Target <file> -ExpectedSha256 <hex> -TotalBytes <n> [-Connections 4]
param(
    [Parameter(Mandatory = $true)][string]$Url,
    [Parameter(Mandatory = $true)][string]$Target,
    [Parameter(Mandatory = $true)][string]$ExpectedSha256,
    [Parameter(Mandatory = $true)][int64]$TotalBytes,
    [int]$Connections = 4,
    [int]$MaxConcurrent = 0,
    [int]$ConnectTimeoutSeconds = 20,
    [int]$AttemptMaxSeconds = 1800,
    [int]$StallSeconds = 60,
    [int]$MaxAttempts = 6,
    [int]$BackoffMaxSeconds = 30,
    [int]$ProgressEverySeconds = 30,
    [switch]$SkipRangeCheck
)

$ErrorActionPreference = 'Stop'
$targetDir = Split-Path $Target -Parent
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
$partsDir = "$Target.parts"
New-Item -ItemType Directory -Force -Path $partsDir | Out-Null

function Get-LogStamp { (Get-Date).ToString('HH:mm:ss') }

function Test-RangeSupport {
    param([string]$U, [int]$TimeoutSec)
    $probe = Join-Path $env:TEMP ("range_probe_" + [guid]::NewGuid().ToString('N') + ".bin")
    $hdr = "$probe.hdr"
    try {
        & curl.exe -sL --fail --show-error --connect-timeout $TimeoutSec --max-time ($TimeoutSec * 3) `
            -r 0-0 -o $probe -D $hdr $U 2>$null | Out-Null
        if (-not (Test-Path $hdr)) { return $false }
        $text = Get-Content $hdr -Raw
        $has206 = $text -match 'HTTP/\d\.\d 206'
        $hasRange = $text -match 'content-range:\s*bytes\s+0-0/\d+'
        return ($has206 -and $hasRange)
    } catch { return $false } finally {
        Remove-Item $probe, $hdr -Force -ErrorAction SilentlyContinue
    }
}

function Get-PartBytes {
    param($Part)
    $n = 0
    if (Test-Path $Part.Path) { $n += (Get-Item $Part.Path).Length }
    $tail = "$($Part.Path).tail"
    if (Test-Path $tail) { $n += (Get-Item $tail).Length }
    return [int64]$n
}

function Merge-Tail {
    param($Part)
    $tail = "$($Part.Path).tail"
    if ((Test-Path $tail) -and ((Get-Item $tail).Length -gt 0)) {
        $have = if (Test-Path $Part.Path) { (Get-Item $Part.Path).Length } else { 0 }
        $tailLen = (Get-Item $tail).Length
        if (($have + $tailLen) -le $Part.Size) {
            $fsA = [System.IO.File]::Open($Part.Path, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write)
            try {
                $bytes = [System.IO.File]::ReadAllBytes($tail)
                $fsA.Write($bytes, 0, $bytes.Length)
            } finally { $fsA.Dispose() }
            "[$(Get-LogStamp)] merged_tail part=$($Part.Index) bytes=$tailLen"
        }
        Remove-Item $tail -Force -ErrorAction SilentlyContinue
    }
}

# ---- already complete? ----------------------------------------------------
if ((Test-Path $Target) -and (Get-Item $Target).Length -eq $TotalBytes) {
    $sha = (Get-FileHash -Path $Target -Algorithm SHA256).Hash.ToLower()
    if ($sha -eq $ExpectedSha256.ToLower()) { "already_complete=true sha256=$sha"; "SHA256_OK"; exit 0 }
    "existing_target_sha_mismatch -> rebuilding from parts"
}

# ---- range support check --------------------------------------------------
if (-not $SkipRangeCheck) {
    "[$(Get-LogStamp)] checking HTTP Range support ..."
    if (-not (Test-RangeSupport -U $Url -TimeoutSec $ConnectTimeoutSeconds)) {
        "RANGE_NOT_SUPPORTED_OR_PROBE_FAILED - refusing parallel mode (server must answer 206 + content-range)"
        exit 8
    }
    "[$(Get-LogStamp)] range support OK (206 + content-range)"
}

$chunk = [int64][math]::Ceiling($TotalBytes / $Connections)
"target=$Target"
"total_bytes=$TotalBytes connections=$Connections chunk_bytes=$chunk parts_dir=$partsDir"

$parts = @()
for ($i = 0; $i -lt $Connections; $i++) {
    $start = $i * $chunk
    if ($start -ge $TotalBytes) { break }
    $end = [math]::Min($start + $chunk - 1, $TotalBytes - 1)
    $parts += [pscustomobject]@{
        Index = $i; Start = $start; End = $end
        Size  = $end - $start + 1
        Path  = (Join-Path $partsDir "part_$i.bin")
    }
}
# Layout guard: a parts directory created with a different chunk size (e.g. after changing
# -Connections) contains byte ranges that do not match the current plan. Mixing layouts corrupts
# the assembled file (this actually happened: SHA-256 mismatch after a 4 -> 12 connection switch).
# In that case the old directory is moved aside (never deleted) and a clean download starts.
$layoutFile = Join-Path $partsDir 'layout.json'
$currentLayout = @{ total = $TotalBytes; connections = $Connections; chunk = $chunk } | ConvertTo-Json -Compress
$existing = Get-ChildItem $partsDir -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'part_*.bin*' }
if (Test-Path $layoutFile) {
    $oldLayout = Get-Content $layoutFile -Raw
    if ($oldLayout.Trim() -ne $currentLayout) {
        $stale = "$partsDir.stale-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        "[$(Get-LogStamp)] LAYOUT_CHANGED old=$($oldLayout.Trim()) new=$currentLayout -> moving parts aside to $stale"
        Move-Item -Path $partsDir -Destination $stale -Force
        New-Item -ItemType Directory -Force -Path $partsDir | Out-Null
        $layoutFile = Join-Path $partsDir 'layout.json'
    }
} elseif ($existing.Count -gt 0) {
    $bad = @($existing | Where-Object { $_.Length -gt $chunk })
    if ($bad.Count -gt 0) {
        $stale = "$partsDir.stale-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        "[$(Get-LogStamp)] LEGACY_PARTS_WITH_WRONG_RANGE (part larger than chunk) -> moving aside to $stale"
        Move-Item -Path $partsDir -Destination $stale -Force
        New-Item -ItemType Directory -Force -Path $partsDir | Out-Null
        $layoutFile = Join-Path $partsDir 'layout.json'
    }
}
Set-Content -Path $layoutFile -Value $currentLayout -Encoding ascii

"[$(Get-LogStamp)] plan: $($parts.Count) parts, each $([math]::Round($chunk / 1MB, 0)) MB"

# recover tails left by an interrupted run before anything else
foreach ($p in $parts) { Merge-Tail $p }

$attempts = @{}
foreach ($p in $parts) { $attempts[$p.Index] = 0 }
$sw = [Diagnostics.Stopwatch]::StartNew()
$lastProgressPrint = (Get-Date).AddMinutes(-10)
$lastDataAt = Get-Date
$noGrowthRounds = 0
$lastRateBytes = [int64](($parts | ForEach-Object { Get-PartBytes $_ } | Measure-Object -Sum).Sum)
$lastRateAt = Get-Date

while ($true) {
    $pending = @($parts | Where-Object {
            (Get-PartBytes $_) -lt $_.Size
        })
    if ($pending.Count -eq 0) { "[$(Get-LogStamp)] ALL_PARTS_COMPLETE"; break }

    $toLaunch = @()
    foreach ($p in $pending) {
        if ($attempts[$p.Index] -ge $MaxAttempts) {
            "PART_GAVE_UP part=$($p.Index) attempts=$($attempts[$p.Index])"
            continue
        }
        # finish the ranges that are closest to completion first
        $toLaunch += $p
    }
    if ($toLaunch.Count -eq 0) { "INCOMPLETE all remaining parts exhausted attempts"; exit 7 }
    if ($MaxConcurrent -le 0) { $MaxConcurrent = $Connections }
    $toLaunch = @($toLaunch | Sort-Object { $_.Size - (Get-PartBytes $_) } | Select-Object -First $MaxConcurrent)
    foreach ($p in $toLaunch) { $attempts[$p.Index]++ }

    # limited exponential backoff only when the previous round produced no new bytes at all
    if ($noGrowthRounds -gt 0) {
        $backoff = [math]::Min([math]::Pow(2, $noGrowthRounds), $BackoffMaxSeconds)
        "[$(Get-LogStamp)] no-progress backoff ${backoff}s (consecutive empty rounds: $noGrowthRounds)"
        Start-Sleep -Seconds $backoff
    }
    $bytesBeforeRound = ($parts | ForEach-Object { Get-PartBytes $_ } | Measure-Object -Sum).Sum

    $running = @()
    foreach ($p in $toLaunch) {
        $have = if (Test-Path $p.Path) { (Get-Item $p.Path).Length } else { 0 }
        if ($have -gt $p.Size) { $have = 0 }           # never delete a part; restart only this range
        $tail = "$($p.Path).tail"
        Remove-Item $tail -Force -ErrorAction SilentlyContinue
        $rangeStart = $p.Start + $have
        $out = if ($have -gt 0) { $tail } else { $p.Path }
        $proc = Start-Process -FilePath 'curl.exe' -PassThru -WindowStyle Hidden -ArgumentList @(
            '-L', '--fail', '--silent', '--show-error',
            '--connect-timeout', "$ConnectTimeoutSeconds",
            '--max-time', "$AttemptMaxSeconds",
            '-r', "$rangeStart-$($p.End)", '-o', $out, $Url)
        $running += [pscustomobject]@{ Part = $p; Proc = $proc; LastBytes = (Get-PartBytes $p); LastChange = (Get-Date) }
    }
    "[$(Get-LogStamp)] launched $($running.Count) connections (stall watchdog ${StallSeconds}s)"

    # ---- watchdog loop: never wait for all blindly ----
    while ($true) {
        Start-Sleep -Seconds 3
        $alive = 0
        foreach ($r in $running) {
            $r.Proc.Refresh()
            $nowBytes = Get-PartBytes $r.Part
            if ($nowBytes -gt $r.LastBytes) {
                $r.LastBytes = $nowBytes
                $r.LastChange = Get-Date
                $lastDataAt = Get-Date
            }
            if (-not $r.Proc.HasExited) {
                $idle = ((Get-Date) - $r.LastChange).TotalSeconds
                if ($idle -gt $StallSeconds) {
                    "[$(Get-LogStamp)] STALL part=$($r.Part.Index) idle=${StallSeconds}s -> killing this connection only"
                    Stop-Process -Id $r.Proc.Id -Force -ErrorAction SilentlyContinue
                } else { $alive++ }
            }
        }
        # progress line
        if (((Get-Date) - $lastProgressPrint).TotalSeconds -ge $ProgressEverySeconds) {
            $have = ($parts | ForEach-Object { Get-PartBytes $_ } | Measure-Object -Sum).Sum
            # instantaneous rate over the last progress window (not the run average: resumed
            # files already contain data, so total/elapsed would be meaningless)
            $dtRate = ((Get-Date) - $lastRateAt).TotalSeconds
            $rate = if ($dtRate -gt 0) { ($have - $lastRateBytes) / $dtRate } else { 0 }
            if ($rate -lt 0) { $rate = 0 }
            $lastRateBytes = $have
            $lastRateAt = Get-Date
            $left = $TotalBytes - $have
            $eta = if ($rate -gt 0) { [math]::Round(($left / $rate) / 60, 0) } else { 'n/a' }
            "[$(Get-LogStamp)] progress {0:N0}/{1:N0} MB ({2:N1}%) | {3:N0} KB/s | ETA {4} min | last data {5}" -f `
                ($have / 1MB), ($TotalBytes / 1MB), (100 * $have / $TotalBytes), ($rate / 1KB), $eta, $lastDataAt.ToString('HH:mm:ss')
            $lastProgressPrint = Get-Date
        }
        if ($alive -eq 0) { break }
        if ($sw.Elapsed.TotalSeconds -gt (($TotalBytes / 1MB) * 60)) { "GLOBAL_TIMEOUT"; exit 7 }
    }
    foreach ($r in $running) { if (-not $r.Proc.HasExited) { Stop-Process -Id $r.Proc.Id -Force -ErrorAction SilentlyContinue } }
    foreach ($r in $running) { Merge-Tail $r.Part }
    $bytesAfterRound = ($parts | ForEach-Object { Get-PartBytes $_ } | Measure-Object -Sum).Sum
    if (($bytesAfterRound - $bytesBeforeRound) -gt 0) { $noGrowthRounds = 0 } else { $noGrowthRounds++ }
    $done = @($parts | Where-Object { (Get-PartBytes $_) -eq $_.Size }).Count
    "[$(Get-LogStamp)] round done: complete_parts=$done/$($parts.Count) gained_MB=$([math]::Round(($bytesAfterRound - $bytesBeforeRound)/1MB,1)) elapsed_s=$([math]::Round($sw.Elapsed.TotalSeconds,0))"
}

# ---- assemble -------------------------------------------------------------
foreach ($p in $parts) { Merge-Tail $p }
$tmp = "$Target.new"
if (Test-Path $tmp) { Remove-Item $tmp -Force }
$fs = [System.IO.File]::Open($tmp, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
try {
    foreach ($p in $parts) {
        if ((Get-PartBytes $p) -ne $p.Size) { "PART_INCOMPLETE_AT_ASSEMBLY part=$($p.Index)"; exit 7 }
        $bytes = [System.IO.File]::ReadAllBytes($p.Path)
        $fs.Write($bytes, 0, $bytes.Length)
    }
} finally { $fs.Dispose() }

if ((Get-Item $tmp).Length -ne $TotalBytes) {
    "ASSEMBLED_SIZE_MISMATCH got=$((Get-Item $tmp).Length) expected=$TotalBytes"; exit 3
}
# Verify BEFORE replacing the target and BEFORE dropping the parts: on a mismatch the
# downloaded bytes stay on disk for inspection instead of being lost.
$sha = (Get-FileHash -Path $tmp -Algorithm SHA256).Hash.ToLower()
"sha256=$sha"
if ($sha -ne $ExpectedSha256.ToLower()) {
    "SHA256_MISMATCH expected=$($ExpectedSha256.ToLower())"
    "assembled_file_kept=$tmp parts_kept=$partsDir"
    exit 4
}
Move-Item -Path $tmp -Destination $Target -Force
Remove-Item $partsDir -Recurse -Force -ErrorAction SilentlyContinue
"SHA256_OK"
exit 0
