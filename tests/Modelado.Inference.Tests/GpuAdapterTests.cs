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

    // MOD147: the figures MOD121 read on an i7-14700 with no other GPU, and a hybrid laptop's.
    private static readonly GpuAdapter Uhd770 = new("Intel(R) UHD Graphics 770", 2L << 30, 0, Integrated: true);
    private static readonly GpuAdapter Rtx4060 = new("NVIDIA GeForce RTX 4060 Laptop GPU", 8L << 30, 1, Integrated: false);

    [Fact]
    public void Automatic_with_only_an_integrated_GPU_runs_on_the_CPU_and_says_why()
    {
        Assert.SkipUnless(ExecutionProviders.DirectMLAvailable, "this build has no DirectML");
        ProviderPick pick = ExecutionProviders.Pick(ProviderChoice.Auto, [Uhd770]);

        Assert.Equal(new ProviderPick(ExecutionProvider.Cpu, 0, null, ProviderReason.IntegratedOnly), pick);
        Assert.Contains("integrated GPU is slower", pick.Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectML_chosen_still_forces_the_integrated_GPU()
    {
        Assert.SkipUnless(ExecutionProviders.DirectMLAvailable, "this build has no DirectML");

        Assert.Equal(new ProviderPick(ExecutionProvider.DirectML, 0, Uhd770, ProviderReason.None), ExecutionProviders.Pick(ProviderChoice.DirectML, [Uhd770]));
    }

    [Fact]
    public void A_hybrid_laptop_runs_on_its_discrete_GPU_though_DXGI_lists_the_integrated_one_first()
    {
        Assert.SkipUnless(ExecutionProviders.DirectMLAvailable, "this build has no DirectML");
        ProviderPick pick = ExecutionProviders.Pick(ProviderChoice.Auto, [Uhd770, Rtx4060]);

        Assert.Equal(new ProviderPick(ExecutionProvider.DirectML, 1, Rtx4060, ProviderReason.None), pick);
        Assert.Equal("DirectML · NVIDIA GeForce RTX 4060 Laptop GPU, 8 GB", ExecutionProviders.Label(pick.Provider, pick.Adapter));
    }

    [Fact]
    public void An_adapter_DXCore_cannot_place_and_a_desk_with_none_keep_DirectML_as_before()
    {
        Assert.SkipUnless(ExecutionProviders.DirectMLAvailable, "this build has no DirectML");
        GpuAdapter unknown = new("Some GPU", 4L << 30);

        Assert.Equal(new ProviderPick(ExecutionProvider.DirectML, 0, unknown, ProviderReason.None), ExecutionProviders.Pick(ProviderChoice.Auto, [unknown]));
        Assert.Equal(new ProviderPick(ExecutionProvider.DirectML, 0, null, ProviderReason.None), ExecutionProviders.Pick(ProviderChoice.Auto, []));
        Assert.Equal(new ProviderPick(ExecutionProvider.Cpu, 0, null, ProviderReason.None), ExecutionProviders.Pick(ProviderChoice.Cpu, [Rtx4060]));
    }

    [Fact]
    public void Every_adapter_listed_is_hardware_with_its_own_index()
    {
        IReadOnlyList<GpuAdapter> all = GpuAdapter.All();

        Assert.Equal(all.Count, all.Select(a => a.Index).Distinct().Count());
        Assert.All(all, a => Assert.False(string.IsNullOrWhiteSpace(a.Name)));
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
