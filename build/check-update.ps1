<#
.SYNOPSIS
    MOD31: an installed older build updates itself to a newer release and relaunches, and refuses
    an installer that does not match SHA256SUMS.

.DESCRIPTION
    Run on the VMware guest after check-installer.ps1, from the same staged folder, never on the
    desk you work at: it installs, opens windows and clicks in them. The folder holds the older
    Modelado-Setup-0.0.1.exe and, under update\, Modelado-Setup-99.0.0.exe: both compiled from the
    current publish, so the app inside reports the build's version and a release tagged v99.0.0 is
    newer than it. That release is served on 127.0.0.1:47640 by Python's http.server, standing in
    for GitHub, and the installed app is pointed at it with --releases.

    1. The older build is installed; the app offers 99.0.0; Update and restart is pressed through
       UI Automation. The app exits, the installer upgrades in place (the uninstall entry now reads
       99.0.0), and Modelado starts again from the install.
    2. The older build is installed again and SHA256SUMS is made to name another digest; the same
       click is refused, the app says nothing was installed, and the older version stays.

    The windows are driven through UI Automation here rather than in a winwright case, since the
    app under test is the installed one and it exits mid-case (WW575).
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$here = $PSScriptRoot
$app = Join-Path $env:LOCALAPPDATA 'Programs\Modelado'
$exe = Join-Path $app 'Modelado.exe'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{B100F130-2CAB-40FB-A480-987B374E225D}_is1'
$silent = '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER'
$port = 47640

function Check([string]$What, [bool]$Held) {
    if (-not $Held) { Write-Host "FAILED  $What"; Cleanup; exit 1 }
    Write-Host "ok      $What"
}

function Installed { (Get-ItemProperty $uninstallKey -ErrorAction SilentlyContinue).DisplayVersion }

function Install([string]$Installer) {
    Get-Process Modelado -ErrorAction SilentlyContinue | Stop-Process -Force
    $p = Start-Process -FilePath $Installer -ArgumentList $silent -Wait -PassThru
    if ($p.ExitCode -ne 0) { Check "$(Split-Path -Leaf $Installer) exits 0" $false }
}

function Element($Process, [string]$Id, [int]$Seconds = 30) {
    $byPid = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $Process.Id)
    $byId = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
    for ($i = 0; $i -lt $Seconds * 2; $i++) {
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $byPid)
        if ($window) {
            $found = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId)
            if ($found -and -not $found.Current.IsOffscreen -and $found.Current.IsEnabled) { return $found }
        }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function Press($Element) {
    $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Feed([string]$Digest) {
    $installer = Get-Item (Join-Path $feed $current.Name)
    Set-Content -Path (Join-Path $feed 'SHA256SUMS') -Value "$Digest  $($current.Name)" -Encoding ascii -NoNewline
    $release = @{
        tag_name = "v$currentVersion"
        html_url = "http://127.0.0.1:$port/release.json"
        assets = @(
            @{ name = $current.Name; size = $installer.Length; browser_download_url = "http://127.0.0.1:$port/$($current.Name)" },
            @{ name = 'SHA256SUMS'; size = 100; browser_download_url = "http://127.0.0.1:$port/SHA256SUMS" })
    }
    Set-Content -Path (Join-Path $feed 'release.json') -Value ($release | ConvertTo-Json -Depth 4) -Encoding ascii
}

function Launch { Start-Process -FilePath $exe -ArgumentList '--releases', "http://127.0.0.1:$port/release.json", '--provider', 'cpu' -PassThru }

function Cleanup {
    Get-Process Modelado -ErrorAction SilentlyContinue | Stop-Process -Force
    if ($server -and -not $server.HasExited) { $server.Kill() }
    if (Test-Path (Join-Path $app 'unins000.exe')) { Start-Process (Join-Path $app 'unins000.exe') -ArgumentList $silent -Wait | Out-Null }
    Remove-Item -Recurse -Force (Join-Path $env:TEMP 'Modelado-update') -ErrorAction SilentlyContinue
}

$older = Get-Item (Join-Path $here 'Modelado-Setup-0.0.1.exe')
$current = Get-Item (Join-Path $here 'update\Modelado-Setup-99.0.0.exe')
$currentVersion = '99.0.0'
$olderVersion = '0.0.1'

$feed = Join-Path $env:TEMP 'modelado-release-feed'
Remove-Item -Recurse -Force $feed -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $feed | Out-Null
Copy-Item $current.FullName $feed
$digest = (Get-FileHash -Algorithm SHA256 $current.FullName).Hash.ToLowerInvariant()
Remove-Item -Recurse -Force (Join-Path $env:TEMP 'Modelado-update') -ErrorAction SilentlyContinue

$python = (Get-Command python -ErrorAction SilentlyContinue).Source
Check 'Python serves the release' ([bool]$python)
$server = Start-Process -FilePath $python -ArgumentList '-m', 'http.server', $port, '--bind', '127.0.0.1', '--directory', $feed -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2

# 1. The update.
Feed $digest
Install $older.FullName
Check "$olderVersion is installed" ((Installed) -eq $olderVersion)
$window = Launch
$chip = Element $window 'UpdateChip'
Check "the installed $olderVersion offers $currentVersion" ($chip -and $chip.Current.Name -eq "Update $currentVersion available")
Press $chip
$install = Element $window 'AboutInstallUpdate'
Check 'About offers Update and restart' ([bool]$install)
$clicked = Get-Date
Press $install
Check 'the app exits to let the installer run' ($window.WaitForExit(120000))
$relaunched = $null
for ($i = 0; $i -lt 120 -and -not ($relaunched -and (Installed) -eq $currentVersion); $i++) {
    Start-Sleep -Seconds 1
    $relaunched = Get-Process Modelado -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe -and $_.StartTime -gt $clicked } | Select-Object -First 1
}
Check "the install is now $currentVersion, upgraded in place" ((Installed) -eq $currentVersion)
Check 'Modelado started again from the install after the update' ([bool]$relaunched)
$relaunched | Stop-Process -Force

# 2. A tampered installer: SHA256SUMS names another digest.
Remove-Item -Recurse -Force (Join-Path $env:TEMP 'Modelado-update') -ErrorAction SilentlyContinue
Feed ('0' * 64)
Install $older.FullName
Check "$olderVersion is installed again" ((Installed) -eq $olderVersion)
$window = Launch
$chip = Element $window 'UpdateChip'
Check 'the newer release is offered again' ([bool]$chip)
Press $chip
Press (Element $window 'AboutInstallUpdate')
$said = ''
for ($i = 0; $i -lt 120 -and $said -notmatch 'Nothing was installed'; $i++) {
    Start-Sleep -Seconds 1
    $status = Element $window 'AboutUpdateStatus' 1
    if ($status) { $said = $status.Current.Name }
}
Check "the mismatched installer is refused (About read '$said')" ($said -match 'Nothing was installed\.$')
Check 'the app is still running' (-not $window.HasExited)
Check "and $olderVersion is still installed" ((Installed) -eq $olderVersion)
Check 'the refused installer was deleted' (-not (Test-Path (Join-Path $env:TEMP "Modelado-update\$($current.Name)")))

Cleanup
Remove-Item -Recurse -Force $feed
Write-Host 'update check passed'
exit 0
