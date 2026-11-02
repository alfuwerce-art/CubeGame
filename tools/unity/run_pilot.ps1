#requires -Version 5.1
<#
    CubeGame S01-PILOT — автоматический прогон Unity.
    Запускает продюсер: см. RUN_INSTRUCTIONS.md рядом с этим файлом.

    Скрипт не меняет системные настройки и не устанавливает ПО: он только запускает
    уже установленный Unity 6000.3.27f2 и сохраняет логи/результаты в .unity-install\.
#>

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
while ($projectRoot -and -not (Test-Path (Join-Path $projectRoot 'Assets\CubeGame'))) {
    $parent = Split-Path -Parent $projectRoot
    if (-not $parent -or $parent -eq $projectRoot) { break }
    $projectRoot = $parent
}
if (-not $projectRoot -or -not (Test-Path (Join-Path $projectRoot 'Assets\CubeGame'))) {
    throw "Не найден Unity-проект (ожидался Assets\\CubeGame). Запустите скрипт из репозитория CubeGame."
}

$unity = 'D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe'
if (-not (Test-Path $unity)) {
    throw "Unity Editor не найден: $unity"
}

$install = Join-Path $projectRoot '.unity-install'
$logs = Join-Path $install 'logs'
$results = Join-Path $install 'results'
$shots = Join-Path $install 'shots'
$build = Join-Path $install 'build\Pilot'
foreach ($dir in @($logs, $results, $shots, $build)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$script:steps = New-Object System.Collections.Generic.List[object]

function Invoke-Unity {
    param(
        [string]$Name,
        [string[]]$UnityArgs,
        [int[]]$AcceptExitCodes = @(0)
    )

    $log = Join-Path $logs "$Name.log"
    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    Write-Host "Unity $($UnityArgs -join ' ')"
    $full = @('-logFile', $log) + $UnityArgs
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $unity -ArgumentList $full -PassThru -Wait -NoNewWindow
    $timer.Stop()
    $code = $process.ExitCode
    $ok = $AcceptExitCodes -contains $code
    $script:steps.Add([pscustomobject]@{ Step = $Name; ExitCode = $code; Ok = $ok; Seconds = [math]::Round($timer.Elapsed.TotalSeconds, 1); Log = $log })
    Write-Host ("exit={0} time={1:N1}s {2}" -f $code, $timer.Elapsed.TotalSeconds, $(if ($ok) { 'OK' } else { 'FAILED' })) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
    if (Test-Path $log) {
        $problems = Select-String -Path $log -Pattern 'error CS|Compilation failed|Aborting batchmode|No valid Unity Editor license|Package Manager.*error' -ErrorAction SilentlyContinue
        if ($problems) {
            Write-Host "--- подозрительные строки в логе ---" -ForegroundColor Yellow
            $problems | Select-Object -First 15 | ForEach-Object { Write-Host ("  " + $_.Line.Trim()) }
        }
    }
    return $ok
}

# 0. Лицензия
Write-Host "`n=== 0. Проверка лицензии Unity ===" -ForegroundColor Cyan
$licenseLog = Join-Path $logs '0_license.log'
$licenseProcess = Start-Process -FilePath $unity -ArgumentList @('-batchmode','-quit','-nographics','-logFile',$licenseLog) -PassThru -Wait -NoNewWindow
$licenseText = if (Test-Path $licenseLog) { Get-Content $licenseLog -Raw } else { '' }
if ($licenseText -match 'No valid Unity Editor license|License is not active|LICENSE SYSTEM .*failed') {
    Write-Host 'Лицензия Unity не активна. Войдите в Unity Hub (Unity Personal) и запустите скрипт снова.' -ForegroundColor Red
    $script:steps.Add([pscustomobject]@{ Step = 'license'; ExitCode = $licenseProcess.ExitCode; Ok = $false; Seconds = 0; Log = $licenseLog })
    $script:steps | Format-Table -AutoSize
    exit 10
}
$script:steps.Add([pscustomobject]@{ Step = 'license'; ExitCode = $licenseProcess.ExitCode; Ok = $true; Seconds = 0; Log = $licenseLog })
Write-Host 'Лицензия выглядит активной (или ошибок лицензии в логе нет).' -ForegroundColor Green

# 1. Импорт проекта
if (-not (Invoke-Unity -Name '1_project_import' -UnityArgs @('-batchmode','-quit','-nographics','-projectPath',$projectRoot))) {
    Write-Host 'Импорт проекта не удался — дальнейшие шаги остановлены.' -ForegroundColor Red
    $script:steps | Format-Table -AutoSize
    exit 20
}

# 2. EditMode тесты
$editResults = Join-Path $results 'editmode.xml'
Invoke-Unity -Name '2_tests_editmode' -UnityArgs @('-batchmode','-nographics','-projectPath',$projectRoot,'-runTests','-testPlatform','EditMode','-testResults',$editResults) -AcceptExitCodes @(0,2) | Out-Null

# 3. PlayMode тесты
$playResults = Join-Path $results 'playmode.xml'
Invoke-Unity -Name '3_tests_playmode' -UnityArgs @('-batchmode','-projectPath',$projectRoot,'-runTests','-testPlatform','PlayMode','-testResults',$playResults) -AcceptExitCodes @(0,2) | Out-Null

# 4. Сборка сцены
Invoke-Unity -Name '4_scene_build' -UnityArgs @('-batchmode','-quit','-nographics','-projectPath',$projectRoot,'-executeMethod','CubeGame.EditorTools.PilotSceneBuilder.Build') | Out-Null

# 5. Сборка плеера
Invoke-Unity -Name '5_player_build' -UnityArgs @('-batchmode','-quit','-nographics','-projectPath',$projectRoot,'-executeMethod','CubeGame.EditorTools.PilotBuild.BuildStandalone') | Out-Null

# 6. Запуск плеера со скриншотом
$player = Join-Path $build 'CubeGamePilot.exe'
if (Test-Path $player) {
    $shot = Join-Path $shots 'pilot_run.png'
    $playerLog = Join-Path $logs '6_player_run.log'
    Write-Host "`n=== 6. Запуск плеера ===" -ForegroundColor Cyan
    $playerProcess = Start-Process -FilePath $player -ArgumentList @('-cubeSeed','666666','-cubeChunks','8','-cubeScreenshot',$shot,'-cubeFrames','90','-logFile',$playerLog) -PassThru
    if (-not $playerProcess.WaitForExit(180000)) {
        $playerProcess.Kill()
        Write-Host 'Плеер не завершился за 180 с — процесс остановлен.' -ForegroundColor Yellow
    }
    $script:steps.Add([pscustomobject]@{ Step = '6_player_run'; ExitCode = $playerProcess.ExitCode; Ok = (Test-Path $shot); Seconds = 0; Log = $playerLog })
} else {
    Write-Host 'Плеер не собран, шаг запуска пропущен.' -ForegroundColor Yellow
    $script:steps.Add([pscustomobject]@{ Step = '6_player_run'; ExitCode = -1; Ok = $false; Seconds = 0; Log = '' })
}

# Итог
Write-Host "`n=== ИТОГ ===" -ForegroundColor Cyan
$script:steps | Format-Table Step, ExitCode, Ok, Seconds, Log -AutoSize | Out-String | Write-Host

$summaryPath = Join-Path $install 'run_summary.txt'
$script:steps | Format-Table Step, ExitCode, Ok, Seconds, Log -AutoSize | Out-String | Set-Content -Path $summaryPath -Encoding UTF8
Write-Host "Сводка: $summaryPath"
Write-Host "Логи:   $logs"
Write-Host "Тесты:  $results"
Write-Host "Скриншоты: $shots"

if (($script:steps | Where-Object { -not $_.Ok }).Count -gt 0) { exit 1 }
exit 0
