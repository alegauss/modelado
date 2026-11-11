<#
.SYNOPSIS
    MOD79, MOD81, MOD92: runs store-shots.ps1 on the VMware guest, brings the window captures back and
    composes each under its caption, in every language of the listing, into
    artifacts\store-shots\<language>.

.DESCRIPTION
    Stages the publish folder build\publish.cmd made, the site's demos and the script as a git folder
    the runner can carry, as check-installer-vm.ps1 does, and brings each capture back with -Bring.
    Then tools\Modelado.Icon --store-shot draws each screenshot at 1920 x 1080: the brand mark, the
    headline with its stressed words in [brackets], the line under it, the window, and the photo the
    model came from where there is one. The words come from each language's captions in
    build\msix\store-listing.md (store-listing-lib.ps1), so the Store's caption and the headline drawn
    over the screenshot cannot drift apart.

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

# The photo each capture's model was made from, in store-shots.ps1's order; none for the edits.
$photos = @('chair.png', 'studio\relief-face.jpg', $null, $null)

# MOD92: the headlines, the lines under them and the photo label, per language, from store-listing.md.
. (Join-Path $PSScriptRoot 'store-listing-lib.ps1')
$langs = Read-StoreListing
foreach ($code in $langs.Keys) {
    if ($langs[$code].Shots.Count -ne $photos.Count) { Write-Host "store-listing.md: $code has $($langs[$code].Shots.Count) captions for $($photos.Count) captures"; exit 1 }
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

    $bring = (1..$photos.Count | ForEach-Object { "capture-$_.png" }) -join ','
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
# The same captures in every language: the window stays English until Block K translates it.
Get-ChildItem $out -Filter 'screenshot-*.png' -ErrorAction SilentlyContinue | Remove-Item
foreach ($code in $langs.Keys) {
    $l = $langs[$code]
    $dir = Join-Path $out $code
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    New-Item -ItemType Directory -Force $dir | Out-Null
    for ($i = 0; $i -lt $photos.Count; $i++) {
        $capture = Join-Path $raw "capture-$($i + 1).png"
        if (-not (Test-Path $capture)) { Write-Host "No $capture"; exit 1 }
        $shot = $l.Shots[$i]
        $arguments = @('--store-shot', $capture, (Join-Path $dir "screenshot-$($i + 1).png"), $shot.Headline, $shot.Subline)
        if ($photos[$i]) { $arguments += @((Join-Path $demo $photos[$i]), $l.PhotoLabel) }
        & $tool.FullName @arguments | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Host "--store-shot failed for $code $($i + 1)"; exit 1 }
    }
    Write-Host "$code -> $dir"
}
Write-Host "screenshots: $out"
