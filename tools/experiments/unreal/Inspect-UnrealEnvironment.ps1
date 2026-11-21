# Read-only Unreal installation and toolchain inspection. Does not install or change anything.
param(
    [string]$RepoRoot = 'D:\Projects\CubeGame',
    [string]$EngineRoot = ''
)
$ErrorActionPreference = 'Stop'
$locations = [System.Collections.Generic.List[string]]::new()
if ($EngineRoot) { $locations.Add($EngineRoot) }
if ($env:UE_ROOT) { $locations.Add($env:UE_ROOT) }
if ($env:UNREAL_ENGINE_ROOT) { $locations.Add($env:UNREAL_ENGINE_ROOT) }
$launcher = Join-Path $env:ProgramData 'Epic\UnrealEngineLauncher\LauncherInstalled.dat'
if (Test-Path -LiteralPath $launcher) {
    try {
        $manifest = Get-Content -LiteralPath $launcher -Raw | ConvertFrom-Json
        foreach ($item in @($manifest.InstallationList)) {
            if ($item.AppName -match '^UE_' -and $item.InstallLocation) {
                $locations.Add([string]$item.InstallLocation)
            }
        }
    } catch { Write-Warning "Epic Launcher manifest could not be parsed: $($_.Exception.Message)" }
}
foreach ($base in @(
    (Join-Path $env:ProgramFiles 'Epic Games'),
    'D:\Epic Games',
    'D:\Program Files\Epic Games',
    'E:\Epic Games'
)) {
    if (Test-Path -LiteralPath $base) {
        foreach ($folder in @(Get-ChildItem -LiteralPath $base -Directory -Filter 'UE_*' -ErrorAction SilentlyContinue)) {
            $locations.Add($folder.FullName)
        }
    }
}
$engines = @(
    foreach ($location in @($locations | Select-Object -Unique)) {
        $editor = Join-Path $location 'Engine\Binaries\Win64\UnrealEditor.exe'
        $cmd = Join-Path $location 'Engine\Binaries\Win64\UnrealEditor-Cmd.exe'
        $build = Join-Path $location 'Engine\Build\Build.version'
        $ubt = Join-Path $location 'Engine\Binaries\DotNET\UnrealBuildTool\UnrealBuildTool.dll'
        $version = $null
        if (Test-Path -LiteralPath $build) {
            try {
                $v = Get-Content -LiteralPath $build -Raw | ConvertFrom-Json
                $version = "$($v.MajorVersion).$($v.MinorVersion).$($v.PatchVersion)"
            } catch { $version = 'UNREADABLE' }
        }
        [pscustomobject]@{
            root = $location
            version = $version
            editor = (Test-Path -LiteralPath $editor)
            commandlet = (Test-Path -LiteralPath $cmd)
            unreal_build_tool = (Test-Path -LiteralPath $ubt)
        }
    }
)
$pf86 = [Environment]::GetFolderPath('ProgramFilesX86')
$vswhere = Join-Path $pf86 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsCxx = @()
if (Test-Path -LiteralPath $vswhere) {
    $vsCxx = @(& $vswhere -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null)
}
$git = Get-Command git -ErrorAction SilentlyContinue
$gitInfo = [ordered]@{ repository_found = (Test-Path -LiteralPath (Join-Path $RepoRoot '.git')); branch = $null; dirty = $null; head = $null }
if ($git -and $gitInfo.repository_found) {
    $gitInfo.branch = (& git -C $RepoRoot branch --show-current 2>$null | Out-String).Trim()
    $gitInfo.head = (& git -C $RepoRoot rev-parse --short HEAD 2>$null | Out-String).Trim()
    $gitInfo.dirty = [bool]((& git -C $RepoRoot status --porcelain 2>$null | Out-String).Trim())
}
$memoryGB = $null
$gpu = @()
try {
    $computer = Get-CimInstance Win32_ComputerSystem -ErrorAction Stop
    $memoryGB = [math]::Round($computer.TotalPhysicalMemory / 1GB, 1)
    $gpu = @(Get-CimInstance Win32_VideoController -ErrorAction Stop | ForEach-Object { $_.Name })
} catch {}
$mcpPortOpen = $false
$socket = [System.Net.Sockets.TcpClient]::new()
try {
    $connection = $socket.BeginConnect('127.0.0.1', 8000, $null, $null)
    if ($connection.AsyncWaitHandle.WaitOne(600)) {
        try { $socket.EndConnect($connection); $mcpPortOpen = $true } catch {}
    }
} finally { $socket.Dispose() }
[pscustomobject]@{
    task = 'UNREAL_U0_READ_ONLY'
    installed_engines = $engines
    visual_studio_cpp_toolchains = @($vsCxx)
    dotnet_cli_found = [bool](Get-Command dotnet -ErrorAction SilentlyContinue)
    windows_sdk_path = [string]((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' -ErrorAction SilentlyContinue).KitsRoot10)
    mcp_default_port_8000_open = $mcpPortOpen
    mcp_handshake = 'NOT_VERIFIED'
    ram_gb = $memoryGB
    gpu_names = $gpu
    github = $gitInfo
} | ConvertTo-Json -Depth 7
