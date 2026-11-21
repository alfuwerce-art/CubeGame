# check_ps51.ps1 - Windows PowerShell 5.1 compatibility check of the repository's PowerShell scripts.
# Run it with powershell.exe (5.1), not pwsh. For every script under tools\alpha_models:
#   * parse with the 5.1 parser exactly as 5.1 reads the file (no BOM -> ANSI code page);
#   * require ASCII-only content or a UTF-8 BOM (5.1 reads a BOM-less UTF-8 file as ANSI: an em dash
#     E2 80 94 becomes a curly quote in cp1252 and can break string parsing).
# Output: PS51_CHECK_RESULT=PASS|FAIL files=N failed=M, exit 0 only on PASS.
param([string]$Root = (Resolve-Path "$PSScriptRoot\..\..\..").Path)
$ErrorActionPreference = 'Stop'
$files = Get-ChildItem -Path (Join-Path $Root 'tools\alpha_models') -Recurse -Filter '*.ps1' | Sort-Object FullName
$failed = 0
foreach ($f in $files) {
  $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
  $bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
  $nonAscii = @($bytes | Where-Object { $_ -gt 127 }).Count
  $tokens = $null; $errors = $null
  [void][System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$tokens, [ref]$errors)
  $problems = @()
  if ($errors.Count -gt 0) { $problems += ($errors | ForEach-Object { "PARSE $($_.Extent.StartLineNumber): $($_.Message)" }) }
  if ($nonAscii -gt 0 -and -not $bom) { $problems += "NON_ASCII_WITHOUT_BOM bytes=$nonAscii" }
  $rel = $f.FullName.Substring($Root.Length + 1)
  if ($problems.Count -gt 0) { $failed++; Write-Output "FAIL $rel"; $problems | ForEach-Object { Write-Output "  - $_" } }
  else { Write-Output "PASS $rel" }
}
$v = $PSVersionTable.PSVersion.ToString()
Write-Output ("PS51_CHECK_RESULT={0} files={1} failed={2} powershell={3}" -f $(if ($failed -eq 0) { 'PASS' } else { 'FAIL' }), $files.Count, $failed, $v)
if ($failed -gt 0) { exit 1 }
exit 0
