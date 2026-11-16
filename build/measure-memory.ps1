# MOD118: measures the memory each pipeline takes at its default and its highest setting, on the
# CPU and through DirectML, so the README's requirements are figures someone measured.
#
# Each run is modelado-cli on one photo. Its peak working set comes from the CLI itself (it reports
# peakWorkingSetMb in --json); its GPU memory is sampled every 250 ms from Windows' "GPU Process
# Memory" counters for its process id while it runs: dedicated (video memory) and shared (system
# memory the GPU maps). Run it on a PC with a DirectX 12 GPU and the models downloaded; DirectML
# runs are skipped where the CLI falls back to the CPU.
#
# Usage:  build\measure-memory.ps1 [-Photo docs\robot.png] [-Configuration Release]

param(
    [string]$Photo = "docs\robot.png",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
dotnet build tools\Modelado.Cli\Modelado.Cli.csproj --configuration $Configuration --nologo -v quiet | Out-Null
$cli = Join-Path $root "tools\Modelado.Cli\bin\$Configuration\net10.0\modelado-cli.exe"
$out = Join-Path ([IO.Path]::GetTempPath()) "modelado-measure"
New-Item -ItemType Directory -Force $out | Out-Null

$runs = @(
    @{ Name = "3D Object, grid 256"; Args = @("object", $Photo, "-o", "$out\object.glb") },
    @{ Name = "3D Object, High, grid 384"; Args = @("object", $Photo, "-o", "$out\object-high.glb", "--high", "--resolution", "384") },
    @{ Name = "Relief, grid 256"; Args = @("relief", $Photo, "-o", "$out\relief.glb") },
    @{ Name = "Relief, Object only, grid 512"; Args = @("relief", $Photo, "-o", "$out\relief-512.glb", "--grid", "512", "--remove-background") }
)

function Measure-Run([string[]]$arguments) {
    $start = New-Object Diagnostics.ProcessStartInfo $cli
    # Windows PowerShell's .NET Framework has no ArgumentList: each argument is quoted into one line.
    $start.Arguments = (($arguments + @("--json")) | ForEach-Object { '"' + ($_ -replace '"', '\"') + '"' }) -join " "
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $dedicated = 0L; $shared = 0L
    while (-not $process.HasExited) {
        try {
            $samples = (Get-Counter -Counter "\GPU Process Memory(pid_$($process.Id)*)\Dedicated Usage", "\GPU Process Memory(pid_$($process.Id)*)\Shared Usage" -ErrorAction Stop).CounterSamples
            $d = ($samples | Where-Object Path -like "*dedicated usage" | Measure-Object CookedValue -Sum).Sum
            $s = ($samples | Where-Object Path -like "*shared usage" | Measure-Object CookedValue -Sum).Sum
            if ($d -gt $dedicated) { $dedicated = [long]$d }
            if ($s -gt $shared) { $shared = [long]$s }
        } catch {
            # The process has no GPU counters yet, or has just exited.
        }
        Start-Sleep -Milliseconds 250
    }

    $report = $stdout.Result | ConvertFrom-Json
    [pscustomobject]@{
        ExitCode = $process.ExitCode
        Seconds = $report.seconds
        PeakRamMb = $report.peakWorkingSetMb
        GpuDedicatedMb = [math]::Round($dedicated / 1MB)
        GpuSharedMb = [math]::Round($shared / 1MB)
        Warnings = ($report.warnings -join "; ")
        Errors = $stderr.Result
    }
}

$rows = foreach ($provider in "cpu", "dml") {
    foreach ($run in $runs) {
        $m = Measure-Run ($run.Args + @("--provider", $provider))
        if ($m.ExitCode -ne 0) { throw "$($run.Name) on $provider failed: $($m.Errors)" }
        [pscustomobject]@{
            Operation = $run.Name
            Provider = if ($provider -eq "cpu") { "CPU" } elseif ($m.Warnings) { "DirectML (fell back: $($m.Warnings))" } else { "DirectML" }
            Seconds = $m.Seconds
            "Peak RAM (MB)" = $m.PeakRamMb
            "GPU dedicated (MB)" = $m.GpuDedicatedMb
            "GPU shared (MB)" = $m.GpuSharedMb
        }
    }
}

$rows | Format-Table -AutoSize
$rows | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $out "memory.json")
"written to $(Join-Path $out 'memory.json')"
