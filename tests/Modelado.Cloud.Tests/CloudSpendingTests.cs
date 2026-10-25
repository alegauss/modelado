namespace Modelado.Cloud.Tests;

/// <summary>MOD41: what each service cost, kept across restarts, and the budget that warns and refuses.</summary>
public sealed class CloudSpendingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.FromHours(-3));
    private readonly string path = Path.Combine(Path.GetTempPath(), $"modelado-ledger-{Guid.NewGuid():N}.jsonl");

    public void Dispose() => File.Delete(path);

    /// <summary>A service whose every job succeeds at once and costs 10 credits.</summary>
    private sealed class TenCredits : ICloudProvider
    {
        public int Submitted { get; private set; }

        public string Id => "fake";

        public string DisplayName => "Fake";

        public Uri KeyPage => new("https://example.test/keys");

        public CloudCapability Capabilities => CloudCapability.TextTo3D;

        public Task<KeyCheck> ValidateKeyAsync(string key, CancellationToken cancellation) => Task.FromResult(new KeyCheck(true, ""));

        public Task<CloudUsage?> UsageAsync(string key, CancellationToken cancellation) => Task.FromResult<CloudUsage?>(null);

        public CloudCost? EstimateCost(CloudJobRequest request) => new(10, "credits");

        public Task<string> SubmitAsync(string key, CloudJobRequest request, CancellationToken cancellation)
        {
            Submitted++;
            return Task.FromResult($"job-{Submitted}");
        }

        public Task<CloudJobStatus> PollAsync(string key, string jobId, CancellationToken cancellation) => Task.FromResult(new CloudJobStatus(CloudJobState.Succeeded, 1));

        public Task CancelAsync(string key, string jobId, CancellationToken cancellation) => Task.CompletedTask;

        public Task<CloudResult> ResultAsync(string key, string jobId, CancellationToken cancellation) => Task.FromResult(new CloudResult([1], ".glb"));
    }

    private CloudSpending Spending(double monthly) =>
        new(new CloudLedger(path), new Dictionary<string, CloudBudget> { ["fake"] = new(monthly) }, () => Now);

    private static Task<CloudResult> Run(TenCredits provider, CloudSpending spending) =>
        CloudJobs.RunAsync(provider, "key", new CloudJobRequest(CloudCapability.TextTo3D, Prompt: "a chair"), null, TestContext.Current.CancellationToken, TimeSpan.Zero, spending);

    [Fact]
    public async Task Spending_past_the_warning_share_turns_the_service_amber()
    {
        TenCredits fake = new();
        CloudSpending spending = Spending(50);

        for (int i = 0; i < 3; i++)
        {
            await Run(fake, spending);
        }

        Assert.Equal((BudgetState.Ok, 30), spending.StateOf("fake"));
        await Run(fake, spending);
        Assert.Equal((BudgetState.Warning, 40), spending.StateOf("fake"));
    }

    [Fact]
    public async Task Spending_the_whole_budget_refuses_the_next_job_before_it_is_submitted()
    {
        TenCredits fake = new();
        CloudSpending spending = Spending(20);
        await Run(fake, spending);
        await Run(fake, spending);

        BudgetExceededException refused = await Assert.ThrowsAsync<BudgetExceededException>(() => Run(fake, spending));

        Assert.Equal(2, fake.Submitted);
        Assert.Equal("Fake's budget for this month is spent (20 of 20); raise it in Cloud usage to run this.", refused.Message);
    }

    [Fact]
    public async Task The_ledger_survives_a_restart()
    {
        await Run(new TenCredits(), Spending(100));

        // A new ledger over the same file, as the next start of the app reads it.
        IReadOnlyList<LedgerEntry> read = new CloudLedger(path).Entries;

        LedgerEntry entry = Assert.Single(read);
        Assert.Equal(("fake", CloudCapability.TextTo3D, 10.0, "credits", Now, "succeeded"), (entry.Provider, entry.Action, entry.Amount, entry.Unit, entry.At, entry.Outcome));
        Assert.Equal((BudgetState.Ok, 10), Spending(100).StateOf("fake"));
    }

    [Fact]
    public void Only_this_months_spend_counts_against_the_budget()
    {
        CloudLedger ledger = new(path);
        ledger.Record(new LedgerEntry("fake", CloudCapability.TextTo3D, 90, "credits", Now.AddMonths(-1), "succeeded"));
        ledger.Record(new LedgerEntry("other", CloudCapability.TextTo3D, 90, "credits", Now, "succeeded"));

        Assert.Equal((BudgetState.Ok, 0), Spending(100).StateOf("fake"));
        Assert.Equal(180, ledger.Spent("fake") + ledger.Spent("other"));
    }

    [Fact]
    public void A_torn_last_line_leaves_the_entries_before_it()
    {
        CloudLedger ledger = new(path);
        ledger.Record(new LedgerEntry("fake", CloudCapability.TextTo3D, 10, "credits", Now, "succeeded"));
        File.AppendAllText(path, "{\"Provider\":\"fa");

        Assert.Single(ledger.Entries);
    }

    [Fact]
    public void A_service_with_no_budget_is_never_refused()
    {
        CloudSpending spending = new(new CloudLedger(path), new Dictionary<string, CloudBudget>(), () => Now);

        Assert.Equal(BudgetState.None, spending.StateOf("fake").State);
    }
}
