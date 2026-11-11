# Local staging of the closed Alpha 2013 interface font.
#
# Alpha renders every interface string with the TrueType font that ships next to Cube.exe as
# resource1.dat (ALPHA_CODE_STATIC: every widget calls FontCache_find_or_create_scalable(L"resource1.dat"),
# for example StartMenuWidget::vfunc_1 0x583320 and SystemWidget 0x587710). The file is a closed Picroma
# asset, so it is never committed: this script copies it from the read-only local installation into the
# Unity project, where it is imported as a normal Font and packaged into the user's local Player only.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\alpha_content\stage_alpha_ui_font.ps1
#   powershell ... -AlphaRoot 'D:\Games\Cube world' -Destination Assets\CubeGame\Presentation\Gameplay\Resources
#
# Markers:
#   STAGE_ALPHA_FONT=PASS    font copied and verified against the source hash
#   STAGE_ALPHA_FONT=ABSENT  local Alpha installation or the font file is missing; the build keeps the
#                            placeholder font and the runtime logs ALPHA_FONT=ABSENT
[CmdletBinding()]
param(
    [string]$AlphaRoot = 'D:\Games\Cube world',
    [string]$Destination = '',
    [switch]$AllowAbsent
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
if (-not $Destination) {
    $Destination = Join-Path $repoRoot 'Assets\CubeGame\Presentation\Gameplay\Resources'
}

$source = Join-Path $AlphaRoot 'resource1.dat'
$target = Join-Path $Destination 'AlphaFont.ttf'
$meta = "$target.meta"

if (-not (Test-Path $source)) {
    Write-Warning "Alpha UI font not found: $source"
    Write-Output "STAGE_ALPHA_FONT=ABSENT source=$source"
    if ($AllowAbsent) { exit 0 }
    exit 2
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null
Copy-Item -LiteralPath $source -Destination $target -Force

$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$targetHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
if ($sourceHash -ne $targetHash) {
    Write-Output "STAGE_ALPHA_FONT=FAIL hash mismatch source=$sourceHash target=$targetHash"
    exit 1
}

# Unity imports the staged file by its path; the meta file belongs to the local copy only and is ignored by git.
if (-not (Test-Path $meta)) {
    Write-Output "STAGE_ALPHA_FONT=META_PENDING unity will generate $meta on the next import"
}

Write-Output "STAGE_ALPHA_FONT=PASS source=$source sha256=$($sourceHash.Substring(0, 16)) target=$target"
exit 0
