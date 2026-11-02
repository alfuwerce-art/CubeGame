#requires -Version 5.1
<#
    CubeGame — независимая проверка Core без Unity Editor.
    Компилирует Assets\CubeGame\Core (там нет ссылок на UnityEngine) компилятором Roslyn,
    входящим в установленный Unity, и запускает набор проверок детерминизма.

    Ничего не устанавливает и не меняет системные настройки; артефакты пишет в .unity-install\core_check\.
#>

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
while ($projectRoot -and -not (Test-Path (Join-Path $projectRoot 'Assets\CubeGame\Core'))) {
    $parent = Split-Path -Parent $projectRoot
    if (-not $parent -or $parent -eq $projectRoot) { break }
    $projectRoot = $parent
}
$coreDir = Join-Path $projectRoot 'Assets\CubeGame\Core'
$harness = Join-Path $projectRoot 'tools\core_check\Program.cs'
$outDir = Join-Path $projectRoot '.unity-install\core_check'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$unityData = 'D:\Unity\Hub\Editor\6000.3.27f2\Editor\Data'
$csc = Join-Path $unityData 'DotNetSdkRoslyn\csc.dll'
$mono = Join-Path $unityData 'MonoBleedingEdge\lib\mono\4.5'
if (-not (Test-Path $csc)) { throw "Не найден Roslyn: $csc" }
if (-not (Test-Path (Join-Path $mono 'mscorlib.dll'))) { throw "Не найдены сборки Mono: $mono" }

$sources = Get-ChildItem $coreDir -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$exe = Join-Path $outDir 'CoreCheck.exe'
$log = Join-Path $outDir 'core_check.log'

$cscArgs = @(
    $csc,
    '/nologo',
    '/langversion:latest',
    '/target:exe',
    "/out:$exe",
    "/r:$(Join-Path $mono 'mscorlib.dll')",
    "/r:$(Join-Path $mono 'System.dll')",
    "/r:$(Join-Path $mono 'System.Core.dll')"
) + $sources + @($harness)

Write-Host "Компиляция Core ($($sources.Count) файлов) -> $exe"
& dotnet @cscArgs 2>&1 | Tee-Object -FilePath $log
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) {
    Write-Host 'Компиляция не удалась — см. лог.' -ForegroundColor Red
    exit 20
}

Write-Host "`nЗапуск проверок:"
& $exe 2>&1 | Tee-Object -FilePath $log -Append
$code = $LASTEXITCODE
Write-Host "`nИтог: exit=$code, лог: $log"
exit $code
