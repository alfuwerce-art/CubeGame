#requires -Version 5.1
<#
    Creates (or repairs) the CubeGame desktop shortcut.

    The shortcut ALWAYS points to the stable launcher, never to a Unity build:
        CubeGame.lnk -> D:\Games\CubeGame\CubeGameLauncher.exe
    The launcher resolves the active build through current.txt, so game updates never touch
    the shortcut and never touch the launcher (see docs/decisions/D91_STABLE_LAUNCHER_CONTRACT.md).

    The Desktop folder is resolved through the Windows shell API, so no user name is hardcoded.

    Usage:
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\launcher\install_shortcut.ps1
#>

[CmdletBinding()]
param(
    [string]$LauncherRoot = 'D:\Games\CubeGame',
    [string]$ShortcutName = 'CubeGame.lnk'
)

$ErrorActionPreference = 'Stop'

$launcher = Join-Path $LauncherRoot 'CubeGameLauncher.exe'
if (-not (Test-Path $launcher)) { throw "Launcher not found: $launcher" }

$desktop = [Environment]::GetFolderPath('Desktop')
if (-not $desktop -or -not (Test-Path $desktop)) { throw "Desktop folder not found: '$desktop'" }

$shortcutPath = Join-Path $desktop $ShortcutName
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $launcher
$shortcut.WorkingDirectory = $LauncherRoot
$shortcut.IconLocation = "$launcher,0"
$shortcut.Description = 'CubeGame'
$shortcut.Save()

$check = $shell.CreateShortcut($shortcutPath)
Write-Host "SHORTCUT=$shortcutPath"
Write-Host "SHORTCUT_TARGET=$($check.TargetPath)"
Write-Host "SHORTCUT_WORKDIR=$($check.WorkingDirectory)"

if ($check.TargetPath -ne $launcher) {
    throw "Shortcut target mismatch: '$($check.TargetPath)' != '$launcher'"
}
