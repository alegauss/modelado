<#
.SYNOPSIS
    MOD79, MOD81: runs store-shots.ps1 on the VMware guest, brings the window captures back and composes
    each under its caption into artifacts\store-shots.

.DESCRIPTION
    Stages the publish folder build\publish.cmd made, the site's demos and the script as a git folder
    the runner can carry, as check-installer-vm.ps1 does, and brings each capture back with -Bring.
    Then tools\Modelado.Icon --store-shot draws each screenshot at 1920 x 1080: the brand mark, the
    headline with its stressed words in [brackets], the line under it, the window, and the photo the
    model came from where there is one. The headlines must read as the captions in
    build\msix\store-listing.md, brackets aside, or this stops: the Store shows both.

      build\publish.cmd
      powershell -NoProfile -File build\store-shots-vm.ps1 [-ComposeOnly]

    -ComposeOnly composes again from the captures already in artifacts\store-shots\raw.
#>
[CmdletBinding()]
param(
    [string]$Stage = (Join-Path $env:TEMP 'modelado-store-shots'),
    [switch]$ComposeOnly
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repo 'artifacts\store-shots'
$raw = Join-Path $out 'raw'
$demo = Join-Path $repo 'site\public\demo'

# One per capture store-shots.ps1 takes, in its order.
$compose = @(
    @{ Headline = 'One photo becomes [a whole 3D model]'; Subline = 'TripoSR infers the sides and the back, on your own PC'; Photo = 'chair.png' },
    @{ Headline = 'Any photo becomes [a relief]'; Subline = 'Depth Anything V2 turns depth into height, textured with the photo'; Photo = 'studio\relief-face.jpg' },
    @{ Headline = 'Refine it, [every step undoable]'; Subline = 'Simplify, subdivide, smooth, fill holes and flatten a base' },
    @{ Headline = 'Export to GLB, OBJ, PLY or STL, [sized in millimetres]'; Subline = 'Ready for a 3D printer, a game engine, Blender or the web' }
)

# The captions the listing gives the Store, read from store-listing.md's numbered list.
$listing = Get-Content (Join-Path $PSScriptRoot 'msix\store-listing.md') -Encoding UTF8
$start = [Array]::IndexOf($listing, '**Screenshot captions**')
$captions = @($listing[($start + 1)..($listing.Count - 1)] | Where-Object { $_ -match '^\d+\. ' } | ForEach-Object { $_ -replace '^\d+\. ', '' })
$headlines = @($compose | ForEach-Object { $_.Headline -replace '[\[\]]', '' })
if (($captions -join "`n") -ne ($headlines -join "`n")) {
    Write-Host "The headlines and store-listing.md's captions differ:`n  listing:   $($captions -join ' | ')`n  headlines: $($headlines -join ' | ')"
    exit 1
}

if (-not $ComposeOnly) {
    $publish = Join-Path $repo 'artifacts\publish'
    if (-not (Test-Path (Join-Path $publish 'Modelado.exe'))) { Write-Host 'No artifacts\publish: run build\publish.cmd first.'; exit 1 }

    if (Test-Path $Stage) { Remove-Item -Recurse -Force $Stage }
    New-Item -ItemType Directory -Force $Stage | Out-Null
    Copy-Item $publish (Join-Path $Stage 'app') -Recurse
    Copy-Item $demo (Join-Path $Stage 'demo') -Recurse
    Copy-Item (Join-Path $PSScriptRoot 'store-shots.ps1') $Stage
    & git -C $Stage init --quiet
    if ($LASTEXITCODE -ne 0) { exit 1 }

    $bring = (1..$compose.Count | ForEach-Object { "capture-$_.png" }) -join ','
    $winwright = if ($env:WINWRIGHT_HOME) { $env:WINWRIGHT_HOME } else { 'D:\Git\alegauss\winwright' }
    & (Join-Path $winwright 'tools\run-tests-vm.ps1') -Tree $Stage -Name 'modelado-store-shots' -NoGate `
        -Run 'powershell -NoProfile -ExecutionPolicy Bypass -File store-shots.ps1 -Exe app\Modelado.exe -Demo demo -Out shots' `
        -ResultsIn 'shots' -Bring $bring
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    New-Item -ItemType Directory -Force $raw | Out-Null
    # The runner brings each one into the stage's results folder.
    Get-ChildItem $Stage -Recurse -Filter 'capture-*.png' | ForEach-Object { Copy-Item $_.FullName $raw -Force }
}

& dotnet build (Join-Path $repo 'tools\Modelado.Icon') -c Release -v q --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host 'tools\Modelado.Icon did not build.'; exit 1 }
$tool = Get-ChildItem (Join-Path $repo 'tools\Modelado.Icon\bin\Release') -Recurse -Filter 'Modelado.Icon.exe' | Select-Object -First 1
for ($i = 0; $i -lt $compose.Count; $i++) {
    $c = $compose[$i]
    $capture = Join-Path $raw "capture-$($i + 1).png"
    if (-not (Test-Path $capture)) { Write-Host "No $capture"; exit 1 }
    $arguments = @('--store-shot', $capture, (Join-Path $out "screenshot-$($i + 1).png"), $c.Headline, $c.Subline)
    if ($c.Photo) { $arguments += Join-Path $demo $c.Photo }
    & $tool.FullName @arguments
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
Write-Host "screenshots: $out"
