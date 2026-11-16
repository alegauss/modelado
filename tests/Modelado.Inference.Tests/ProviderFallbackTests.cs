using Microsoft.ML.OnnxRuntime;
using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>
/// MOD120: a model that fails on the GPU falls back to the CPU only where the GPU came from
/// Automatic; where DirectML was chosen, the failure is an error that says so. A file that is not a
/// model fails to load on any provider, which shows the two paths apart.
/// </summary>
public sealed class ProviderFallbackTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("modelado-fallback-").FullName;

    public ProviderFallbackTests()
    {
        string folder = Path.Combine(root, "broken");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "meta.json"), "{}");
        File.WriteAllBytes(Path.Combine(folder, "fp32.onnx"), [1, 2, 3, 4]);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void Only_automatic_falls_back()
    {
        Assert.True(ModelRunner.FallsBack(ProviderChoice.Auto));
        Assert.False(ModelRunner.FallsBack(ProviderChoice.DirectML));
        Assert.False(ModelRunner.FallsBack(ProviderChoice.Cpu));
    }

    [Fact]
    public void Where_directml_was_chosen_a_gpu_failure_is_an_error_naming_the_choice()
    {
        Assert.SkipUnless(ExecutionProviders.DirectMLAvailable, "this ONNX Runtime build has no DirectML");
        using ModelRunner runner = new(new ModelLibrary([root]), ExecutionProvider.DirectML, highQuality: true, fallBack: false);

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => runner.Load("broken"));

        Assert.Contains("DirectML was chosen, so it is not run on the CPU", failure.Message, StringComparison.Ordinal);
        Assert.Null(runner.Notice);
    }

    [Fact]
    public void On_automatic_a_gpu_failure_is_tried_on_the_cpu_and_the_notice_says_so()
    {
        Assert.SkipUnless(ExecutionProviders.DirectMLAvailable, "this ONNX Runtime build has no DirectML");
        using ModelRunner runner = new(new ModelLibrary([root]), ExecutionProvider.DirectML, highQuality: true, fallBack: true);

        // The CPU cannot read it either, so its own failure is what comes back, after the notice.
        Assert.Throws<OnnxRuntimeException>(() => runner.Load("broken"));
        Assert.Contains("could not be loaded on the GPU", runner.Notice, StringComparison.Ordinal);
    }
}
