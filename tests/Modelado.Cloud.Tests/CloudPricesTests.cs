namespace Modelado.Cloud.Tests;

/// <summary>MOD42: an action's cost is estimated before it runs, from a dated price table.</summary>
public sealed class CloudPricesTests : IDisposable
{
    private const string Table = """
        {
          "provider": "fake", "unit": "credits", "read": "2026-10-01", "source": "https://example.test/pricing",
          "actions": {
            "ImageTo3D": { "base": 20, "options": { "texture": { "true": 10 }, "candidates": { "4": 15 } } },
            "TextTo3D": { "base": 5 }
          }
        }
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private readonly string path = Path.Combine(Path.GetTempPath(), $"modelado-ledger-{Guid.NewGuid():N}.jsonl");

    public void Dispose() => File.Delete(path);

    /// <summary>A service priced by the table, which reports 12 credits for what it was asked.</summary>
    private sealed class Priced(CloudPrices prices, double? actual) : ICloudProvider
    {
        public int Submitted { get; private set; }

        public string Id => "fake";

        public string DisplayName => "Fake";

        public Uri KeyPage => new("https://example.test/keys");

        public CloudCapability Capabilities => CloudCapability.ImageTo3D | CloudCapability.TextTo3D;

        public Task<KeyCheck> ValidateKeyAsync(string key, CancellationToken cancellation) => Task.FromResult(new KeyCheck(true, ""));

        public Task<CloudUsage?> UsageAsync(string key, CancellationToken cancellation) => Task.FromResult<CloudUsage?>(null);

        public CloudCost? EstimateCost(CloudJobRequest request) => prices.Estimate(request);

        public Task<string> SubmitAsync(string key, CloudJobRequest request, CancellationToken cancellation)
        {
            Submitted++;
            return Task.FromResult("job");
        }

        public Task<CloudJobStatus> PollAsync(string key, string jobId, CancellationToken cancellation) => Task.FromResult(new CloudJobStatus(CloudJobState.Succeeded, 1));

        public Task CancelAsync(string key, string jobId, CancellationToken cancellation) => Task.CompletedTask;

        public Task<CloudResult> ResultAsync(string key, string jobId, CancellationToken cancellation) =>
            Task.FromResult(new CloudResult([1], ".glb", actual is { } a ? new CloudCost(a, "credits") : null));
    }

    private static CloudJobRequest Image(params (string Name, string Value)[] options) =>
        new(CloudCapability.ImageTo3D, Image: [1], Options: options.ToDictionary(o => o.Name, o => o.Value));

    [Fact]
    public void Each_option_change_moves_the_estimate()
    {
        CloudPrices prices = CloudPrices.Parse(Table);

        Assert.Equal(new CloudCost(20, "credits"), prices.Estimate(Image()));
        Assert.Equal(new CloudCost(30, "credits"), prices.Estimate(Image(("texture", "true"))));
        Assert.Equal(new CloudCost(20, "credits"), prices.Estimate(Image(("texture", "false"))));
        Assert.Equal(new CloudCost(45, "credits"), prices.Estimate(Image(("texture", "true"), ("candidates", "4"))));
        Assert.Equal(new CloudCost(5, "credits"), prices.Estimate(new CloudJobRequest(CloudCapability.TextTo3D, Prompt: "a chair")));
        Assert.Null(prices.Estimate(new CloudJobRequest(CloudCapability.Remesh)));
    }

    [Fact]
    public async Task An_action_that_would_cross_the_budget_is_refused_before_upload()
    {
        Priced fake = new(CloudPrices.Parse(Table), null);
        CloudLedger ledger = new(path);
        ledger.Record(new LedgerEntry("fake", CloudCapability.TextTo3D, 15, "credits", Now, "succeeded"));
        CloudSpending spending = new(ledger, new Dictionary<string, CloudBudget> { ["fake"] = new(40) }, () => Now);

        BudgetExceededException refused = await Assert.ThrowsAsync<BudgetExceededException>(() =>
            CloudJobs.RunAsync(fake, "key", Image(("texture", "true")), null, TestContext.Current.CancellationToken, TimeSpan.Zero, spending));

        Assert.Equal(0, fake.Submitted);
        Assert.Equal("Fake's budget for this month is too small for it: ~30 with 25 of 40 left; raise it in Cloud usage to run this.", refused.Message);
        await CloudJobs.RunAsync(fake, "key", Image(), null, TestContext.Current.CancellationToken, TimeSpan.Zero, spending);
        Assert.Equal(1, fake.Submitted);
    }

    [Theory]
    [InlineData(12.0, 12.0)]
    [InlineData(null, 30.0)]
    public async Task The_ledger_records_the_estimate_and_the_actual(double? reported, double recorded)
    {
        Priced fake = new(CloudPrices.Parse(Table), reported);
        CloudSpending spending = new(new CloudLedger(path), new Dictionary<string, CloudBudget>(), () => Now);

        await CloudJobs.RunAsync(fake, "key", Image(("texture", "true")), null, TestContext.Current.CancellationToken, TimeSpan.Zero, spending);

        LedgerEntry entry = Assert.Single(new CloudLedger(path).Entries);
        Assert.Equal((30.0, recorded, "credits"), (entry.Estimated, entry.Amount, entry.Unit));
    }

    [Fact]
    public void A_table_older_than_ninety_days_is_stale()
    {
        CloudPrices prices = CloudPrices.Parse(Table);

        Assert.False(prices.IsStale(new DateOnly(2026, 12, 30)));
        Assert.True(prices.IsStale(new DateOnly(2026, 12, 31)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"provider": "fake", "unit": "credits", "read": "1 Oct", "actions": {}}""")]
    [InlineData("""{"provider": "fake", "unit": "credits", "read": "2026-10-01", "actions": {"Teleport": {"base": 1}}}""")]
    public void A_table_that_does_not_read_is_refused(string json)
    {
        Assert.Throws<InvalidDataException>(() => CloudPrices.Parse(json));
    }
}
