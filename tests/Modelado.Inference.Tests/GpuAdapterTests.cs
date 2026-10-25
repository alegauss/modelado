using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>MOD52: the provider chip names the GPU DirectML runs on, and still reads CPU on the CPU.</summary>
public sealed class GpuAdapterTests
{
    [Fact]
    public void The_chip_names_the_GPU_and_its_video_memory()
    {
        GpuAdapter rtx = new("NVIDIA GeForce RTX 4060", 8L << 30);

        Assert.Equal("DirectML · NVIDIA GeForce RTX 4060, 8 GB", ExecutionProviders.Label(ExecutionProvider.DirectML, rtx));
    }

    [Fact]
    public void The_chip_reads_CPU_on_the_CPU_whatever_the_GPU()
    {
        Assert.Equal("CPU", ExecutionProviders.Label(ExecutionProvider.Cpu, new GpuAdapter("NVIDIA GeForce RTX 4060", 8L << 30)));
        Assert.Equal("CPU", ExecutionProviders.Label(ExecutionProvider.Cpu, null));
    }

    [Fact]
    public void A_GPU_that_cannot_be_named_leaves_DirectML_alone()
    {
        Assert.Equal("DirectML", ExecutionProviders.Label(ExecutionProvider.DirectML, null));
    }

    [Theory]
    [InlineData(8L << 30, "8 GB")]
    [InlineData((6L << 30) + (900L << 20), "7 GB")]
    [InlineData(512L << 20, "512 MB")]
    public void Video_memory_is_whole_gigabytes_or_megabytes_under_one(long bytes, string expected)
    {
        Assert.Equal(expected, GpuAdapter.Memory(bytes));
    }

    [Fact]
    public void Reading_the_adapter_never_throws_where_there_is_none_to_name()
    {
        // The guest's Basic Render Driver, a desk with a GPU and Linux CI each answer without throwing.
        GpuAdapter? adapter = GpuAdapter.ForDevice();

        if (adapter is not null)
        {
            Assert.False(string.IsNullOrWhiteSpace(adapter.Name));
            Assert.True(adapter.VideoMemory > 0);
        }

        Assert.Null(GpuAdapter.ForDevice(-1));
    }
}
