# Read-only DeepSeek Harness / CubeGame environment inspector.
# This command intentionally does NOT expose profile files, credentials, tokens or remote URLs.
[CmdletBinding()]
param(
  [string]$ProjectRoot = 'D:\Projects\CubeGame'
)
$ErrorActionPreference = 'Stop'

function Exists-Command([string]$name) {
  return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

$commands = [ordered]@{}
foreach ($name in @('git','node','dsh','unity','blender','UnrealEditor-Cmd','python','pwsh','code')) {
  $commands[$name] = Exists-Command $name
}

$gitInfo = [ordered]@{
  repository = $false
  branch = $null
  head = $null
  modifiedTrackedFiles = $null
  expectedOrigin = $null
}
if ((Test-Path -LiteralPath $ProjectRoot -PathType Container) -and $commands['git']) {
  $top = & git -C $ProjectRoot rev-parse --show-toplevel 2>$null
  if ($LASTEXITCODE -eq 0 -and $top) {
    $gitInfo['repository'] = $true
    $branchName = & git -C $ProjectRoot symbolic-ref --quiet --short HEAD 2>$null
    if ($LASTEXITCODE -eq 0) { $gitInfo['branch'] = ($branchName | Out-String).Trim() }
    $head = & git -C $ProjectRoot rev-parse --short HEAD 2>$null
    if ($LASTEXITCODE -eq 0) { $gitInfo['head'] = ($head | Out-String).Trim() }
    $status = @(& git -C $ProjectRoot status --porcelain=v1 --untracked-files=no 2>$null)
    if ($LASTEXITCODE -eq 0) { $gitInfo['modifiedTrackedFiles'] = @($status | Where-Object { $_ }).Count }
    $remote = & git -C $ProjectRoot remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0) {
      $gitInfo['expectedOrigin'] = [bool]($remote -match 'alfuwerce-art[/:]CubeGame(?:\.git)?$')
    }
  }
}

$skillsRoot = Join-Path $ProjectRoot '.dsh\skills'
$skills = @()
if (Test-Path -LiteralPath $skillsRoot -PathType Container) {
  $skills = @(
    Get-ChildItem -LiteralPath $skillsRoot -Directory -ErrorAction SilentlyContinue |
      Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'SKILL.md') } |
      Select-Object -ExpandProperty Name
  )
}
$homeRoot = if ($env:DSH_HOME) { $env:DSH_HOME } else { Join-Path $HOME '.dsh' }
$profileCandidates = @('cubegame-dev','desktop','web')
$profiles = [ordered]@{}
foreach ($p in $profileCandidates) {
  $profiles[$p] = Test-Path -LiteralPath (Join-Path (Join-Path $homeRoot 'profiles') $p) -PathType Container
}

$report = [ordered]@{
  script = 'CubeGame DSH read-only preflight'
  machineModified = $false
  projectRootExists = Test-Path -LiteralPath $ProjectRoot -PathType Container
  git = $gitInfo
  commandAvailable = $commands
  projectSkillsCount = @($skills).Count
  projectSkills = $skills
  dshProfileFoldersPresent = $profiles
  notes = @(
    'DSH Desktop may not publish a dsh command on PATH; false does not prove Desktop is absent.',
    'Profile folder presence does not prove a plugin is enabled or an agent preset exists.',
    'No secrets, config content, detailed file paths or remote URLs were printed.',
    'Do not enable hooks/plugins before validating the installed DSH version.'
  )
}
$report | ConvertTo-Json -Depth 6
