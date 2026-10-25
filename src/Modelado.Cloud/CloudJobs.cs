using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Cloud;

/// <summary>
/// Runs a cloud job from submit to result (MOD36): submits it, polls until it finishes, reports
/// each status for the same job panel the local operations use, and fetches what it made. A
/// cancellation asks the service to stop the job before it is given up here, so a cancelled job
/// does not keep spending credits.
/// </summary>
public static class CloudJobs
{
    /// <summary>How often a job is polled unless a caller says otherwise.</summary>
    public static readonly TimeSpan DefaultPoll = TimeSpan.FromSeconds(3);

    /// <summary>Runs one job.</summary>
    /// <param name="provider">The service.</param>
    /// <param name="key">The user's key for it.</param>
    /// <param name="request">What to do.</param>
    /// <param name="progress">Told each status.</param>
    /// <param name="cancellation">Cancels it, at the service too.</param>
    /// <param name="poll">How long between polls.</param>
    /// <param name="spending">MOD41: the ledger every job is recorded in and the budgets that refuse one.</param>
    /// <param name="tracked">Told the job's id when it is submitted and each time it moves, so a caller can keep it across a restart.</param>
    /// <returns>What the job made.</returns>
    /// <exception cref="InvalidOperationException">The service failed or cancelled the job.</exception>
    /// <exception cref="BudgetExceededException">The service's monthly budget is spent; nothing was submitted.</exception>
    public static async Task<CloudResult> RunAsync(
        ICloudProvider provider, string key, CloudJobRequest request, IProgress<CloudJobStatus>? progress, CancellationToken cancellation, TimeSpan? poll = null, CloudSpending? spending = null, Action<string>? tracked = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);
        if ((provider.Capabilities & request.Action) != request.Action || request.Action == CloudCapability.None)
        {
            throw new InvalidOperationException($"{provider.DisplayName} cannot do {request.Action}.");
        }

        // MOD42: refused before anything is uploaded, when the budget is spent or the action's
        // estimate would cross it.
        CloudCost? estimate = provider.EstimateCost(request);
        if (spending?.Refusal(provider.Id, estimate) is { } why)
        {
            throw new BudgetExceededException($"{provider.DisplayName}'s budget for this month is {why}; raise it in Cloud usage to run this.");
        }

        string job = await provider.SubmitAsync(key, request, cancellation).ConfigureAwait(false);
        tracked?.Invoke(job);
        progress?.Report(new CloudJobStatus(CloudJobState.Queued));
        return await FollowAsync(provider, key, request.Action, job, estimate, progress, cancellation, poll, spending, tracked).ConfigureAwait(false);
    }

    /// <summary>
    /// Follows a job submitted before (MOD37), by the id <c>tracked</c> last gave, as the app does
    /// at start for one that was running when it closed: polled to its end and fetched as if it
    /// had never stopped.
    /// </summary>
    /// <param name="provider">The service.</param>
    /// <param name="key">The user's key for it.</param>
    /// <param name="action">What it was asked to do.</param>
    /// <param name="job">Its id.</param>
    /// <param name="progress">Told each status.</param>
    /// <param name="cancellation">Cancels it, at the service too.</param>
    /// <param name="poll">How long between polls.</param>
    /// <param name="spending">Where it is recorded when it ends.</param>
    /// <param name="tracked">Told each time it moves.</param>
    /// <returns>What the job made.</returns>
    public static Task<CloudResult> ResumeAsync(
        ICloudProvider provider, string key, CloudCapability action, string job, IProgress<CloudJobStatus>? progress, CancellationToken cancellation, TimeSpan? poll = null, CloudSpending? spending = null, Action<string>? tracked = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrEmpty(job);
        return FollowAsync(provider, key, action, job, null, progress, cancellation, poll, spending, tracked);
    }

    private static async Task<CloudResult> FollowAsync(
        ICloudProvider provider, string key, CloudCapability action, string job, CloudCost? estimate, IProgress<CloudJobStatus>? progress, CancellationToken cancellation, TimeSpan? poll, CloudSpending? spending, Action<string>? tracked)
    {
        try
        {
            while (true)
            {
                CloudJobStatus status = await provider.PollAsync(key, job, cancellation).ConfigureAwait(false);
                if (status.NextJobId is { } moved && moved != job)
                {
                    job = moved;
                    tracked?.Invoke(job);
                }

                progress?.Report(status);
                switch (status.State)
                {
                    case CloudJobState.Succeeded:
                        CloudResult result = await provider.ResultAsync(key, job, cancellation).ConfigureAwait(false);
                        spending?.Record(provider.Id, action, estimate, result.Actual ?? estimate, "succeeded");
                        return result;
                    case CloudJobState.Failed or CloudJobState.Cancelled:
                        string outcome = status.State == CloudJobState.Failed ? "failed" : "cancelled";
                        spending?.Record(provider.Id, action, estimate, null, outcome);
                        throw new InvalidOperationException($"{provider.DisplayName} {outcome} the job{(status.Message is null ? "." : $": {status.Message}")}");
                }

                await Task.Delay(poll ?? DefaultPoll, cancellation).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Tell the service, so the job stops spending; that this cannot be cancelled too is
            // the point, and a failure to reach it changes nothing the user can do.
            try
            {
                await provider.CancelAsync(key, job, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ignored) when (ignored is HttpRequestException or IOException or InvalidOperationException)
            {
                // The job may run on at the service; there is nothing more to do from here.
            }

            spending?.Record(provider.Id, action, estimate, null, "cancelled");
            throw;
        }
    }

    /// <summary>The history label for a version a service made, e.g. "Meshy AI: image to 3D".</summary>
    /// <param name="provider">The service.</param>
    /// <param name="action">What it did.</param>
    /// <returns>The label.</returns>
    public static string Label(ICloudProvider provider, CloudCapability action)
    {
        ArgumentNullException.ThrowIfNull(provider);
        string words = action switch
        {
            CloudCapability.ImageTo3D => "image to 3D",
            CloudCapability.TextTo3D => "text to 3D",
            CloudCapability.Remesh => "remesh",
            CloudCapability.Retexture => "retexture",
            CloudCapability.TextToImage => "text to image",
            CloudCapability.Texture => "texture",
            _ => action.ToString(),
        };
        return $"{provider.DisplayName}: {words}";
    }

    /// <summary>A model result as a mesh, to enter the history like any other version.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The mesh.</returns>
    /// <exception cref="InvalidDataException">It is not a GLB this build reads.</exception>
    public static Mesh ToMesh(CloudResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsModel ? MeshImport.ReadGlb(result.Bytes) : throw new InvalidDataException("The service returned an image, not a model.");
    }

    /// <summary>An image result, decoded.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The image.</returns>
    /// <exception cref="InvalidDataException">It is a model, or not an image this build reads.</exception>
    public static RgbaImage ToImage(CloudResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsModel)
        {
            throw new InvalidDataException("The service returned a model, not an image.");
        }

        using MemoryStream stream = new(result.Bytes);
        return ImageCodec.Decode(stream, "the service's image");
    }
}
