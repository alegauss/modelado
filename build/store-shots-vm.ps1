<#
.SYNOPSIS
    MOD79: runs store-shots.ps1 on the VMware guest and brings the screenshots back to
    artifacts\store-shots.

.DESCRIPTION
    Stages the publish folder build\publish.cmd made, the site's demos and the script as a git folder
    the runner can carry, as check-installer-vm.ps1 does, and brings each screenshot back with -Bring.

      build\publish.cmd
      powershell -NoProfile -File build\store-shots-vm.ps1
#>
[CmdletBinding()]
param([string]$Stage = (Join-Path $env:TEMP 'modelado-store-shots'))

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $repo 'artifacts\publish'
if (-not (Test-Path (Join-Path $publish 'Modelado.exe'))) { Write-Host 'No artifacts\publish: run build\publish.cmd first.'; exit 1 }

if (Test-Path $Stage) { Remove-Item -Recurse -Force $Stage }
New-Item -ItemType Directory -Force $Stage | Out-Null
Copy-Item $publish (Join-Path $Stage 'app') -Recurse
Copy-Item (Join-Path $repo 'site\public\demo') (Join-Path $Stage 'demo') -Recurse
Copy-Item (Join-Path $PSScriptRoot 'store-shots.ps1') $Stage
& git -C $Stage init --quiet
if ($LASTEXITCODE -ne 0) { exit 1 }

$count = 4
$bring = (1..$count | ForEach-Object { "screenshot-$_.png" }) -join ','
$winwright = if ($env:WINWRIGHT_HOME) { $env:WINWRIGHT_HOME } else { 'D:\Git\alegauss\winwright' }
& (Join-Path $winwright 'tools\run-tests-vm.ps1') -Tree $Stage -Name 'modelado-store-shots' -NoGate `
    -Run 'powershell -NoProfile -ExecutionPolicy Bypass -File store-shots.ps1 -Exe app\Modelado.exe -Demo demo -Out shots' `
    -ResultsIn 'shots' -Bring $bring
$code = $LASTEXITCODE

$out = Join-Path $repo 'artifacts\store-shots'
New-Item -ItemType Directory -Force $out | Out-Null
# The runner brings each one into the stage's results folder.
Get-ChildItem $Stage -Recurse -Filter 'screenshot-*.png' | ForEach-Object { Copy-Item $_.FullName $out -Force }
Write-Host "screenshots: $out"
exit $code
