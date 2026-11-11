<#
.SYNOPSIS
    MOD73: on the VMware guest, registers the MSIX layout build-msix.ps1 made, starts Modelado the way
    its Start menu entry does, and removes the package again.

.DESCRIPTION
    Run by check-msix-vm.ps1 from the tree it stages, with the layout in .\layout. Registering an
    unsigned layout needs Developer Mode, which this turns on where the guest's user may; a run
    that is not elevated may not, and WW618 asks winwright to prepare the guest for it. Each step
    prints ok or FAIL; the exit code is the number of failures.
#>
[CmdletBinding()]
# The layout to register: .\layout where check-msix-vm.ps1 staged it, or artifacts\msix\layout when run
# on a desk that already has Developer Mode (the host, by the user's choice of 11/11/2026).
param([string]$Layout = (Join-Path $PSScriptRoot 'layout'))

$ErrorActionPreference = 'Stop'
$failures = 0
function Check([string]$what, [bool]$ok) {
    if ($ok) { Write-Host "ok   $what" } else { Write-Host "FAIL $what"; $script:failures++ }
}

$manifest = Join-Path $Layout 'AppxManifest.xml'
$unlock = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
$devMode = (Get-ItemProperty $unlock -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense -eq 1
if (-not $devMode) {
    try {
        New-Item $unlock -Force -ErrorAction SilentlyContinue | Out-Null
        Set-ItemProperty $unlock -Name AllowDevelopmentWithoutDevLicense -Value 1 -Type DWord
    } catch {
        Write-Host "Developer Mode could not be turned on here: $($_.Exception.Message)"
    }
}

Get-AppxPackage -Name 'Alegauss.Modelado' | Remove-AppxPackage
Add-AppxPackage -Register $manifest
$package = Get-AppxPackage -Name 'Alegauss.Modelado'
Check 'the layout registers as Alegauss.Modelado' ($null -ne $package)
if (-not $package) { exit $failures }
Check "with the reserved family name ($($package.PackageFamilyName))" ($package.PackageFamilyName -eq 'Alegauss.Modelado_d7cpypw1jc0bm')

# The Start menu entry is the application's user model id under shell:AppsFolder.
$aumid = "$($package.PackageFamilyName)!Modelado"
Check 'the Start menu lists it' ($null -ne (Get-StartApps | Where-Object AppID -eq $aumid))
Start-Process explorer.exe "shell:AppsFolder\$aumid"

$install = $package.InstallLocation
$window = $null
foreach ($i in 1..90) {
    Start-Sleep -Seconds 1
    $window = Get-Process Modelado -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 -and $_.Path -like "$install*" } | Select-Object -First 1
    if ($window) { break }
}
Check 'Modelado starts from it, from the package folder, with a window' ($null -ne $window)
if ($window) { Check "the window is titled Modelado ($($window.MainWindowTitle))" ($window.MainWindowTitle -like '*Modelado*') }

Get-Process Modelado -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$install*" } | Stop-Process -Force
Start-Sleep -Seconds 2
Remove-AppxPackage $package.PackageFullName
Check 'the package is removed again' ($null -eq (Get-AppxPackage -Name 'Alegauss.Modelado'))

Write-Host "$failures failure(s)"
exit $failures
