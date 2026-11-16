using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>MOD119: a run is checked against free memory, from MOD118's measured peaks.</summary>
public sealed class MemoryCheckTests
{
    [Fact]
    public void The_needs_follow_the_measured_table_heaviest_on_the_cpu_and_at_high()
    {
        Assert.True(MemoryCheck.NeededMb(Pipeline.Object, ExecutionProvider.Cpu, false) > MemoryCheck.NeededMb(Pipeline.Object, ExecutionProvider.DirectML, false));
        Assert.True(MemoryCheck.NeededMb(Pipeline.Object, ExecutionProvider.DirectML, true) > MemoryCheck.NeededMb(Pipeline.Object, ExecutionProvider.DirectML, false));
        Assert.True(MemoryCheck.NeededMb(Pipeline.Relief, ExecutionProvider.Cpu, false) < 1024);

        // At or above the README's measured peaks (4.2 GB on the CPU, 1.8 GB on a GPU).
        Assert.InRange(MemoryCheck.NeededMb(Pipeline.Object, ExecutionProvider.Cpu, false), 4300, 5000);
        Assert.InRange(MemoryCheck.NeededMb(Pipeline.Object, ExecutionProvider.DirectML, false), 1850, 2500);
    }

    [Fact]
    public void A_forced_low_limit_stops_a_3d_object_and_the_figures_say_why()
    {
        string? before = Environment.GetEnvironmentVariable("MODELADO_AVAILABLE_MB");
        try
        {
            Environment.SetEnvironmentVariable("MODELADO_AVAILABLE_MB", "0");
            Assert.Equal(0, MemoryCheck.AvailableMb());
            MemoryFit fit = MemoryCheck.Check(Pipeline.Object, ExecutionProvider.Cpu, high: false);
            Assert.False(fit.Fits);
            Assert.Equal(4400, fit.NeededMb);

            Environment.SetEnvironmentVariable("MODELADO_AVAILABLE_MB", "100000");
            Assert.True(MemoryCheck.Check(Pipeline.Object, ExecutionProvider.Cpu, high: true).Fits);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MODELADO_AVAILABLE_MB", before);
        }
    }

    [Fact]
    public void Windows_reports_some_free_memory()
    {
        Assert.True(Environment.GetEnvironmentVariable("MODELADO_AVAILABLE_MB") is not null || MemoryCheck.AvailableMb() > 0);
    }
}
