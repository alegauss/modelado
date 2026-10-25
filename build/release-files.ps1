<#
.SYNOPSIS
    MOD29: checks that a version, the build and the installer agree, and writes what a release holds.

.DESCRIPTION
    Run after build-installer.cmd, by update-release.cmd on the maintainer's machine and by
    .github\workflows\release.yml on the tag, so the two paths cannot drift: one implementation of
    the three decisions the updater depends on (the asset's name, its digest, the version).

    It refuses, with the two values that disagree, when the version asked for is not the one in
    Directory.Build.props, not the published Modelado.exe's product version, or not in the
    installer's file name. Otherwise it writes into -Out:

      Modelado-Setup-<x.y.z>.exe   the installer
      SHA256SUMS                   "<sha256>  Modelado-Setup-<x.y.z>.exe", what the updater checks
      notes.md                     the commit titles since the previous tag, each naming its task,
                                   and why Windows warns about an unsigned installer

      powershell -NoProfile -File build\release-files.ps1 -Version 0.2.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Out = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $Out) { $Out = Join-Path $root 'artifacts\release' }

function Refuse([string]$What) { Write-Host "*** $What ***"; exit 1 }

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') { Refuse "The version '$Version' is not x.y.z: a tag is v<x.y.z>." }

$props = [System.IO.File]::ReadAllText((Join-Path $root 'Directory.Build.props'))
$declared = if ($props -match '<Version>\s*([0-9.]+)\s*</Version>') { $Matches[1] } else { '' }
if ($declared -ne $Version) { Refuse "The release is $Version but Directory.Build.props says ${declared}: change <Version>, or the tag." }

$exe = Join-Path $root 'artifacts\publish\Modelado.exe'
if (-not (Test-Path $exe)) { Refuse 'artifacts\publish\Modelado.exe is missing: run build\build-installer.cmd first.' }
$built = (Get-Item $exe).VersionInfo.ProductVersion.Trim()
if ($built -ne $Version) { Refuse "The release is $Version but the published Modelado.exe is $built." }

$name = "Modelado-Setup-$Version.exe"
$installer = Join-Path $root "artifacts\installer\$name"
if (-not (Test-Path $installer)) { Refuse "The release is $Version but artifacts\installer holds no $name." }

if (Test-Path $Out) { Remove-Item -Recurse -Force $Out }
New-Item -ItemType Directory -Force $Out | Out-Null
Copy-Item $installer $Out

# sha256sum's own format, so `sha256sum -c SHA256SUMS` checks it too: lowercase hex, two spaces, LF.
$hash = (Get-FileHash -Algorithm SHA256 $installer).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText((Join-Path $Out 'SHA256SUMS'), "$hash  $name`n")

# The commits since the previous tag; every task's commit names its MOD id in the title.
# No earlier tag is an answer, not an error: Windows PowerShell would stop on git's stderr.
$ErrorActionPreference = 'Continue'
$previous = & git -C $root describe --tags --abbrev=0 --match 'v*' 'HEAD^' 2>$null
$ErrorActionPreference = 'Stop'
$range = if ($LASTEXITCODE -eq 0 -and $previous) { "$previous..HEAD" } else { 'HEAD' }
$titles = @(& git -C $root log --no-merges --format='- %s' $range)
$notes = @("Modelado $Version", '')
$notes += if ($previous) { "Changes since $($previous):" } else { 'The first release:' }
$notes += ''
$notes += $titles
$notes += @('',
    'The installer is not code-signed yet, so Windows SmartScreen warns before it runs. Check it',
    'against SHA256SUMS, which the in-app updater checks as well.')
[System.IO.File]::WriteAllText((Join-Path $Out 'notes.md'), ($notes -join "`n") + "`n")

Write-Host "release $Version -> $Out"
Write-Host "  $name  sha256 $hash"
