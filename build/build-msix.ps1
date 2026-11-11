<#
.SYNOPSIS
    MOD73: assembles the Microsoft Store package from the publish folder, after claude-tray's
    build\build-msix.ps1 (T409, T416).

.DESCRIPTION
    Called by build-msix.cmd after build\publish.cmd. Steps:
      1) the version: <Version> in Directory.Build.props, as X.Y.Z.0 (the Store requires the fourth
         part to be 0);
      2) the layout in artifacts\msix\layout: the publish folder the Inno installer takes, less what
         installer.iss excludes, the manifest with the version, and the logos tools\Modelado.Icon
         draws (--msix-assets);
      3) makepri indexes the logo variants, so the taskbar icon is not drawn on an accent plate;
      4) makeappx pack into artifacts\msix\Modelado.msix, unsigned: the Store signs it on upload;
      5) with -Register, the layout is installed on this machine (Add-AppxPackage -Register), the one
         way to run the package without a certificate; it needs Developer Mode.
#>
[CmdletBinding()]
param([switch]$Register)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# --- 1) Version ------------------------------------------------------------------------------
$props = Get-Content (Join-Path $root 'Directory.Build.props') -Raw -Encoding UTF8
if ($props -notmatch '<Version>\s*([0-9]+\.[0-9]+\.[0-9]+)\s*</Version>') {
    throw '<Version> not found in Directory.Build.props'
}
$version = "$($Matches[1]).0"

# --- 2) Layout -------------------------------------------------------------------------------
$publish = Join-Path $root 'artifacts\publish'
if (-not (Test-Path (Join-Path $publish 'Modelado.exe'))) { throw "No Modelado.exe in $publish (run build\publish.cmd)" }
if (-not (Test-Path (Join-Path $publish 'modelado-cli.exe'))) { throw "No modelado-cli.exe in $publish (run build\publish.cmd)" }

$msixDir = Join-Path $root 'artifacts\msix'
$layout = Join-Path $msixDir 'layout'
if (Test-Path $msixDir) { Remove-Item $msixDir -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $layout 'Assets') | Out-Null

# The same files installer.iss takes: everything but symbols, the DirectML debug layer, import
# libraries and XML docs.
$excluded = @('*.pdb', 'DirectML.Debug.dll', '*.lib', '*.xml')
Get-ChildItem $publish -Recurse -File | Where-Object {
    $name = $_.Name
    -not ($excluded | Where-Object { $name -like $_ })
} | ForEach-Object {
    $target = Join-Path $layout $_.FullName.Substring($publish.Length + 1)
    New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
    Copy-Item $_.FullName $target
}

$manifest = Get-Content (Join-Path $PSScriptRoot 'msix\AppxManifest.xml') -Raw -Encoding UTF8
$manifest = $manifest.Replace('{VERSION}', $version)
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

& dotnet run --project (Join-Path $root 'tools\Modelado.Icon') -c Release -- --msix-assets (Join-Path $layout 'Assets')
if ($LASTEXITCODE -ne 0) { throw "Modelado.Icon --msix-assets exited with $LASTEXITCODE" }

Write-Host "layout: $layout  (version $version)"

# --- 3) makepri, 4) makeappx ------------------------------------------------------------------
$kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$makeappx = Get-ChildItem $kits -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like '*\x64\*' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw 'makeappx.exe not found: install the Windows SDK' }

$makepri = Join-Path $makeappx.DirectoryName 'makepri.exe'
$priconfig = Join-Path $msixDir 'priconfig.xml'
& $makepri createconfig /cf $priconfig /dq en-US /pv 10.0.0 /o | Out-Host
if ($LASTEXITCODE -ne 0) { throw "makepri createconfig exited with $LASTEXITCODE" }
# Only the logos are indexed. Over the whole layout, makepri reads .NET's satellite folders (cs\, de\,
# ja\ ...) as languages and splits resources.language-*.pri out of the index, which would list them
# as languages the package supports, beside the manifest's en-us alone.
$priRoot = Join-Path $msixDir 'pri'
New-Item -ItemType Directory -Force $priRoot | Out-Null
Copy-Item (Join-Path $layout 'Assets') $priRoot -Recurse
& $makepri new /pr $priRoot /cf $priconfig /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o | Out-Host
if ($LASTEXITCODE -ne 0) { throw "makepri new exited with $LASTEXITCODE" }

$msix = Join-Path $msixDir 'Modelado.msix'
& $makeappx.FullName pack /o /d $layout /p $msix | Out-Host
if ($LASTEXITCODE -ne 0) { throw "makeappx exited with $LASTEXITCODE" }
Write-Host "package: $msix"

# --- 5) Local install ------------------------------------------------------------------------
if ($Register) {
    Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml')
    Write-Host 'Installed from the layout: start Modelado from the Start menu.'
}
