<#
.SYNOPSIS
    MOD28: installs, upgrades, opens a model with and uninstalls Modelado on the desk it runs on.

.DESCRIPTION
    Run on the VMware guest by check-installer-vm.ps1, never on the desk you work at: it installs
    into the current user's profile and opens a window. From the folder it is staged in, which holds
    an older installer and the current one (Modelado-Setup-*.exe) and cube.obj:

    1. the older build installs silently, per user: the app, modelado-cli, the notices, the Start
       menu entry and "Open with" for .glb, .obj, .ply and .stl;
    2. a downloaded model is put where the app downloads them, and the current build upgrades over
       the older one in place: one install, the new version, the model kept;
    3. modelado-cli runs from the install, on its own runtime;
    4. cube.obj opens through the command "Open with Modelado" runs, and the window names it;
    5. the silent uninstall removes the app, its Start menu entry and its "Open with" entries, and
       keeps the downloaded models, the answer a silent uninstall gives to the question it asks.

    Exits 0 when every check held, 1 with the first one that did not.
#>
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$app = Join-Path $env:LOCALAPPDATA 'Programs\Modelado'
$models = Join-Path $env:LOCALAPPDATA 'Modelado\models'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B100F130-2CAB-40FB-A480-987B374E225D}_is1'
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Modelado\Modelado.lnk'
$silent = '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER'

function Check([string]$What, [bool]$Held) {
    if (-not $Held) { Write-Host "FAILED  $What"; exit 1 }
    Write-Host "ok      $What"
}

function Install([string]$Installer) {
    $p = Start-Process -FilePath $Installer -ArgumentList $silent -Wait -PassThru
    Check "$(Split-Path -Leaf $Installer) exits 0" ($p.ExitCode -eq 0)
}

function Uninstall {
    $p = Start-Process -FilePath (Join-Path $app 'unins000.exe') -ArgumentList $silent -Wait -PassThru
    # The uninstaller hands over to a copy of itself and returns; the folder going is its end.
    for ($i = 0; $i -lt 60 -and (Test-Path (Join-Path $app 'Modelado.exe')); $i++) { Start-Sleep -Seconds 1 }
    return $p.ExitCode
}

function OpenWith([string]$Extension) {
    (Get-ItemProperty "HKCU:\Software\Classes\$Extension\OpenWithProgids" -ErrorAction SilentlyContinue).'Modelado.Model' -ne $null
}

$installers = @(Get-ChildItem $here -Filter 'Modelado-Setup-*.exe' | Sort-Object { [version]($_.BaseName -replace '^Modelado-Setup-', '') })
Check 'an older and a current installer are staged' ($installers.Count -eq 2)
$older, $current = $installers
$currentVersion = $current.BaseName -replace '^Modelado-Setup-', ''

# A run that stopped half way leaves an install behind; start from none.
if (Test-Path (Join-Path $app 'unins000.exe')) { Uninstall | Out-Null }
if (Test-Path $models) { Remove-Item -Recurse -Force $models }

# 1. The older build.
Install $older.FullName
Check 'Modelado.exe is installed per user' (Test-Path (Join-Path $app 'Modelado.exe'))
Check 'modelado-cli.exe beside it' (Test-Path (Join-Path $app 'modelado-cli.exe'))
Check 'with the third-party notices' (Test-Path (Join-Path $app 'licenses\README.txt'))
Check 'the Start menu has an entry' (Test-Path $startMenu)
foreach ($extension in '.glb', '.obj', '.ply', '.stl') { Check "Open with Modelado is offered for $extension" (OpenWith $extension) }
Check 'no model is bundled' (-not (Test-Path (Join-Path $app 'models')))

# 2. The upgrade, with a downloaded model in place.
$kept = Join-Path $models 'kept-model\meta.json'
New-Item -ItemType Directory -Force (Split-Path $kept) | Out-Null
Set-Content -Path $kept -Value '{}' -Encoding ascii
Install $current.FullName
Check "the install is now $currentVersion" ((Get-ItemProperty $uninstallKey).DisplayVersion -eq $currentVersion)
Check 'one install, upgraded in place' (@(Get-ChildItem 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall' | Where-Object { (Get-ItemProperty $_.PSPath).DisplayName -like 'Modelado*' }).Count -eq 1)
Check 'the downloaded model is kept' (Test-Path $kept)

# 3. The CLI on the installed runtime.
$cli = Start-Process -FilePath (Join-Path $app 'modelado-cli.exe') -ArgumentList '--help' -Wait -PassThru -NoNewWindow
Check 'modelado-cli --help runs from the install' ($cli.ExitCode -eq 0)

# 4. A model opened the way Explorer's Open with opens it: the ProgID's own command line.
$command = (Get-ItemProperty 'HKCU:\Software\Classes\Modelado.Model\shell\open\command').'(default)'
Check 'the open command names the installed exe' ($command -eq ('"{0}" "%1"' -f (Join-Path $app 'Modelado.exe')))
$cube = Join-Path $here 'cube.obj'
$window = Start-Process -FilePath (Join-Path $app 'Modelado.exe') -ArgumentList ('"{0}"' -f $cube) -PassThru
# The title is "cube.obj · Modelado"; the dot by its code point, since Windows PowerShell reads
# this file as ANSI.
$expected = 'cube.obj {0} Modelado' -f [char]0xB7
$title = ''
for ($i = 0; $i -lt 30 -and $title -ne $expected; $i++) {
    Start-Sleep -Seconds 1
    $window.Refresh()
    $title = $window.MainWindowTitle
}
Check "cube.obj opens in the window (its title read '$title')" ($title -eq $expected)
$window.CloseMainWindow() | Out-Null
if (-not $window.WaitForExit(15000)) { $window.Kill() }

# 5. The uninstall.
$code = Uninstall
Check 'the uninstaller exits 0' ($code -eq 0)
Check 'the app is gone' (-not (Test-Path (Join-Path $app 'Modelado.exe')))
Check 'the Start menu entry is gone' (-not (Test-Path $startMenu))
Check 'the uninstall entry is gone' (-not (Test-Path $uninstallKey))
foreach ($extension in '.glb', '.obj', '.ply', '.stl') { Check "Open with Modelado is withdrawn for $extension" (-not (OpenWith $extension)) }
Check 'the Modelado.Model type is gone' (-not (Test-Path 'HKCU:\Software\Classes\Modelado.Model'))
Check 'the downloaded models are kept by a silent uninstall' (Test-Path $kept)

Remove-Item -Recurse -Force $models
Write-Host 'installer check passed'
exit 0
