<#
.SYNOPSIS
    MOD84: the Store listing's trailer and its thumbnail, in every language of the listing.

.DESCRIPTION
    For each language section of build\msix\store-listing.md (store-listing-lib.ps1), writes the spec
    tools\Modelado.Icon --trailer reads: the screenshots' headlines and lines, the photo label, the
    trailer's own two lines, the site's demo models and photos, and that language's composed
    screenshots, which store-shots-vm.ps1 must have made first. Writes
    artifacts\store-trailer\<language>\trailer.mp4 and trailer.png (the thumbnail the Store asks
    beside each trailer), 1920 x 1080. Needs ffmpeg on the PATH.

      powershell -NoProfile -File build\store-trailer.ps1 [-Language pt-br]
#>
[CmdletBinding()]
param([string]$Language)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$demo = Join-Path $repo 'site\public\demo'
$shots = Join-Path $repo 'artifacts\store-shots'
$out = Join-Path $repo 'artifacts\store-trailer'
if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { Write-Host 'ffmpeg is not on the PATH.'; exit 1 }

. (Join-Path $PSScriptRoot 'store-listing-lib.ps1')
$langs = Read-StoreListing

& dotnet build (Join-Path $repo 'tools\Modelado.Icon') -c Release -v q --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host 'tools\Modelado.Icon did not build.'; exit 1 }
$tool = Get-ChildItem (Join-Path $repo 'tools\Modelado.Icon\bin\Release') -Recurse -Filter 'Modelado.Icon.exe' | Select-Object -First 1

$utf8 = New-Object Text.UTF8Encoding $false
foreach ($code in @($langs.Keys | Where-Object { -not $Language -or $_ -eq $Language })) {
    $l = $langs[$code]
    $screens = @(1..4 | ForEach-Object { Join-Path $shots "$code\screenshot-$_.png" })
    foreach ($s in $screens) { if (-not (Test-Path $s)) { Write-Host "No $s (run build\store-shots-vm.ps1 first)"; exit 1 } }
    $dir = Join-Path $out $code
    New-Item -ItemType Directory -Force $dir | Out-Null
    $spec = [ordered]@{
        headlines     = @($l.Shots | ForEach-Object { $_.Headline })
        sublines      = @($l.Shots | ForEach-Object { $_.Subline })
        photoLabel    = $l.PhotoLabel
        viewsHeadline = $l.Views.Headline
        viewsSubline  = $l.Views.Subline
        end           = $l.TrailerEnd
        objectModel   = (Join-Path $demo 'chair.glb')
        objectPhoto   = (Join-Path $demo 'chair.png')
        reliefModel   = (Join-Path $demo 'studio\relief-face.glb')
        reliefPhoto   = (Join-Path $demo 'studio\relief-face.jpg')
        screenshots   = $screens
    }
    $specPath = Join-Path $dir 'trailer.json'
    [IO.File]::WriteAllText($specPath, ($spec | ConvertTo-Json -Depth 4), $utf8)
    & $tool.FullName --trailer (Join-Path $dir 'trailer.mp4') $specPath
    if ($LASTEXITCODE -ne 0) { Write-Host "The $code trailer failed."; exit 1 }
}
Write-Host "trailers: $out"
