using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>A loopback HTTP server holding files, honouring Range, that can fail on demand.</summary>
internal sealed class FileServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

    public FileServer()
    {
        for (int attempt = 0; ; attempt++)
        {
            int port = Random.Shared.Next(20000, 60000);
            try
            {
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                listener.Start();
                Base = $"http://127.0.0.1:{port}";
                break;
            }
            catch (HttpListenerException) when (attempt < 10)
            {
                listener.Prefixes.Clear();
            }
        }

        _ = Task.Run(Serve);
    }

    public string Base { get; } = "";

    public int Requests { get; private set; }

    public List<string?> Ranges { get; } = [];

    /// <summary>Requests to answer with 503 before serving again.</summary>
    public int FailNext { get; set; }

    public void Add(string name, byte[] bytes) => files[name] = bytes;

    public void Dispose() => listener.Close();

    private async Task Serve()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (!listener.IsListening)
            {
                return;
            }

            Requests++;
            HttpListenerResponse response = context.Response;
            string name = context.Request.Url!.AbsolutePath.TrimStart('/');
            Ranges.Add(context.Request.Headers["Range"]);
            if (FailNext > 0)
            {
                FailNext--;
                response.StatusCode = 503;
            }
            else if (!files.TryGetValue(name, out byte[]? bytes))
            {
                response.StatusCode = 404;
            }
            else
            {
                int from = 0;
                if (context.Request.Headers["Range"] is { } range && range.StartsWith("bytes=", StringComparison.Ordinal))
                {
                    from = int.Parse(range[6..].TrimEnd('-'), System.Globalization.CultureInfo.InvariantCulture);
                    response.StatusCode = 206;
                }

                response.ContentLength64 = bytes.Length - from;
                await response.OutputStream.WriteAsync(bytes.AsMemory(from)).ConfigureAwait(false);
            }

            response.Close();
        }
    }
}

