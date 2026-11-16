using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>MOD116: Light quality asks for the INT8 variant on the CPU, and falls back to fp32 wherever there is none.</summary>
public sealed class LightQualityTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("modelado-light-").FullName;

    public void Dispose() => Directory.Delete(root, recursive: true);

    private void Model(string name, params string[] variants)
    {
        Directory.CreateDirectory(Path.Combine(root, name));
        File.WriteAllText(Path.Combine(root, name, "meta.json"), "{}");
        foreach (string variant in variants)
        {
            File.WriteAllBytes(Path.Combine(root, name, variant + ".onnx"), [1]);
        }
    }

    [Theory]
    [InlineData(ExecutionProvider.Cpu, false, false, "fp32")]
    [InlineData(ExecutionProvider.Cpu, false, true, "int8")]
    [InlineData(ExecutionProvider.Cpu, true, true, "fp32")]
    [InlineData(ExecutionProvider.DirectML, false, true, "fp16")]
    [InlineData(ExecutionProvider.DirectML, true, false, "fp32")]
    public void Light_is_int8_on_the_cpu_only_and_high_wins(ExecutionProvider provider, bool high, bool light, string variant)
    {
        Assert.Equal(variant, ExecutionProviders.VariantFor(provider, high, light));
    }

    [Fact]
    public void A_light_run_uses_the_int8_encoder_and_fp32_for_models_that_have_no_int8()
    {
        Model("triposr-encoder", "fp32", "int8");
        Model("u2net", "fp32");
        using ModelRunner runner = new(new ModelLibrary([root]), ExecutionProvider.Cpu, light: true);

        Assert.Equal("int8", runner.VariantOf("triposr-encoder"));
        Assert.Equal("fp32", runner.VariantOf("u2net"));
        Assert.Empty(runner.Missing(["triposr-encoder", "u2net"]));
    }

    [Fact]
    public void A_light_download_of_a_model_the_manifest_has_no_int8_of_fetches_fp32()
    {
        ModelManifest manifest = ModelManifest.Parse("""
            {"schema": 1, "version": "t", "models": [{"name": "u2net", "task": "background-removal",
             "meta": {"url": "https://h/m.json", "bytes": 2, "sha256": "aa"},
             "variants": {"fp32": {"url": "https://h/u2net-fp32.onnx", "bytes": 5, "sha256": "bb"}},
             "license": "MIT", "license_url": "https://h/l"}]}
            """);
        using ModelRunner runner = new(new ModelLibrary([root]), ExecutionProvider.Cpu, light: true);

        IReadOnlyList<ModelRequest> missing = runner.Missing(["u2net"]);

        Assert.Equal("int8", Assert.Single(missing).Variant);
        Assert.Equal("fp32", ModelDownloads.VariantOf(manifest, missing[0]));
    }
}
