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
param(
    [string]$Layout = (Join-Path $PSScriptRoot 'layout'),
    # MOD78: also check the association and the CLI alias, and run cases-packaged against the
    # registered package (tests\Modelado.Cases with MODELADO_PACKAGED=1), from a built checkout.
    [switch]$Cases
)

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

if ($Cases) {
    # MOD75: the package's ProgID beside each type in Open with, naming this application.
    foreach ($type in '.glb', '.obj', '.ply', '.stl') {
        $progIds = @((Get-Item "HKCU:\Software\Classes\$type\OpenWithProgids" -ErrorAction SilentlyContinue).Property | Where-Object { $_ -like 'AppX*' })
        $ours = $progIds | Where-Object { (Get-ItemProperty "HKCU:\Software\Classes\$_\Application" -ErrorAction SilentlyContinue).AppUserModelID -eq $aumid }
        Check "Open with lists the package for $type" ($null -ne $ours)
    }

    # MOD76: both aliases, and the CLI's answering from a shell that is not the package's.
    $aliases = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps'
    Check 'Modelado.exe has an alias on PATH' (Test-Path (Join-Path $aliases 'Modelado.exe'))
    $help = & cmd /c "`"$(Join-Path $aliases 'modelado-cli.exe')`" --help" 2>&1 | Out-String
    Check 'modelado-cli --help answers through its alias, on the console' ($LASTEXITCODE -eq 0 -and $help -match 'modelado-cli')

    $repo = Split-Path -Parent $PSScriptRoot
    $env:MODELADO_PACKAGED = '1'
    try {
        # The test app itself, as run-tests.cmd and run-live.cmd start it: dotnet test refuses
        # Microsoft.Testing.Platform's runner on the .NET 10 SDK.
        $runner = Join-Path $repo 'tests\Modelado.Cases\bin\Debug\net10.0-windows\Modelado.Cases.exe'
        if (-not (Test-Path $runner)) { throw "No $runner (dotnet build Modelado.slnx first)" }
        & $runner
        Check 'cases-packaged pass against the registered package' ($LASTEXITCODE -eq 0)
    }
    finally {
        Remove-Item Env:\MODELADO_PACKAGED
    }
}

Remove-AppxPackage $package.PackageFullName
Check 'the package is removed again' ($null -eq (Get-AppxPackage -Name 'Alegauss.Modelado'))

Write-Host "$failures failure(s)"
exit $failures
