<#
.SYNOPSIS
    MOD28: runs check-installer.ps1 on the VMware guest against the installer build-installer.cmd made.

.DESCRIPTION
    The installer is under artifacts\, which .gitignore keeps out of what winwright's runner
    carries, so this stages a tree of its own: the current installer, an older one compiled from the
    same installer.iss and publish (version 0.0.1), check-installer.ps1 and the cube fixture, as a
    git folder the runner can list (WW574 would let the runner carry them itself). The guest's
    credentials are winwright's, as for run-tests-vm.cmd.

      build\build-installer.cmd
      powershell -NoProfile -File build\check-installer-vm.ps1

    Exits with the guest check's code.
#>
[CmdletBinding()]
param([string]$Stage = (Join-Path $env:TEMP 'modelado-installer-check'))

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$installers = Join-Path $repo 'artifacts\installer'
$current = Get-ChildItem $installers -Filter 'Modelado-Setup-*.exe' -ErrorAction SilentlyContinue |
    Where-Object { $_.BaseName -ne 'Modelado-Setup-0.0.1' } | Select-Object -First 1
if (-not $current) { Write-Host 'No installer in artifacts\installer: run build\build-installer.cmd first.'; exit 1 }

$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { Write-Host 'ISCC.exe not found. Install Inno Setup 6: https://jrsoftware.org/isdl.php'; exit 1 }
& $iscc /Q /DMyAppVersion=0.0.1 (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE -ne 0) { Write-Host 'The older installer did not compile.'; exit 1 }

if (Test-Path $Stage) { Remove-Item -Recurse -Force $Stage }
New-Item -ItemType Directory -Force $Stage | Out-Null
Copy-Item $current.FullName, (Join-Path $installers 'Modelado-Setup-0.0.1.exe'), (Join-Path $PSScriptRoot 'check-installer.ps1'), (Join-Path $repo 'cases\fixtures\cube.obj') -Destination $Stage
& git -C $Stage init --quiet
if ($LASTEXITCODE -ne 0) { exit 1 }

$winwright = if ($env:WINWRIGHT_HOME) { $env:WINWRIGHT_HOME } else { 'D:\Git\alegauss\winwright' }
& (Join-Path $winwright 'tools\run-tests-vm.ps1') -Tree $Stage -Name 'modelado-installer' -NoGate `
    -Run 'powershell -NoProfile -ExecutionPolicy Bypass -File check-installer.ps1'
exit $LASTEXITCODE
