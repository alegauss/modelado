using System.Net;
using System.Text;
using Modelado.Cloud.Providers;

namespace Modelado.Cloud.Tests;

/// <summary>MOD39: Ideogram behind the provider seam, against a stand-in for its v2 API.</summary>
public sealed class IdeogramProviderTests
{
    private static readonly byte[] Image = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    /// <summary>Answers by method and path (query included), and keeps each request's form.</summary>
    private sealed class FakeIdeogram : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Body)> script = new(StringComparer.Ordinal);

        public List<(string Line, string? Key, string? Form)> Sent { get; } = [];

        public FakeIdeogram Answer(string line, string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            script[line] = (status, body);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string line = $"{request.Method} {request.RequestUri!.PathAndQuery}";
            string? form = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Sent.Add((line, request.Headers.TryGetValues("Api-Key", out IEnumerable<string>? keys) ? keys.Single() : null, form));
            if (request.RequestUri.AbsolutePath.StartsWith("/files/", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Image) };
            }

            (HttpStatusCode status, string body) = script[line];
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static IdeogramProvider Ideogram(FakeIdeogram fake) => new(new HttpClient(fake), new Uri("https://ideogram.test/"));

    private const string Completed = """
        {"generation_id": "g1", "status": "completed", "usage_cost_usd_micros": 90000, "data": [
          {"prompt": "a wooden chair, centred", "seed": 11, "url": "https://ideogram.test/files/1.png", "is_image_safe": true},
          {"prompt": "a wooden chair, centred", "seed": 12, "url": "", "is_image_safe": false},
          {"prompt": "a wooden chair, centred", "seed": 13, "url": "https://ideogram.test/files/3.png", "is_image_safe": true}
        ]}
        """;

    [Fact]
    public async Task A_prompt_becomes_the_candidates_ideogram_judged_safe_with_their_seeds()
    {
        FakeIdeogram fake = new FakeIdeogram()
            .Answer("POST /v2/image/generate/ideogram-3", """{"generation_id": "g1", "seed": 11}""")
            .Answer("GET /v2/generations/g1", Completed);

        CloudResult result = await CloudJobs.RunAsync(
            Ideogram(fake), "idg_key", new CloudJobRequest(CloudCapability.TextToImage, Prompt: "a wooden chair", Options: new Dictionary<string, string> { ["count"] = "3" }),
            null, TestContext.Current.CancellationToken, TimeSpan.Zero);

        Assert.Equal([11L, 13L], result.Images!.Select(i => i.Seed));
        Assert.Equal("a wooden chair, centred", result.Images![0].Prompt);
        Assert.Equal(new CloudCost(0.09, "USD"), result.Actual);
        (string line, string? key, string? form) = fake.Sent[0];
        Assert.Equal(("POST /v2/image/generate/ideogram-3", "idg_key"), (line, key));
        foreach (string field in (string[])["name=prompt", "a wooden chair", "name=num_images", "name=rendering_speed", "turbo", "name=aspect_ratio", "1x1", "name=style_type", "realistic", "name=async"])
        {
            Assert.Contains(field, form, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_key_is_tested_by_a_dry_run_that_generates_nothing()
    {
        FakeIdeogram good = new FakeIdeogram().Answer("POST /v2/image/generate/ideogram-3?dry_run=true", """{"object": "price_quote", "usd_micros": 30000}""");
        FakeIdeogram bad = new FakeIdeogram().Answer("POST /v2/image/generate/ideogram-3?dry_run=true", """{"message": "Access denied."}""", HttpStatusCode.Unauthorized);

        Assert.Equal(new KeyCheck(true, "The key works."), await Ideogram(good).ValidateKeyAsync("k", TestContext.Current.CancellationToken));
        Assert.Equal(new KeyCheck(false, "Ideogram refused the key."), await Ideogram(bad).ValidateKeyAsync("k", TestContext.Current.CancellationToken));
        Assert.DoesNotContain("name=async", good.Sent[0].Form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_generation_says_why()
    {
        FakeIdeogram fake = new FakeIdeogram()
            .Answer("POST /v2/image/generate/ideogram-3", """{"generation_id": "g2", "seed": 1}""")
            .Answer("GET /v2/generations/g2", """{"generation_id": "g2", "status": "failed", "failure_reason": "content_policy_violation"}""");

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() => CloudJobs.RunAsync(
            Ideogram(fake), "k", new CloudJobRequest(CloudCapability.TextToImage, Prompt: "x"), null, TestContext.Current.CancellationToken, TimeSpan.Zero));

        Assert.Equal("Ideogram failed the job: content policy violation", failure.Message);
    }

    [Fact]
    public async Task A_refusal_says_what_ideogram_said()
    {
        FakeIdeogram fake = new FakeIdeogram().Answer("POST /v2/image/generate/ideogram-3", """{"detail": "9 is greater than the maximum of 8 - 'num_images'"}""", HttpStatusCode.BadRequest);

        HttpRequestException refused = await Assert.ThrowsAsync<HttpRequestException>(() => Ideogram(fake).SubmitAsync(
            "k", new CloudJobRequest(CloudCapability.TextToImage, Prompt: "x", Options: new Dictionary<string, string> { ["count"] = "9" }), TestContext.Current.CancellationToken));

        Assert.Equal("Ideogram answered 400: 9 is greater than the maximum of 8 - 'num_images'", refused.Message);
    }

    [Theory]
    [InlineData(null, null, 0.12)]
    [InlineData("1", "quality", 0.09)]
    [InlineData("2", "default", 0.12)]
    public void The_estimate_is_per_image_by_speed(string? count, string? speed, double usd)
    {
        Dictionary<string, string> options = [];
        if (count is not null)
        {
            options["count"] = count;
        }

        if (speed is not null)
        {
            options["speed"] = speed;
        }

        CloudCost? cost = Ideogram(new FakeIdeogram()).EstimateCost(new CloudJobRequest(CloudCapability.TextToImage, Prompt: "x", Options: options));

        Assert.Equal(new CloudCost(usd, "USD"), cost);
        Assert.Equal(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"${usd:0.00}"), cost!.ToString());
    }

    [Fact]
    public void Ideogram_is_registered_beside_meshy()
    {
        Assert.Equal(["meshy", "ideogram"], CloudProviders.All.Select(p => p.Id));
    }
}
