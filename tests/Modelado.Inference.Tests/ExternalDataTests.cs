using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>
/// MOD124: a model whose weights are ONNX external data (a graph over protobuf's 2 GB limit, such as
/// TripoSG's) is listed with its parts, downloaded with them beside the graph, verified, and runs.
/// The fixture is a 4-element Add whose constant lives in fp32.onnx.data, the way a large one does.
/// </summary>
public sealed class ExternalDataTests : IDisposable
{
    private static readonly string Fixture = Path.Combine(Repository.Root, "tests", "Modelado.Inference.Tests", "Fixtures", "external-data");
    private readonly string root = Directory.CreateTempSubdirectory("modelado-external-").FullName;
    private readonly FileServer server = new();
    private readonly HttpClient http = new();
    private readonly byte[] graph = File.ReadAllBytes(Path.Combine(Fixture, "fp32.onnx"));
    private readonly byte[] weights = File.ReadAllBytes(Path.Combine(Fixture, "fp32.onnx.data"));
    private readonly byte[] meta = "{\"name\": \"ext\"}"u8.ToArray();

    public void Dispose()
    {
        http.Dispose();
        server.Dispose();
        Directory.Delete(root, recursive: true);
    }

    private ModelManifest Manifest(string partName = "fp32.onnx.data", byte[]? served = null)
    {
        server.Add("ext-fp32.onnx", graph);
        server.Add("ext-fp32.onnx.data", served ?? weights);
        server.Add("ext-meta.json", meta);
        object File(string name, byte[] bytes) => new { url = $"{server.Base}/{name}", bytes = bytes.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) };
        string json = JsonSerializer.Serialize(new
        {
            schema = 1,
            version = "test",
            models = new object[]
            {
                new
                {
                    name = "ext",
                    task = "test",
                    meta = File("ext-meta.json", meta),
                    variants = new Dictionary<string, object>
                    {
                        ["fp32"] = new
                        {
                            url = $"{server.Base}/ext-fp32.onnx",
                            bytes = graph.Length,
                            sha256 = Convert.ToHexStringLower(SHA256.HashData(graph)),
                            data = new[] { new { name = partName, url = $"{server.Base}/ext-fp32.onnx.data", bytes = weights.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(weights)) } },
                        },
                    },
                    license = "MIT",
                    license_url = "https://example.org",
                },
            },
        });
        return ModelManifest.Parse(json, loopback: true);
    }

    [Fact]
    public async Task A_model_in_parts_downloads_beside_its_graph_verifies_and_runs()
    {
        ModelManifest manifest = Manifest();
        ModelRequest[] request = [new("ext", "fp32")];
        Assert.Equal(graph.Length + weights.Length + meta.Length, ModelDownloads.TotalBytes(manifest, request));

        DownloadResult result = await ModelDownloads.DownloadAsync(http, manifest, root, request, null, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Fetched);
        Assert.Equal(weights, await File.ReadAllBytesAsync(Path.Combine(root, "ext", "fp32.onnx.data"), TestContext.Current.CancellationToken));
        Assert.Equal(0, ModelDownloads.PendingBytes(manifest, root, request));

        // ONNX Runtime finds the weights beside the graph: Y = X + [1, 2, 3, 4].
        using ModelRunner runner = new(new ModelLibrary([root]), ExecutionProvider.Cpu);
        using OrtValue x = OrtValue.CreateTensorValueFromMemory(new float[] { 10, 20, 30, 40 }, [4]);
        using IDisposableReadOnlyCollection<OrtValue> y = runner.Run("ext", new Dictionary<string, OrtValue> { ["X"] = x }, ["Y"]);
        Assert.Equal([11f, 22f, 33f, 44f], y[0].GetTensorDataAsSpan<float>().ToArray());
    }

    [Fact]
    public async Task Weights_that_arrive_wrong_leave_no_graph_behind()
    {
        // The parts come first, so a failed part never leaves a graph that looks installed.
        ModelManifest manifest = Manifest(served: new byte[16]);

        await Assert.ThrowsAsync<InvalidDataException>(() => ModelDownloads.DownloadAsync(http, manifest, root, [new("ext", "fp32")], null, TestContext.Current.CancellationToken, retryDelay: TimeSpan.Zero));

        Assert.False(File.Exists(Path.Combine(root, "ext", "fp32.onnx")));
        Assert.False(new ModelLibrary([root]).Has("ext", "fp32"));
    }

    [Theory]
    [InlineData("../elsewhere.data")]
    [InlineData("sub\\fp32.onnx.data")]
    [InlineData("C:weights")]
    public void A_part_named_as_a_path_is_refused(string name)
    {
        InvalidDataException refused = Assert.Throws<InvalidDataException>(() => Manifest(partName: name));
        Assert.Contains("bare file name", refused.Message, StringComparison.Ordinal);
    }
}
