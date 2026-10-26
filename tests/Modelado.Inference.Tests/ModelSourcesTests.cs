using System.Net;
using Modelado.Inference;
using Modelado.Inference.Pipelines;

namespace Modelado.Inference.Tests;

/// <summary>MOD27: model repositories added beside the built-in one, pinned and merged after it.</summary>
public sealed class ModelSourcesTests : IDisposable
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"modelado-sources-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static ManifestModel Model(string name, string task) => new(
        name, task, new ManifestFile($"https://host/{name}-meta.json", 1, new string('0', 64)),
        new Dictionary<string, ManifestFile> { ["fp32"] = new($"https://host/{name}-fp32.onnx", 1, new string('0', 64)) }, "MIT", "https://host/LICENSE");

    private static readonly ModelManifest BuiltIn = new(1, "1", [Model(ReliefReconstruction.Depth, "depth"), Model("u2net", "background-removal")]);

    private static readonly ModelSource Mine = new("me/models", "https://huggingface.co/me/models/resolve/x/manifest.json");

    /// <summary>Hugging Face's revision API, and a manifest, as the resolver and the merge read them.</summary>
    private sealed class Hub(string answer) : HttpMessageHandler
    {
        public List<Uri> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) });
        }
    }

    [Fact]
    public async Task A_repository_id_is_pinned_to_the_commit_main_is_at()
    {
        Hub hub = new($$"""{"id": "me/models", "sha": "{{Commit}}"}""");
        using HttpClient http = new(hub);

        ModelSource source = await ModelSources.ResolveAsync(http, " me/models ", TestContext.Current.CancellationToken);

        Assert.Equal(new ModelSource("me/models", $"https://huggingface.co/me/models/resolve/{Commit}/manifest.json"), source);
        Assert.Equal("https://huggingface.co/api/models/me/models/revision/main", Assert.Single(hub.Asked).AbsoluteUri);
    }

    [Fact]
    public async Task A_revision_answer_without_a_commit_is_refused()
    {
        using HttpClient http = new(new Hub("""{"id": "me/models"}"""));

        await Assert.ThrowsAsync<HttpRequestException>(() => ModelSources.ResolveAsync(http, "me/models", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://example.org/models/manifest.json")]
    [InlineData("http://127.0.0.1:47631/files/manifest.json")]
    public async Task A_manifest_url_is_kept_as_given(string url)
    {
        Hub hub = new("{}");
        using HttpClient http = new(hub);

        Assert.Equal(url, (await ModelSources.ResolveAsync(http, url, TestContext.Current.CancellationToken)).ManifestUrl);
        Assert.Empty(hub.Asked);
    }

    [Theory]
    [InlineData("http://example.org/manifest.json")]
    [InlineData("models")]
    [InlineData("me/../models")]
    [InlineData("file:///C:/manifest.json")]
    public async Task Anything_else_is_refused_with_what_is_accepted(string given)
    {
        using HttpClient http = new(new Hub("{}"));

        ArgumentException refused = await Assert.ThrowsAsync<ArgumentException>(() => ModelSources.ResolveAsync(http, given, TestContext.Current.CancellationToken));
        Assert.Contains("owner/name", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_source_s_models_come_after_the_built_in_ones()
    {
        ModelManifest merged = ModelSources.Merge(BuiltIn, [(Mine, new ModelManifest(1, "custom", [Model("my-depth", "depth")]))]);

        Assert.Equal([ReliefReconstruction.Depth, "u2net", "my-depth"], merged.Models.Select(m => m.Name));
        Assert.Equal("1", merged.Version);
        Assert.Equal([ReliefReconstruction.Depth, "my-depth"], ModelSources.ForTask(merged, ReliefReconstruction.DepthTask));
    }

    [Fact]
    public void A_source_reusing_a_built_in_name_is_refused_naming_it()
    {
        InvalidDataException refused = Assert.Throws<InvalidDataException>(() =>
            ModelSources.Merge(BuiltIn, [(Mine, new ModelManifest(1, "custom", [Model(ReliefReconstruction.Depth, "depth")]))]));

        Assert.Contains($"me/models lists {ReliefReconstruction.Depth}, a name the built-in models already use", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_sources_cannot_share_a_name_either()
    {
        ModelSource other = new("you/models", "https://huggingface.co/you/models/resolve/y/manifest.json");
        ModelManifest custom = new(1, "custom", [Model("my-depth", "depth")]);

        InvalidDataException refused = Assert.Throws<InvalidDataException>(() => ModelSources.Merge(BuiltIn, [(Mine, custom), (other, custom)]));

        Assert.Contains("you/models lists my-depth, a name me/models already use", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_source_s_manifest_is_read_and_merged()
    {
        const string Json = """
            {"schema": 1, "version": "custom", "models": [{"name": "my-depth", "task": "depth",
              "meta": {"url": "https://host/my-depth-meta.json", "bytes": 1, "sha256": "00"},
              "variants": {"fp32": {"url": "https://host/my-depth-fp32.onnx", "bytes": 1, "sha256": "00"}},
              "license": "MIT", "license_url": "https://host/LICENSE"}]}
            """;
        Hub hub = new(Json);
        using HttpClient http = new(hub);

        ModelManifest merged = await ModelSources.WithSourcesAsync(http, BuiltIn, [Mine], TestContext.Current.CancellationToken);

        Assert.Equal("my-depth", merged.Models[^1].Name);
        Assert.Equal(Mine.ManifestUrl, Assert.Single(hub.Asked).AbsoluteUri);
    }

    [Fact]
    public void Sources_are_kept_in_order_beside_the_other_settings()
    {
        string settings = Path.Combine(folder, "settings.json");
        ProviderSetting.Save(settings, ProviderChoice.Cpu);
        ModelSource other = new("https://example.org/manifest.json", "https://example.org/manifest.json");

        ModelSources.Save(settings, [Mine, other]);

        Assert.Equal([Mine, other], ModelSources.Load(settings));
        Assert.Equal(ProviderChoice.Cpu, ProviderSetting.Load(settings));
    }

    [Fact]
    public void Relief_refuses_a_chosen_model_that_is_not_a_depth_model()
    {
        string model = Path.Combine(folder, "my-upscaler");
        Directory.CreateDirectory(model);
        File.WriteAllText(Path.Combine(model, "meta.json"), """
            {"schema": 1, "name": "my-upscaler", "task": "upscale",
             "inputs": [{"name": "image", "shape": [1, 3, "h", "w"]}], "outputs": [{"name": "up", "shape": [1, 3, "h", "w"]}],
             "preprocess": {"value_range": [0, 1]}, "params": {},
             "variants": {"fp32": {"file": "fp32.onnx", "bytes": 1, "sha256": "00"}},
             "upstream": {"url": "u", "license": "MIT", "license_url": "l"}}
            """);
        using ModelRunner runner = new(new ModelLibrary([folder]), ExecutionProvider.Cpu);

        InvalidDataException refused = Assert.Throws<InvalidDataException>(() =>
            ReliefReconstruction.EstimateDepth(runner, new Modelado.Imaging.RgbaImage(2, 2, new byte[16]), "my-upscaler"));

        Assert.Equal("my-upscaler's task is upscale, not depth, so it cannot make a relief.", refused.Message);
    }

    [Fact]
    public void No_file_or_a_file_that_does_not_read_holds_no_sources()
    {
        string settings = Path.Combine(folder, "settings.json");
        Assert.Empty(ModelSources.Load(settings));
        Directory.CreateDirectory(folder);
        File.WriteAllText(settings, "{ not json");
        Assert.Empty(ModelSources.Load(settings));
    }
}
