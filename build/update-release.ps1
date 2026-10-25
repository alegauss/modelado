# MOD29: sets Modelado's version in the two places that carry it, after PhotoBoost's
# update-release.ps1. A test keeps them equal (tests\Modelado.Architecture.Tests\VersionTests.cs):
#   - Directory.Build.props             -> <Version>x.y.z</Version>
#   - src\Modelado.App\app.manifest     -> assemblyIdentity version="x.y.z.0"
# The installer reads the version from the published exe, so there is no third place.
#
#   powershell -File build\update-release.ps1 -Version 0.2.0
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "Invalid version: '$Version'. Use the form x.y.z (e.g. 0.2.0)."
}

# Always UTF-8 without a BOM: in Windows PowerShell 5.1, Get-Content uses the ANSI codepage by default.
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Read-Utf8([string]$path) { [System.IO.File]::ReadAllText($path, $utf8) }
function Write-Utf8([string]$path, [string]$text) { [System.IO.File]::WriteAllText($path, $text, $utf8) }

$propsPath = Join-Path $root 'Directory.Build.props'
$props = Read-Utf8 $propsPath
if ($props -notmatch '<Version>\s*([0-9]+\.[0-9]+\.[0-9]+)\s*</Version>') {
    throw 'Could not find <Version> in Directory.Build.props'
}
$old = $Matches[1]
Write-Utf8 $propsPath ($props -replace '<Version>\s*[0-9]+\.[0-9]+\.[0-9]+\s*</Version>', "<Version>$Version</Version>")
Write-Host "Directory.Build.props: $old -> $Version"

$manifestPath = Join-Path $root 'src\Modelado.App\app.manifest'
$manifest = Read-Utf8 $manifestPath
$pattern = '(<assemblyIdentity\s+version=")[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+(")'
if ($manifest -notmatch $pattern) {
    throw 'Could not find the assemblyIdentity version in app.manifest'
}
Write-Utf8 $manifestPath ($manifest -replace $pattern, "`${1}$Version.0`${2}")
Write-Host "app.manifest: assemblyIdentity $Version.0"
