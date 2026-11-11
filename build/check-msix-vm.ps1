<#
.SYNOPSIS
    MOD73: runs check-msix.ps1 on the VMware guest against the layout build-msix.cmd made.

.DESCRIPTION
    The layout is under artifacts\, which .gitignore keeps out of what winwright's runner carries,
    so this stages a git folder of its own, as check-installer-vm.ps1 does. The guest's credentials
    are winwright's, as for run-tests-vm.cmd.

      build\build-msix.cmd
      powershell -NoProfile -File build\check-msix-vm.ps1

    Exits with the guest check's code.
#>
[CmdletBinding()]
param([string]$Stage = (Join-Path $env:TEMP 'modelado-msix-check'))

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$layout = Join-Path $repo 'artifacts\msix\layout'
if (-not (Test-Path (Join-Path $layout 'AppxManifest.xml'))) { Write-Host 'No layout in artifacts\msix: run build\build-msix.cmd first.'; exit 1 }

if (Test-Path $Stage) { Remove-Item -Recurse -Force $Stage }
New-Item -ItemType Directory -Force $Stage | Out-Null
Copy-Item $layout (Join-Path $Stage 'layout') -Recurse
Copy-Item (Join-Path $PSScriptRoot 'check-msix.ps1') $Stage
& git -C $Stage init --quiet
if ($LASTEXITCODE -ne 0) { exit 1 }

$winwright = if ($env:WINWRIGHT_HOME) { $env:WINWRIGHT_HOME } else { 'D:\Git\alegauss\winwright' }
& (Join-Path $winwright 'tools\run-tests-vm.ps1') -Tree $Stage -Name 'modelado-msix' -NoGate `
    -Run 'powershell -NoProfile -ExecutionPolicy Bypass -File check-msix.ps1'
exit $LASTEXITCODE
