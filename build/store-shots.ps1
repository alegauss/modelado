<#
.SYNOPSIS
    MOD79, MOD81: the Microsoft Store screenshots' window captures, taken from the real window.

.DESCRIPTION
    Starts Modelado once per shot on a model from the site's demos, sizes the window to 1680 x 1040 pixels (the
    composed screenshot is 1920 x 1080, and the guest's 150% scale keeps the interface legible when the
    window is drawn smaller in it), turns
    the model to a three-quarter view with a drag in the viewport, brings the card the caption is about
    into view through UI Automation, does what the shot shows (a Simplify and a Smooth, STL in
    millimetres), captures the window with PrintWindow and closes it. Opens a window and moves the
    mouse on the desk, so it runs on the VMware guest through store-shots-vm.ps1, which composes each
    capture under its caption afterwards.

      powershell -NoProfile -File build\store-shots.ps1 -Exe <Modelado.exe> -Demo <site\public\demo> -Out <folder>

    Writes capture-1.png .. capture-N.png in the order build\msix\store-listing.md captions them.
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

# The order of the captions in store-listing.md. Drag is the turn in viewport pixels (0.4 degree each,
# OrbitCamera.Dragged); Card is the AutomationId brought to the top of the Operations panel; Invoke
# runs those buttons in order; Stl picks STL and a longest side in millimetres.
$shots = @(
    # The model each card made, beside the card that makes it from a photo: its text says how.
    @{ Open = 'chair.glb'; Wait = 12; Drag = @(-90, 18); Card = '3D Object' },
    @{ Open = 'studio\relief-face.glb'; Wait = 10; Drag = @(-100, 20); Card = 'Relief' },
    @{ Open = 'studio\object-hamburger.glb'; Wait = 8; Drag = @(-80, 25); Card = 'Edit and improve'; Invoke = @('SimplifyButton', 'SmoothButton') },
    @{ Open = 'studio\relief-landscape.glb'; Wait = 8; Drag = @(-110, 15); Card = 'Export'; Stl = 150 }
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
public static class Shot {
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
    public static void Size(IntPtr h, int w, int hh) {
        SetProcessDPIAware();
        ShowWindow(h, 1);
        SetWindowPos(h, IntPtr.Zero, 40, 40, w, hh, 0x0040);
        SetForegroundWindow(h);
    }
    // A left-button drag in small steps, as a hand would make it, so each move reaches the viewport.
    public static void Drag(int x, int y, int dx, int dy) {
        SetCursorPos(x, y);
        Thread.Sleep(100);
        mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        for (int i = 1; i <= 20; i++) { SetCursorPos(x + dx * i / 20, y + dy * i / 20); Thread.Sleep(25); }
        mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        SetCursorPos(x + 2000, y);
    }
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out RECT r, int size);
    // The window as it is on the desk, whatever size it settled at (a minimum size and the guest's
    // DPI scaling both move it off the size asked for), cut to the frame DWM draws: GetWindowRect
    // also counts the invisible resize borders around it.
    public static void Save(IntPtr h, string path) {
        RECT outer, frame;
        GetWindowRect(h, out outer);
        if (DwmGetWindowAttribute(h, 9, out frame, Marshal.SizeOf(typeof(RECT))) != 0) frame = outer; // DWMWA_EXTENDED_FRAME_BOUNDS
        using (Bitmap b = new Bitmap(outer.Right - outer.Left, outer.Bottom - outer.Top, PixelFormat.Format32bppArgb)) {
            using (Graphics g = Graphics.FromImage(b)) {
                IntPtr dc = g.GetHdc();
                PrintWindow(h, dc, 2); // PW_RENDERFULLCONTENT, so WPF's content is drawn
                g.ReleaseHdc(dc);
            }
            Rectangle visible = new Rectangle(frame.Left - outer.Left, frame.Top - outer.Top, frame.Right - frame.Left, frame.Bottom - frame.Top);
            using (Bitmap cut = b.Clone(visible, PixelFormat.Format32bppArgb)) cut.Save(path, ImageFormat.Png);
        }
    }
}
'@ -ReferencedAssemblies System.Drawing

