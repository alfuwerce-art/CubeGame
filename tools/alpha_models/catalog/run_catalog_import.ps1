# run_catalog_import.ps1 - D64: Alpha model catalog -> ISOLATED Unity 6000.3.27f2 project (static geometry
# of every route). Steps: export (.cubdec + manifest, resumable) -> Unity batch import -> verdict.
# A PASS needs BOTH the marker CATALOG_IMPORT_RESULT=PASS in CatalogOutput/catalog_import.txt AND Unity exit 0.
# Alpha data stays in .scratch (git-ignored). Resume: re-running skips models already imported with the
# same blob SHA256 (Python export and the Unity importer both keep per-model records).
#
#   -Select sample   one model per (route, marker rule)  — representative check
#   -Select all      the whole catalog (DeepSeek mass run)
#   -Select ROUTE,.. e.g. EQUIPMENT_ATTACH,ITEM_STATIC  or name globs (iron-*)
param(
  [string]$Project = 'D:\Projects\CubeGame\.scratch\catalog\unity\AlphaCatalog',
  [string]$Select  = 'sample',
  [string]$Python  = 'python',
  [string]$Unity   = 'D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..\..").Path
$Project = [System.IO.Path]::GetFullPath($Project)
$scratchRoot = [System.IO.Path]::GetFullPath((Join-Path $repo '.scratch')) + [System.IO.Path]::DirectorySeparatorChar
if (-not $Project.StartsWith($scratchRoot, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Catalog project must stay inside repository .scratch' }
if (-not (Test-Path -LiteralPath $Unity)) { throw "Unity 6000.3.27f2 not found at $Unity (do not use 6.6)" }
$lockPath = Join-Path $Project 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
  try { $l = [System.IO.File]::Open($lockPath, 'Open', 'ReadWrite', 'None'); $l.Dispose() }
  catch { throw 'Unity project is already open; close it before running this project' }
  Remove-Item -LiteralPath $lockPath
}

# 1. isolated project skeleton (idempotent): accepted VoxelMesh + shader + the importer only
$scripts = "$Project\Assets\AlphaCatalog\Scripts"
New-Item -ItemType Directory -Force -Path "$scripts\Runtime", "$scripts\Editor", "$Project\Packages", "$Project\ProjectSettings" | Out-Null
if (-not (Test-Path -LiteralPath "$Project\Packages\manifest.json")) {
  '{ "dependencies": { "com.unity.modules.imageconversion": "1.0.0", "com.unity.modules.jsonserialize": "1.0.0" } }' | Set-Content -Encoding ascii "$Project\Packages\manifest.json"
}
if (-not (Test-Path -LiteralPath "$Project\ProjectSettings\ProjectVersion.txt")) {
  "m_EditorVersion: 6000.3.27f2" | Set-Content -Encoding ascii "$Project\ProjectSettings\ProjectVersion.txt"
}
$transfer = Join-Path $repo 'tools\alpha_models\transfer\unity\Runtime'
Copy-Item -Force (Join-Path $transfer 'VoxelMesh.cs'), (Join-Path $transfer 'VoxelShaded.shader') "$scripts\Runtime\"
Copy-Item -Force (Join-Path $PSScriptRoot 'unity\Editor\*') "$scripts\Editor\"

# 2. export (Python, resumable)
Push-Location $repo
try {
  $exp = & $Python -m tools.alpha_models.catalog.static_export --project $Project --select $Select
  if ($LASTEXITCODE -ne 0) { throw "static_export failed: $exp" }
  Write-Output "EXPORT $exp"
} finally { Pop-Location }

# 3. Unity batch import (no -quit: the importer exits with its own verdict code)
$out = Join-Path $Project 'CatalogOutput'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$marker = Join-Path $out 'catalog_import.txt'
if (Test-Path -LiteralPath $marker) { Move-Item -LiteralPath $marker -Destination (Join-Path $out ("catalog_import_prev_{0}.txt" -f (Get-Date -Format 'yyyyMMdd_HHmmss'))) }
$log = Join-Path $out 'unity_import.log'
$ua = @('-batchmode', '-nographics', '-projectPath', $Project, '-executeMethod', 'CubeGame.AlphaCatalog.AlphaCatalogImport.Run', '-logFile', $log)
$p = Start-Process -FilePath $Unity -ArgumentList $ua -Wait -PassThru -WindowStyle Hidden
$exit = [int]$p.ExitCode

# 4. verdict: marker AND exit code
$manPath = Join-Path $Project 'Assets\AlphaCatalog\Data\manifest.json'
if (Test-Path -LiteralPath $manPath) {
  $man = Get-Content -Raw -LiteralPath $manPath | ConvertFrom-Json
  Write-Output ("  MANIFEST models={0} absent_in_db={1} empty_visible={2}" -f @($man.models).Count, @($man.absent).Count, @($man.empty_visible).Count)
}
$problems = @()
if ($exit -ne 0) { $problems += "UNITY_EXIT_CODE=$exit (expected 0)" }
if (-not (Test-Path -LiteralPath $marker)) { $problems += "MARKER_FILE_MISSING $marker" }
else {
  $line = Select-String -Path $marker -Pattern '^CATALOG_IMPORT_RESULT=(\w+)' | Select-Object -First 1
  if (-not $line) { $problems += 'MARKER_NOT_FOUND' } elseif ($line.Matches[0].Groups[1].Value -ne 'PASS') { $problems += 'MARKER_VALUE=' + $line.Matches[0].Groups[1].Value }
  Get-Content -LiteralPath $marker | ForEach-Object { Write-Output "  $_" }
}
if ($problems.Count -eq 0) { Write-Output "CATALOG_IMPORT_RESULT=PASS exit=$exit"; exit 0 }
Write-Output "CATALOG_IMPORT_RESULT=FAIL exit=$exit"
$problems | ForEach-Object { Write-Output "  - $_" }
exit 1
