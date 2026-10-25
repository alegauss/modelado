namespace Modelado.Cloud;

/// <summary>What a cloud service can be asked to do (MOD36).</summary>
[Flags]
public enum CloudCapability
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>A photo becomes a 3D model.</summary>
    ImageTo3D = 1,

    /// <summary>A prompt becomes a 3D model.</summary>
    TextTo3D = 2,

    /// <summary>A model gets new topology.</summary>
    Remesh = 4,

    /// <summary>A model gets a new texture from a prompt.</summary>
    Retexture = 8,

    /// <summary>A prompt becomes an image.</summary>
    TextToImage = 16,

    /// <summary>A prompt becomes a seamless texture or material image.</summary>
    Texture = 32,
}

/// <summary>One job's input: the action and what it works from.</summary>
/// <param name="Action">One capability.</param>
/// <param name="Prompt">The text, for the actions that take one.</param>
/// <param name="Image">An encoded image (PNG, JPEG), for the actions that take one.</param>
/// <param name="Model">A GLB, for the actions that work on a model.</param>
/// <param name="Options">The service's own options by name, e.g. "texture": "true".</param>
public sealed record CloudJobRequest(
    CloudCapability Action,
    string? Prompt = null,
    byte[]? Image = null,
    byte[]? Model = null,
    IReadOnlyDictionary<string, string>? Options = null);

/// <summary>Where a job is.</summary>
public enum CloudJobState
{
    /// <summary>Accepted and waiting.</summary>
    Queued,

    /// <summary>Being worked on.</summary>
    Running,

    /// <summary>Done; its result can be fetched.</summary>
    Succeeded,

    /// <summary>The service gave up on it.</summary>
    Failed,

    /// <summary>Cancelled, by the user or the service.</summary>
    Cancelled,
}

/// <summary>A job's state as the service reports it.</summary>
/// <param name="State">Where it is.</param>
/// <param name="Progress">0 to 1, as far as the service says.</param>
/// <param name="Message">The service's words, for a failure in particular.</param>
/// <param name="NextJobId">
/// The id the job goes on under, where it moved to another task (Meshy's refine after its
/// preview), so a job kept across a restart is resumed where it is now.
/// </param>
public sealed record CloudJobStatus(CloudJobState State, double Progress = 0, string? Message = null, string? NextJobId = null)
{
    /// <summary>True once the job will not change again.</summary>
    public bool Finished => State is CloudJobState.Succeeded or CloudJobState.Failed or CloudJobState.Cancelled;
}

/// <summary>What a finished job made.</summary>
/// <param name="Bytes">The file.</param>
/// <param name="Extension">Its kind: ".glb" for a model, ".png" or ".jpg" for an image.</param>
/// <param name="Actual">MOD42: what the job cost, where the service reports it; the estimate stands in otherwise.</param>
/// <param name="Images">MOD39: every candidate an image job made, the first of them being <paramref name="Bytes"/>.</param>
public sealed record CloudResult(byte[] Bytes, string Extension, CloudCost? Actual = null, IReadOnlyList<CloudImage>? Images = null)
{
    /// <summary>True for a model, false for an image.</summary>
    public bool IsModel => string.Equals(Extension, ".glb", StringComparison.OrdinalIgnoreCase);
}

/// <summary>What testing a key came to.</summary>
/// <param name="Valid">The service accepted it.</param>
/// <param name="Message">Why not, or what it says about the account.</param>
public sealed record KeyCheck(bool Valid, string Message);

/// <summary>An account's standing with a service, for MOD41's status.</summary>
/// <param name="Summary">The service's own words, e.g. "1,240 credits".</param>
/// <param name="Remaining">What is left, in the service's unit, where it says.</param>
public sealed record CloudUsage(string Summary, double? Remaining);

/// <summary>What an action is expected to cost before it runs, for MOD42.</summary>
/// <param name="Amount">How much, in the service's unit.</param>
/// <param name="Unit">"credits", "USD" and so on.</param>
public sealed record CloudCost(double Amount, string Unit)
{
    /// <summary>How a person reads it: "$0.12" for dollars, "30 credits" for credits.</summary>
    public override string ToString() => Unit == "USD"
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"${Amount:0.00}")
        : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Amount:0.##} {Unit}");
}

/// <summary>One image a service made, with what made it (MOD39).</summary>
/// <param name="Bytes">The encoded image.</param>
/// <param name="Prompt">The prompt it was made from, as the service rewrote it.</param>
/// <param name="Seed">The seed that remakes it, where the service gives one.</param>
public sealed record CloudImage(byte[] Bytes, string Prompt, long? Seed);

/// <summary>
/// One cloud service (MOD36). A new service is one class behind this interface, not a new screen:
/// About lists every registered provider with a key field, and each action the app offers asks
/// which providers have that capability. Every such service runs jobs asynchronously, so a job
/// is submitted, polled, cancelled and fetched through one shape, which <see cref="CloudJobs"/>
/// drives. Nothing here is called without a key the user stored and an action the user started.
/// </summary>
public interface ICloudProvider
{
    /// <summary>The stable id keys are stored under, e.g. "meshy".</summary>
    string Id { get; }

    /// <summary>The name a person reads, e.g. "Meshy AI".</summary>
    string DisplayName { get; }

    /// <summary>Where a key is made.</summary>
    Uri KeyPage { get; }

    /// <summary>What it can do.</summary>
    CloudCapability Capabilities { get; }

    /// <summary>MOD55: the dated price table its estimates come from, or null where it has none.</summary>
    CloudPrices? Prices => null;

    /// <summary>Asks the service whether a key works.</summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The answer.</returns>
    Task<KeyCheck> ValidateKeyAsync(string key, CancellationToken cancellation);

    /// <summary>The account's standing, or null where the service does not say.</summary>
    /// <param name="key">The key.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The usage.</returns>
    Task<CloudUsage?> UsageAsync(string key, CancellationToken cancellation);

    /// <summary>What an action would cost, or null where it cannot be known before it runs.</summary>
    /// <param name="request">The action.</param>
    /// <returns>The estimate.</returns>
    CloudCost? EstimateCost(CloudJobRequest request);

    /// <summary>Starts a job.</summary>
    /// <param name="key">The key.</param>
    /// <param name="request">What to do.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The service's id for the job.</returns>
    Task<string> SubmitAsync(string key, CloudJobRequest request, CancellationToken cancellation);

    /// <summary>Where a job is.</summary>
    /// <param name="key">The key.</param>
    /// <param name="jobId">The job.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>Its status.</returns>
    Task<CloudJobStatus> PollAsync(string key, string jobId, CancellationToken cancellation);

    /// <summary>Asks the service to stop a job.</summary>
    /// <param name="key">The key.</param>
    /// <param name="jobId">The job.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task.</returns>
    Task CancelAsync(string key, string jobId, CancellationToken cancellation);

    /// <summary>Fetches a finished job's result.</summary>
    /// <param name="key">The key.</param>
    /// <param name="jobId">The job.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The result.</returns>
    Task<CloudResult> ResultAsync(string key, string jobId, CancellationToken cancellation);
}
