<#
.SYNOPSIS
    MOD79: the Microsoft Store screenshots, taken from the real window.

.DESCRIPTION
    Starts Modelado once per shot on a photo or a model from the site's demos, sizes the window to
    1920 x 1080 (the Store refuses a desktop screenshot under 1366 x 768), waits for it to settle,
    captures it with PrintWindow and closes it. Opens a window on the desk, so it runs on the VMware
    guest through store-shots-vm.ps1, not on the desk you work at.

      powershell -NoProfile -File build\store-shots.ps1 -Exe <Modelado.exe> -Demo <site\public\demo> -Out <folder>

    Writes screenshot-1.png .. screenshot-N.png in the order build\msix\store-listing.md captions them.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Exe,
    [Parameter(Mandatory)] [string]$Demo,
    [Parameter(Mandatory)] [string]$Out
)
$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
$Demo = (Resolve-Path $Demo).Path
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path

# The order of the captions in store-listing.md.
$shots = @(
    @{ Open = 'chair.png'; Wait = 6 },
    # Not chair.glb: on the guest its window was still blank when captured, twice the others' wait.
    @{ Open = 'studio\relief-face.glb'; Wait = 10 },
    @{ Open = 'studio\relief-landscape.glb'; Wait = 8 },
    @{ Open = 'studio\object-hamburger.glb'; Wait = 8 }
)

Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class Shot {
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    public static void Size(IntPtr h, int w, int hh) {
        SetProcessDPIAware();
        ShowWindow(h, 1);
        SetWindowPos(h, IntPtr.Zero, 0, 0, w, hh, 0x0040);
    }
    public static void Save(IntPtr h, int w, int hh, string path) {
        using (Bitmap b = new Bitmap(w, hh, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(b)) {
            IntPtr dc = g.GetHdc();
            PrintWindow(h, dc, 2); // PW_RENDERFULLCONTENT, so WPF's content is drawn
            g.ReleaseHdc(dc);
            b.Save(path, ImageFormat.Png);
        }
    }
}
'@ -ReferencedAssemblies System.Drawing

New-Item -ItemType Directory -Force $Out | Out-Null
for ($i = 0; $i -lt $shots.Count; $i++) {
    $file = Join-Path $Demo $shots[$i].Open
    if (-not (Test-Path $file)) { throw "No $file" }
    $p = Start-Process $Exe -ArgumentList '--open', "`"$file`"", '--provider', 'cpu', '--releases', 'off' -PassThru
    $deadline = (Get-Date).AddSeconds(30)
    while ($p.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250; $p.Refresh() }
    if ($p.MainWindowHandle -eq 0) { throw "Modelado showed no window for $file" }
    [Shot]::Size($p.MainWindowHandle, 1920, 1080)
    Start-Sleep -Seconds $shots[$i].Wait
    $png = Join-Path $Out "screenshot-$($i + 1).png"
    [Shot]::Save($p.MainWindowHandle, 1920, 1080, $png)
    Write-Host "wrote $png ($($shots[$i].Open))"
    $p.CloseMainWindow() | Out-Null
    if (-not $p.WaitForExit(10000)) { $p.Kill() }
}
