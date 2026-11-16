using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Modelado.Inference;

/// <summary>The pipelines whose memory <see cref="MemoryCheck"/> knows.</summary>
public enum Pipeline
{
    /// <summary>A whole object with TripoSR.</summary>
    Object,

    /// <summary>A relief from the photo's depth.</summary>
    Relief,
}

/// <summary>What a run needs against what is free, in megabytes, and whether it fits.</summary>
/// <param name="NeededMb">The RAM the run peaks at, as measured (MOD118).</param>
/// <param name="FreeMb">What this process holds now plus what Windows has free.</param>
public sealed record MemoryFit(long NeededMb, long FreeMb)
{
    /// <summary>Whether the run fits.</summary>
    public bool Fits => FreeMb >= NeededMb;
}

/// <summary>
/// The memory check before a run (MOD119): the RAM a pipeline peaks at, from MOD118's measurements
/// (the README's peak memory table, rounded up), against the RAM this process already holds, which
/// covers sessions loaded by an earlier run, plus what Windows reports as free. A PC short of it is
/// told before a long run rather than failing in the middle of one. Video memory is not checked: a
/// model DirectML cannot fit is run again on the CPU (<see cref="ModelRunner.Notice"/>), which the
/// RAM figure for the CPU path then covers. MODELADO_AVAILABLE_MB stands in for what Windows reports,
/// so a test can force a low limit.
/// </summary>
public static class MemoryCheck
{
    /// <summary>The RAM a run peaks at, in megabytes, from the measured table.</summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <param name="provider">Where it runs.</param>
    /// <param name="high">High quality (3D Object's full-precision encoder on a GPU).</param>
    /// <returns>Megabytes.</returns>
    public static long NeededMb(Pipeline pipeline, ExecutionProvider provider, bool high) => (pipeline, provider, high) switch
    {
        (Pipeline.Object, ExecutionProvider.Cpu, false) => 4400,
        (Pipeline.Object, ExecutionProvider.Cpu, true) => 4700,
        (Pipeline.Object, _, false) => 2000,
        (Pipeline.Object, _, true) => 4800,
        _ => 900,
    };

    /// <summary>Whether a run fits in the memory free now.</summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <param name="provider">Where it runs.</param>
    /// <param name="high">High quality.</param>
    /// <returns>The figures, and whether they fit.</returns>
    public static MemoryFit Check(Pipeline pipeline, ExecutionProvider provider, bool high)
    {
        using Process self = Process.GetCurrentProcess();
        return new MemoryFit(NeededMb(pipeline, provider, high), (self.WorkingSet64 >> 20) + AvailableMb());
    }

    /// <summary>The physical memory Windows has free, in megabytes, or MODELADO_AVAILABLE_MB when set.</summary>
    /// <returns>Megabytes; a very large number where it cannot be read, so nothing is refused for want of a figure.</returns>
    public static long AvailableMb()
    {
        if (Environment.GetEnvironmentVariable("MODELADO_AVAILABLE_MB") is { Length: > 0 } forced && long.TryParse(forced, out long mb))
        {
            return mb;
        }

        MemoryStatus status = new() { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return OperatingSystem.IsWindows() && GlobalMemoryStatusEx(ref status) ? (long)(status.AvailablePhysical >> 20) : long.MaxValue / 2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
