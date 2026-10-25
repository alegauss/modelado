using System.Numerics;
using Modelado.Geometry;

namespace Modelado.Cloud.Tests;

/// <summary>MOD36: a job runs from submit to a version in the history through the provider seam.</summary>
public sealed class CloudJobsTests
{
    private static byte[] Glb()
    {
        Mesh quad = new(
            [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)],
            [0, 1, 2, 0, 2, 3],
            [new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0)]);
        string path = Path.Combine(Path.GetTempPath(), $"modelado-cloud-{Guid.NewGuid():N}.glb");
        try
        {
            MeshExport.Save(quad, path);
            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A service that answers from a script of statuses.</summary>
    private sealed class FakeProvider(params CloudJobStatus[] script) : ICloudProvider
    {
        private int polled;

        public string Id => "fake";

        public string DisplayName => "Fake";

        public Uri KeyPage => new("https://example.test/keys");

        public CloudCapability Capabilities => CloudCapability.ImageTo3D | CloudCapability.TextTo3D;

        public List<string> Calls { get; } = [];

        public byte[] Result { get; init; } = [];

        public Task<KeyCheck> ValidateKeyAsync(string key, CancellationToken cancellation) => Task.FromResult(new KeyCheck(key == "good", "checked"));

        public Task<CloudUsage?> UsageAsync(string key, CancellationToken cancellation) => Task.FromResult<CloudUsage?>(null);

        public CloudCost? EstimateCost(CloudJobRequest request) => new(5, "credits");

        public Task<string> SubmitAsync(string key, CloudJobRequest request, CancellationToken cancellation)
        {
            Calls.Add($"submit {key} {request.Action}");
            return Task.FromResult("job-1");
        }

        public Task<CloudJobStatus> PollAsync(string key, string jobId, CancellationToken cancellation)
        {
            Calls.Add($"poll {jobId}");
            return Task.FromResult(script[Math.Min(polled++, script.Length - 1)]);
        }

        public Task CancelAsync(string key, string jobId, CancellationToken cancellation)
        {
            Calls.Add($"cancel {jobId}");
            return Task.CompletedTask;
        }

        public Task<CloudResult> ResultAsync(string key, string jobId, CancellationToken cancellation)
        {
            Calls.Add($"result {jobId}");
            return Task.FromResult(new CloudResult(Result, ".glb"));
        }
    }

    private sealed class Recorded : IProgress<CloudJobStatus>
    {
        public List<CloudJobState> States { get; } = [];

        public void Report(CloudJobStatus value) => States.Add(value.State);
    }

    [Fact]
    public async Task A_job_runs_from_submit_to_a_version_in_the_history()
    {
        FakeProvider fake = new(new(CloudJobState.Running, 0.5), new(CloudJobState.Succeeded, 1)) { Result = Glb() };
        Recorded progress = new();
        MeshHistory history = new();
        history.Reset(new Mesh([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [0, 1, 2]), "Opened");

        CloudResult result = await CloudJobs.RunAsync(fake, "good", new CloudJobRequest(CloudCapability.ImageTo3D, Image: [1, 2, 3]), progress, TestContext.Current.CancellationToken, TimeSpan.Zero);
        Mesh made = CloudJobs.ToMesh(result);
        history.Push(made, CloudJobs.Label(fake, CloudCapability.ImageTo3D));

        Assert.Equal(["submit good ImageTo3D", "poll job-1", "poll job-1", "result job-1"], fake.Calls);
        Assert.Equal([CloudJobState.Queued, CloudJobState.Running, CloudJobState.Succeeded], progress.States);
        Assert.Equal(2, made.TriangleCount);
        Assert.Equal("Fake: image to 3D", history.Labels[^1]);
    }

    [Fact]
    public async Task A_failed_job_says_what_the_service_said()
    {
        FakeProvider fake = new(new CloudJobStatus(CloudJobState.Failed, 0, "no object found"));

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CloudJobs.RunAsync(fake, "good", new CloudJobRequest(CloudCapability.TextTo3D, Prompt: "a chair"), null, TestContext.Current.CancellationToken, TimeSpan.Zero));

        Assert.Equal("Fake failed the job: no object found", failure.Message);
    }

    [Fact]
    public async Task A_cancelled_job_is_cancelled_at_the_service_too()
    {
        FakeProvider fake = new(new CloudJobStatus(CloudJobState.Running, 0.1));
        using CancellationTokenSource cancel = new();
        Task<CloudResult> running = CloudJobs.RunAsync(fake, "good", new CloudJobRequest(CloudCapability.ImageTo3D), null, cancel.Token, TimeSpan.FromMilliseconds(20));

        await Task.Delay(100, TestContext.Current.CancellationToken);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal("cancel job-1", fake.Calls[^1]);
    }

    [Fact]
    public async Task An_action_the_service_lacks_is_never_submitted()
    {
        FakeProvider fake = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CloudJobs.RunAsync(fake, "good", new CloudJobRequest(CloudCapability.Remesh), null, TestContext.Current.CancellationToken));

        Assert.Empty(fake.Calls);
    }

    [Fact]
    public void Providers_are_found_by_what_they_can_do()
    {
        FakeProvider fake = new();

        Assert.Equal([fake], CloudProviders.For(CloudCapability.TextTo3D, [fake]));
        Assert.Empty(CloudProviders.For(CloudCapability.Texture, [fake]));
        Assert.Empty(CloudProviders.For(CloudCapability.None, [fake]));
    }

    [Fact]
    public void A_model_result_is_not_read_as_an_image_nor_an_image_as_a_model()
    {
        Assert.Throws<InvalidDataException>(() => CloudJobs.ToImage(new CloudResult([1], ".glb")));
        Assert.Throws<InvalidDataException>(() => CloudJobs.ToMesh(new CloudResult([1], ".png")));
    }
}