$UIA = [System.Windows.Automation.AutomationElement]
# By AutomationId, or by name where the element has none (a card's heading).
function Find($root, [string]$id) {
    foreach ($property in $UIA::AutomationIdProperty, $UIA::NameProperty) {
        $condition = New-Object System.Windows.Automation.PropertyCondition($property, $id)
        $found = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
        if ($found) { return $found }
    }
    throw "No element $id in the window"
}

# Scrolls the Operations panel so the element sits near its top: the panel's ScrollPattern takes a
# percentage, worked out from where the element is now and how tall the scrolled content is.
function ScrollTo($root, [string]$id) {
    $panel = Find $root 'OperationsScroll'
    $scroll = $panel.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    if (-not $scroll.Current.VerticallyScrollable) { return }
    $view = $panel.Current.BoundingRectangle
    $content = $view.Height * 100 / $scroll.Current.VerticalViewSize
    $offset = ($content - $view.Height) * $scroll.Current.VerticalScrollPercent / 100
    $target = (Find $root $id).Current.BoundingRectangle
    $wanted = $offset + ($target.Top - $view.Top) - 30
    $percent = [Math]::Max(0, [Math]::Min(100, $wanted * 100 / ($content - $view.Height)))
    $scroll.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $percent)
}

function Invoke-Button($root, [string]$id) {
    (Find $root $id).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Select-Stl($root, [int]$millimetres) {
    $combo = Find $root 'ExportFormat'
    $expand = $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    Start-Sleep -Milliseconds 400
    # By its start: the item's name carries a middle dot, which Windows PowerShell would read from this
    # file in the ANSI code page.
    $item = $combo.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
        Where-Object { $_.Current.Name -like 'STL*' } | Select-Object -First 1
    if (-not $item) { throw 'No STL item in the format list' }
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $expand.Collapse()
    Start-Sleep -Milliseconds 300
    (Find $root 'ExportSize').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("$millimetres")
}

for ($i = 0; $i -lt $shots.Count; $i++) {
    $shot = $shots[$i]
    $file = Join-Path $Demo $shot.Open
    if (-not (Test-Path $file)) { throw "No $file" }
    $p = Start-Process $Exe -ArgumentList '--open', "`"$file`"", '--provider', 'cpu', '--releases', 'off' -PassThru
    # A shot that fails must not leave the window holding the guest tree for the next run.
    try {
    $deadline = (Get-Date).AddSeconds(30)
    while ($p.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250; $p.Refresh() }
    if ($p.MainWindowHandle -eq 0) { throw "Modelado showed no window for $file" }
    [Shot]::Size($p.MainWindowHandle, 1680, 1040)
    Start-Sleep -Seconds $shot.Wait

    # The tree can refuse the window for a moment after it is shown; ask again before giving up.
    $root = $null
    foreach ($try in 1..15) {
        $p.Refresh()
        try { $root = $UIA::FromHandle($p.MainWindowHandle); break }
        catch { Write-Host "UI Automation refused the window ($try): $($_.Exception.InnerException.Message)"; Start-Sleep -Seconds 1 }
    }
    if (-not $root) { throw "UI Automation never reached the window for $file" }
    # The viewport is a Grid, which has no automation peer; the rendered image in it has.
    $viewport = (Find $root 'ViewImage').Current.BoundingRectangle
    [Shot]::Drag([int]($viewport.Left + $viewport.Width / 2), [int]($viewport.Top + $viewport.Height / 2), $shot.Drag[0], $shot.Drag[1])
    foreach ($id in @($shot.Invoke | Where-Object { $_ })) { Invoke-Button $root $id; Start-Sleep -Seconds 3 }
    if ($shot.Stl) { Select-Stl $root $shot.Stl }
    ScrollTo $root $shot.Card
    Start-Sleep -Seconds 2

    $png = Join-Path $Out "capture-$($i + 1).png"
    [Shot]::Save($p.MainWindowHandle, $png)
    Write-Host "wrote $png ($($shot.Open))"
    }
    finally {
        # A model changed by Simplify asks before closing; the shot is not kept.
        $p.CloseMainWindow() | Out-Null
        if (-not $p.WaitForExit(5000)) { $p.Kill() }
    }
}
