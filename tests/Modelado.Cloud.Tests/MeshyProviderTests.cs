using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Modelado.Cloud.Providers;

namespace Modelado.Cloud.Tests;

/// <summary>MOD37: Meshy AI behind the provider seam, against a stand-in for its REST API.</summary>
public sealed class MeshyProviderTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
    private static readonly byte[] Glb = Encoding.ASCII.GetBytes("glTF-stand-in");

    /// <summary>Answers each request from a script keyed by method and path, and keeps what it was sent.</summary>
    private sealed class FakeMeshy : HttpMessageHandler
    {
        private readonly Dictionary<string, Queue<(HttpStatusCode Status, string Body)>> script = new(StringComparer.Ordinal);

        public List<(string Line, string? Authorization, JsonNode? Body)> Sent { get; } = [];

        public FakeMeshy Answer(string line, string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            if (!script.TryGetValue(line, out Queue<(HttpStatusCode, string)>? answers))
            {
                script[line] = answers = new();
            }

            answers.Enqueue((status, body));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string line = $"{request.Method} {request.RequestUri!.AbsolutePath}";
            string? content = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Sent.Add((line, request.Headers.Authorization?.ToString(), content is null ? null : JsonNode.Parse(content)));
            if (line == "GET /files/model.glb")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Glb) };
            }

            Queue<(HttpStatusCode Status, string Body)> answers = script[line];
            (HttpStatusCode status, string body) = answers.Count > 1 ? answers.Dequeue() : answers.Peek();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static string Task(string status, int progress, int credits = 0, string? error = null) =>
        $$"""{"id": "t", "status": "{{status}}", "progress": {{progress}}, "consumed_credits": {{credits}}, "model_urls": {"glb": "https://meshy.test/files/model.glb"}{{(error is null ? "" : $", \"task_error\": {{\"message\": \"{error}\"}}")}}}""";

    private static MeshyProvider Meshy(FakeMeshy fake) => new(new HttpClient(fake), new Uri("https://meshy.test/"));

    [Fact]
    public async Task An_image_goes_up_as_a_data_uri_and_its_model_comes_back_with_the_credits_spent()
    {
        FakeMeshy fake = new FakeMeshy()
            .Answer("POST /openapi/v1/image-to-3d", """{"result": "img-1"}""")
            .Answer("GET /openapi/v1/image-to-3d/img-1", Task("PENDING", 0))
            .Answer("GET /openapi/v1/image-to-3d/img-1", Task("IN_PROGRESS", 40, 30))
            .Answer("GET /openapi/v1/image-to-3d/img-1", Task("SUCCEEDED", 100, 30));

        CloudResult result = await CloudJobs.RunAsync(Meshy(fake), "msy_key", new CloudJobRequest(CloudCapability.ImageTo3D, Image: Png), null, TestContext.Current.CancellationToken, TimeSpan.Zero);

        Assert.Equal(Glb, result.Bytes);
        Assert.Equal(new CloudCost(30, "credits"), result.Actual);
        (string line, string? authorization, JsonNode? body) = fake.Sent[0];
        Assert.Equal(("POST /openapi/v1/image-to-3d", "Bearer msy_key"), (line, authorization));
        Assert.Equal($"data:image/png;base64,{Convert.ToBase64String(Png)}", body!["image_url"]!.GetValue<string>());
        Assert.Equal((true, false, "latest", "triangle"), (body["should_texture"]!.GetValue<bool>(), body["enable_pbr"]!.GetValue<bool>(), body["ai_model"]!.GetValue<string>(), body["topology"]!.GetValue<string>()));
        Assert.Equal("glb", body["target_formats"]![0]!.GetValue<string>());
        Assert.Null(body["target_polycount"]);
    }

    [Fact]
    public async Task The_options_reach_the_request_as_meshy_names_them()
    {
        FakeMeshy fake = new FakeMeshy().Answer("POST /openapi/v1/image-to-3d", """{"result": "img-2"}""");
        Dictionary<string, string> options = new() { ["texture"] = "true", ["pbr"] = "true", ["topology"] = "quad", ["polycount"] = "8000", ["model"] = "meshy-6" };

        await Meshy(fake).SubmitAsync("k", new CloudJobRequest(CloudCapability.ImageTo3D, Image: Png, Options: options), TestContext.Current.CancellationToken);

        JsonNode body = fake.Sent[0].Body!;
        Assert.Equal((true, "quad", 8000, true, "meshy-6"), (body["enable_pbr"]!.GetValue<bool>(), body["topology"]!.GetValue<string>(), body["target_polycount"]!.GetValue<int>(), body["should_remesh"]!.GetValue<bool>(), body["ai_model"]!.GetValue<string>()));
    }

    [Fact]
    public async Task A_textured_text_job_is_a_preview_then_its_refine()
    {
        FakeMeshy fake = new FakeMeshy()
            .Answer("POST /openapi/v2/text-to-3d", """{"result": "pre-1"}""")
            .Answer("POST /openapi/v2/text-to-3d", """{"result": "ref-1"}""")
            .Answer("GET /openapi/v2/text-to-3d/pre-1", Task("SUCCEEDED", 100, 20))
            .Answer("GET /openapi/v2/text-to-3d/ref-1", Task("IN_PROGRESS", 50, 10))
            .Answer("GET /openapi/v2/text-to-3d/ref-1", Task("SUCCEEDED", 100, 10));
        List<double> progress = [];

        CloudResult result = await CloudJobs.RunAsync(
            Meshy(fake), "k", new CloudJobRequest(CloudCapability.TextTo3D, Prompt: "a wooden chair"), new SyncProgress(s => progress.Add(s.Progress)), TestContext.Current.CancellationToken, TimeSpan.Zero);

        Assert.Equal(new CloudCost(30, "credits"), result.Actual);
        Assert.Equal("preview", fake.Sent[0].Body!["mode"]!.GetValue<string>());
        Assert.Equal("a wooden chair", fake.Sent[0].Body!["prompt"]!.GetValue<string>());
        JsonNode refine = fake.Sent.First(s => s.Body?["mode"]?.GetValue<string>() == "refine").Body!;
        Assert.Equal("pre-1", refine["preview_task_id"]!.GetValue<string>());
        Assert.Equal([0, 0.5, 0.75, 1], progress);
    }

    [Fact]
    public async Task A_failed_task_says_what_meshy_said()
    {
        FakeMeshy fake = new FakeMeshy()
            .Answer("POST /openapi/v1/image-to-3d", """{"result": "img-3"}""")
            .Answer("GET /openapi/v1/image-to-3d/img-3", Task("FAILED", 10, 0, "The image has no clear object."));

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CloudJobs.RunAsync(Meshy(fake), "k", new CloudJobRequest(CloudCapability.ImageTo3D, Image: Png), null, TestContext.Current.CancellationToken, TimeSpan.Zero));

        Assert.Equal("Meshy AI failed the job: The image has no clear object.", failure.Message);
    }

    [Fact]
    public async Task A_refusal_says_what_meshy_said_and_never_the_key()
    {
        FakeMeshy fake = new FakeMeshy().Answer("POST /openapi/v1/image-to-3d", """{"message": "Insufficient credits"}""", HttpStatusCode.PaymentRequired);

        HttpRequestException refused = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Meshy(fake).SubmitAsync("msy_secret", new CloudJobRequest(CloudCapability.ImageTo3D, Image: Png), TestContext.Current.CancellationToken));

        Assert.Equal("Meshy AI answered 402: Insufficient credits", refused.Message);
        Assert.DoesNotContain("msy_secret", refused.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_key_is_checked_against_the_balance()
    {
        FakeMeshy good = new FakeMeshy().Answer("GET /openapi/v1/balance", """{"balance": 1000, "monthly_credits": 600, "permanent_credits": 400, "monthly_resets_at": null}""");
        FakeMeshy bad = new FakeMeshy().Answer("GET /openapi/v1/balance", """{"message": "Invalid API key"}""", HttpStatusCode.Unauthorized);

        Assert.Equal(new KeyCheck(true, "The key works: 1000 credits."), await Meshy(good).ValidateKeyAsync("k", TestContext.Current.CancellationToken));
        Assert.Equal(new KeyCheck(false, "Meshy AI refused the key."), await Meshy(bad).ValidateKeyAsync("k", TestContext.Current.CancellationToken));
        Assert.Equal(new CloudUsage("1000 credits", 1000), await Meshy(good).UsageAsync("k", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_running_task_cannot_be_cancelled_and_says_so()
    {
        FakeMeshy fake = new FakeMeshy().Answer("DELETE /openapi/v1/image-to-3d/img-4", "{}", HttpStatusCode.Conflict);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Meshy(fake).CancelAsync("k", "image:img-4", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(CloudCapability.ImageTo3D, null, null, 30)]
    [InlineData(CloudCapability.ImageTo3D, "false", null, 20)]
    [InlineData(CloudCapability.ImageTo3D, "true", "meshy-6-lite", 15)]
    [InlineData(CloudCapability.ImageTo3D, "false", "meshy-6-lite", 5)]
    [InlineData(CloudCapability.TextTo3D, null, null, 30)]
    [InlineData(CloudCapability.TextTo3D, "false", null, 20)]
    public void The_estimate_follows_meshys_price_table(CloudCapability action, string? texture, string? model, double credits)
    {
        Dictionary<string, string> options = [];
        if (texture is not null)
        {
            options["texture"] = texture;
        }

        if (model is not null)
        {
            options["model"] = model;
        }

        Assert.Equal(new CloudCost(credits, "credits"), Meshy(new FakeMeshy()).EstimateCost(new CloudJobRequest(action, Options: options)));
    }

    [Fact]
    public void Meshy_is_registered_and_its_prices_are_dated()
    {
        Assert.Contains(CloudProviders.All, p => p.Id == "meshy");
        Assert.Equal(new DateOnly(2026, 10, 25), MeshyProvider.PriceTable.Read);
    }

    private sealed class SyncProgress(Action<CloudJobStatus> report) : IProgress<CloudJobStatus>
    {
        public void Report(CloudJobStatus value) => report(value);
    }
}