public sealed class ModelDownloadsTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("modelado-dl-").FullName;
    private readonly FileServer server = new();
    private readonly HttpClient http = new();
    private readonly byte[] encoder16 = Encoding.ASCII.GetBytes(new string('e', 5000));
    private readonly byte[] encoder32 = Encoding.ASCII.GetBytes(new string('E', 9000));
    private readonly byte[] decoder32 = Encoding.ASCII.GetBytes(new string('d', 700));
    private readonly byte[] meta = "{\"name\": \"x\"}"u8.ToArray();

    public void Dispose()
    {
        http.Dispose();
        server.Dispose();
        Directory.Delete(root, recursive: true);
    }

    private ModelManifest Manifest()
    {
        server.Add("enc-fp16.onnx", encoder16);
        server.Add("enc-fp32.onnx", encoder32);
        server.Add("dec-fp32.onnx", decoder32);
        server.Add("meta.json", meta);
        object File(string name, byte[] bytes) => new { url = $"{server.Base}/{name}", bytes = bytes.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) };
        string json = JsonSerializer.Serialize(new
        {
            schema = 1,
            version = "test",
            models = new object[]
            {
                new { name = "enc", task = "image-to-triplane", meta = File("meta.json", meta), variants = new Dictionary<string, object> { ["fp16"] = File("enc-fp16.onnx", encoder16), ["fp32"] = File("enc-fp32.onnx", encoder32) }, license = "MIT", license_url = "https://example.org" },
                new { name = "dec", task = "triplane-decoder", meta = File("meta.json", meta), variants = new Dictionary<string, object> { ["fp32"] = File("dec-fp32.onnx", decoder32) }, license = "MIT", license_url = "https://example.org" },
            },
        });
        return ModelManifest.Parse(json, loopback: true);
    }

    private static ModelRequest[] Fp16(params string[] models) => [.. models.Select(m => new ModelRequest(m, "fp16"))];

    [Fact]
    public async Task Fetches_the_variant_asked_for_and_fp32_where_there_is_no_other()
    {
        ModelManifest manifest = Manifest();
        DownloadResult result = await ModelDownloads.DownloadAsync(http, manifest, root, Fp16("enc", "dec"), null, TestContext.Current.CancellationToken);

        Assert.Equal(4, result.Fetched);
        Assert.Equal(encoder16, await File.ReadAllBytesAsync(Path.Combine(root, "enc", "fp16.onnx"), TestContext.Current.CancellationToken));
        Assert.Equal(decoder32, await File.ReadAllBytesAsync(Path.Combine(root, "dec", "fp32.onnx"), TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(root, "enc", "fp32.onnx")));
        Assert.Equal(0, ModelDownloads.PendingBytes(manifest, root, Fp16("enc", "dec")));
    }

    [Fact]
    public async Task A_second_download_fetches_nothing()
    {
        ModelManifest manifest = Manifest();
        await ModelDownloads.DownloadAsync(http, manifest, root, Fp16("enc"), null, TestContext.Current.CancellationToken);
        int before = server.Requests;
        DownloadResult again = await ModelDownloads.DownloadAsync(http, manifest, root, Fp16("enc"), null, TestContext.Current.CancellationToken);

        Assert.Equal(0, again.Fetched);
        Assert.Equal(2, again.Present);
        Assert.Equal(before, server.Requests);
    }

    [Fact]
    public async Task A_partial_file_resumes_with_a_range()
    {
        ModelManifest manifest = Manifest();
        Directory.CreateDirectory(Path.Combine(root, "enc"));
        await File.WriteAllBytesAsync(Path.Combine(root, "enc", "fp16.onnx.part"), encoder16[..1234], TestContext.Current.CancellationToken);

        await ModelDownloads.DownloadAsync(http, manifest, root, Fp16("enc"), null, TestContext.Current.CancellationToken);

        Assert.Contains("bytes=1234-", server.Ranges);
        Assert.Equal(encoder16, await File.ReadAllBytesAsync(Path.Combine(root, "enc", "fp16.onnx"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_server_error_is_retried()
    {
        ModelManifest manifest = Manifest();
        server.FailNext = 2;

        DownloadResult result = await ModelDownloads.DownloadAsync(http, manifest, root, Fp16("dec"), null, TestContext.Current.CancellationToken, retryDelay: TimeSpan.FromMilliseconds(10));

        Assert.Equal(2, result.Fetched);
    }

    [Fact]
    public async Task A_file_with_the_wrong_hash_is_deleted_and_reported()
    {
        ModelManifest manifest = Manifest();
        server.Add("dec-fp32.onnx", Encoding.ASCII.GetBytes(new string('X', 700)));

        await Assert.ThrowsAsync<InvalidDataException>(() => ModelDownloads.DownloadAsync(http, manifest, root, Fp16("dec"), null, TestContext.Current.CancellationToken));

        Assert.False(File.Exists(Path.Combine(root, "dec", "fp32.onnx")));
        Assert.False(File.Exists(Path.Combine(root, "dec", "fp32.onnx.part")));
    }

    [Fact]
    public async Task Progress_ends_at_the_total()
    {
        ModelManifest manifest = Manifest();
        List<DownloadProgress> seen = [];
        await ModelDownloads.DownloadAsync(http, manifest, root, Fp16("enc", "dec"), new Collect(seen), TestContext.Current.CancellationToken);

        long total = ModelDownloads.TotalBytes(manifest, Fp16("enc", "dec"));
        Assert.Equal(encoder16.Length + decoder32.Length + (2 * meta.Length), total);
        Assert.Equal(total, seen[^1].Done);
    }

    [Fact]
    public void A_manifest_with_plain_http_is_refused_unless_on_loopback()
    {
        string json = """{"schema": 1, "version": "1", "models": [{"name": "m", "task": "t", "meta": {"url": "http://example.org/m.json", "bytes": 1, "sha256": "00"}, "variants": {}, "license": "MIT", "license_url": "x"}]}""";

        Assert.Throws<InvalidDataException>(() => ModelManifest.Parse(json, loopback: true));
        Assert.Throws<InvalidDataException>(() => ModelManifest.Parse(json.Replace("\"schema\": 1", "\"schema\": 2", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_embedded_manifest_is_empty_until_published_and_valid_when_present()
    {
        string path = Path.Combine(Repository.Root, "src", "Modelado.Inference", "models-manifest.json");
        ModelManifest embedded = ModelManifest.Parse(File.ReadAllText(path));

        Assert.Equal(embedded.Models.Count > 0, ModelDownloads.Embedded is not null);
        Assert.All(embedded.Models, m => Assert.StartsWith("https://huggingface.co/", m.Meta.Url, StringComparison.Ordinal));
    }

    private sealed class Collect(List<DownloadProgress> into) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => into.Add(value);
    }
}
