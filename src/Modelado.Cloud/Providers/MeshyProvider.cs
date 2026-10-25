using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Modelado.Cloud.Providers;

/// <summary>
/// Meshy AI (MOD37), over its REST API with the user's Bearer key, as documented at
/// docs.meshy.ai on 2026-10-25: image to 3D at /openapi/v1/image-to-3d, text to 3D at
/// /openapi/v2/text-to-3d, the balance at /openapi/v1/balance. Each action's options are the
/// API's own, defaulted to Meshy's defaults:
/// <list type="bullet">
/// <item>texture: true or false (should_texture; for text, whether the preview is refined);</item>
/// <item>pbr: true or false (enable_pbr);</item>
/// <item>topology: triangle or quad;</item>
/// <item>polycount: a number (target_polycount, which turns remeshing on);</item>
/// <item>model: latest, meshy-7.1, meshy-6 or meshy-6-lite (ai_model).</item>
/// </list>
/// A text-to-3D job is Meshy's preview and, with a texture, the refine Meshy runs on it: one job
/// here, its id the preview's, the refine submitted when the preview succeeds. Meshy has no
/// cancel: DELETE refunds a task still pending and is refused for one running, which runs on.
/// </summary>
public sealed class MeshyProvider : ICloudProvider
{
    private const string ImageJob = "image:", TextJob = "text:";
    private static readonly Lazy<CloudPrices> Prices = new(() =>
    {
        using Stream stream = typeof(MeshyProvider).Assembly.GetManifestResourceStream("Modelado.Cloud.Providers.meshy-prices.json")!;
        using StreamReader reader = new(stream);
        return CloudPrices.Parse(reader.ReadToEnd());
    });

    private readonly HttpClient http;
    private readonly Uri api;

    // A text job's refine, by its preview's id, once the preview has succeeded.
    private readonly ConcurrentDictionary<string, string> refines = new(StringComparer.Ordinal);

    /// <summary>A provider over a client the caller owns.</summary>
    /// <param name="http">The client.</param>
    /// <param name="api">The API's root; https://api.meshy.ai unless a test stands one in.</param>
    public MeshyProvider(HttpClient http, Uri? api = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        this.http = http;
        this.api = api ?? new Uri("https://api.meshy.ai/");
    }

    /// <summary>The dated price table estimates come from.</summary>
    public static CloudPrices PriceTable => Prices.Value;

    /// <inheritdoc/>
    public string Id => "meshy";

    /// <inheritdoc/>
    public string DisplayName => "Meshy AI";

    /// <inheritdoc/>
    public Uri KeyPage => new("https://www.meshy.ai/settings/api");

    /// <inheritdoc/>
    public CloudCapability Capabilities => CloudCapability.ImageTo3D | CloudCapability.TextTo3D;

    /// <summary>The request's options with Meshy's defaults filled in, which is what is sent and priced.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The options.</returns>
    public static IReadOnlyDictionary<string, string> OptionsOf(CloudJobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<string, string> options = new(StringComparer.Ordinal) { ["texture"] = "true", ["pbr"] = "false", ["topology"] = "triangle", ["model"] = "latest" };
        foreach ((string name, string value) in request.Options ?? new Dictionary<string, string>())
        {
            options[name] = value;
        }

        return options;
    }

    /// <inheritdoc/>
    public async Task<KeyCheck> ValidateKeyAsync(string key, CancellationToken cancellation)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "openapi/v1/balance", key, null, cancellation).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new KeyCheck(false, "Meshy AI refused the key.");
        }

        response.EnsureSuccessStatusCode();
        JsonNode? balance = await ReadAsync(response, cancellation).ConfigureAwait(false);
        return new KeyCheck(true, $"The key works: {Credits(balance)} credits.");
    }

    /// <inheritdoc/>
    public async Task<CloudUsage?> UsageAsync(string key, CancellationToken cancellation)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "openapi/v1/balance", key, null, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        JsonNode? balance = await ReadAsync(response, cancellation).ConfigureAwait(false);
        double credits = Credits(balance);
        return new CloudUsage(string.Create(CultureInfo.InvariantCulture, $"{credits:0} credits"), credits);
    }

    /// <inheritdoc/>
    public CloudCost? EstimateCost(CloudJobRequest request) =>
        PriceTable.Estimate(request with { Options = OptionsOf(request) });

    /// <inheritdoc/>
    public async Task<string> SubmitAsync(string key, CloudJobRequest request, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyDictionary<string, string> options = OptionsOf(request);
        JsonObject body = new() { ["ai_model"] = options["model"], ["topology"] = options["topology"], ["target_formats"] = new JsonArray("glb") };
        if (options.TryGetValue("polycount", out string? polycount))
        {
            body["should_remesh"] = true;
            body["target_polycount"] = int.Parse(polycount, CultureInfo.InvariantCulture);
        }

        string path;
        if (request.Action == CloudCapability.ImageTo3D)
        {
            byte[] image = request.Image ?? throw new ArgumentException("Image to 3D needs an image.", nameof(request));
            body["image_url"] = $"data:{MimeOf(image)};base64,{Convert.ToBase64String(image)}";
            body["should_texture"] = options["texture"] == "true";
            body["enable_pbr"] = options["texture"] == "true" && options["pbr"] == "true";
            path = "openapi/v1/image-to-3d";
        }
        else if (request.Action == CloudCapability.TextTo3D)
        {
            body["mode"] = "preview";
            body["prompt"] = request.Prompt ?? throw new ArgumentException("Text to 3D needs a prompt.", nameof(request));
            path = "openapi/v2/text-to-3d";
        }
        else
        {
            throw new InvalidOperationException($"Meshy AI cannot do {request.Action}.");
        }

        string id = await CreateAsync(path, key, body, cancellation).ConfigureAwait(false);
        string job = (request.Action == CloudCapability.ImageTo3D ? ImageJob : TextJob) + id;
        if (request.Action == CloudCapability.TextTo3D && options["texture"] == "true")
        {
            // Marked for refining: the refine is submitted once the preview has succeeded.
            refines[id] = options["pbr"] == "true" ? "pending-pbr" : "pending";
        }

        return job;
    }

    /// <inheritdoc/>
    public async Task<CloudJobStatus> PollAsync(string key, string jobId, CancellationToken cancellation)
    {
        (string path, string id, bool text) = Parse(jobId);
        if (text && refines.TryGetValue(id, out string? refine))
        {
            if (refine.StartsWith("pending", StringComparison.Ordinal))
            {
                JsonNode preview = await TaskAsync(path, id, key, cancellation).ConfigureAwait(false);
                CloudJobStatus first = StatusOf(preview);
                if (first.State != CloudJobState.Succeeded)
                {
                    return first with { Progress = first.Progress / 2 };
                }

                JsonObject body = new() { ["mode"] = "refine", ["preview_task_id"] = id, ["enable_pbr"] = refine == "pending-pbr", ["target_formats"] = new JsonArray("glb") };
                refines[id] = await CreateAsync(path, key, body, cancellation).ConfigureAwait(false);
                return new CloudJobStatus(CloudJobState.Running, 0.5, "Texturing");
            }

            CloudJobStatus second = StatusOf(await TaskAsync(path, refine, key, cancellation).ConfigureAwait(false));
            return second with { Progress = 0.5 + (second.Progress / 2) };
        }

        return StatusOf(await TaskAsync(path, id, key, cancellation).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async Task CancelAsync(string key, string jobId, CancellationToken cancellation)
    {
        (string path, string id, _) = Parse(jobId);
        string task = refines.TryGetValue(id, out string? refine) && !refine.StartsWith("pending", StringComparison.Ordinal) ? refine : id;
        using HttpResponseMessage response = await SendAsync(HttpMethod.Delete, $"{path}/{task}", key, null, cancellation).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException("Meshy AI runs a task it has started to the end; it cannot be stopped.");
        }

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task<CloudResult> ResultAsync(string key, string jobId, CancellationToken cancellation)
    {
        (string path, string id, _) = Parse(jobId);
        JsonNode task = await TaskAsync(path, id, key, cancellation).ConfigureAwait(false);
        double credits = task["consumed_credits"]?.GetValue<double>() ?? 0;
        if (refines.TryGetValue(id, out string? refine) && !refine.StartsWith("pending", StringComparison.Ordinal))
        {
            task = await TaskAsync(path, refine, key, cancellation).ConfigureAwait(false);
            credits += task["consumed_credits"]?.GetValue<double>() ?? 0;
        }

        string url = task["model_urls"]?["glb"]?.GetValue<string>() ?? throw new InvalidDataException("Meshy AI's task has no GLB.");
        byte[] glb = await http.GetByteArrayAsync(new Uri(url), cancellation).ConfigureAwait(false);
        refines.TryRemove(id, out _);
        return new CloudResult(glb, ".glb", new CloudCost(credits, "credits"));
    }

    private static CloudJobStatus StatusOf(JsonNode task)
    {
        double progress = (task["progress"]?.GetValue<double>() ?? 0) / 100;
        string? error = task["task_error"]?["message"]?.GetValue<string>();
        return task["status"]?.GetValue<string>() switch
        {
            "PENDING" => new CloudJobStatus(CloudJobState.Queued, progress),
            "IN_PROGRESS" => new CloudJobStatus(CloudJobState.Running, progress),
            "SUCCEEDED" => new CloudJobStatus(CloudJobState.Succeeded, 1),
            "FAILED" => new CloudJobStatus(CloudJobState.Failed, progress, string.IsNullOrEmpty(error) ? null : error),
            "CANCELED" => new CloudJobStatus(CloudJobState.Cancelled, progress),
            var other => throw new InvalidDataException($"Meshy AI reported a status this build does not know: {other}."),
        };
    }

    private static (string Path, string Id, bool Text) Parse(string jobId) =>
        jobId.StartsWith(ImageJob, StringComparison.Ordinal) ? ("openapi/v1/image-to-3d", jobId[ImageJob.Length..], false)
        : jobId.StartsWith(TextJob, StringComparison.Ordinal) ? ("openapi/v2/text-to-3d", jobId[TextJob.Length..], true)
        : throw new ArgumentException($"'{jobId}' is not a Meshy AI job.", nameof(jobId));

    private static double Credits(JsonNode? balance) => balance?["balance"]?.GetValue<double>() ?? 0;

    private static string MimeOf(byte[] image) => image switch
    {
        [0x89, 0x50, 0x4E, 0x47, ..] => "image/png",
        [0xFF, 0xD8, ..] => "image/jpeg",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => "image/webp",
        _ => throw new ArgumentException("Meshy AI takes a PNG, JPEG or WebP image.", nameof(image)),
    };

    private async Task<string> CreateAsync(string path, string key, JsonObject body, CancellationToken cancellation)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Post, path, key, body, cancellation).ConfigureAwait(false);
        await RefuseAsync(response, cancellation).ConfigureAwait(false);
        JsonNode? created = await ReadAsync(response, cancellation).ConfigureAwait(false);
        return created?["result"]?.GetValue<string>() ?? throw new InvalidDataException("Meshy AI accepted the task but named no id.");
    }

    private async Task<JsonNode> TaskAsync(string path, string id, string key, CancellationToken cancellation)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, $"{path}/{id}", key, null, cancellation).ConfigureAwait(false);
        await RefuseAsync(response, cancellation).ConfigureAwait(false);
        return await ReadAsync(response, cancellation).ConfigureAwait(false) ?? throw new InvalidDataException("Meshy AI answered with no task.");
    }

    // A refusal says what Meshy said, which names the problem (credits, a bad image) better than
    // a status code; never the key, which is only ever in the request's header.
    private static async Task RefuseAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string said = "";
        try
        {
            said = (await ReadAsync(response, cancellation).ConfigureAwait(false))?["message"]?.GetValue<string>() ?? "";
        }
        catch (Exception unreadable) when (unreadable is JsonException or InvalidOperationException)
        {
            // Not JSON; the status says enough.
        }

        throw new HttpRequestException($"Meshy AI answered {(int)response.StatusCode}{(said.Length > 0 ? $": {said}" : ".")}", null, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string key, JsonObject? body, CancellationToken cancellation)
    {
        using HttpRequestMessage request = new(method, new Uri(api, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await http.SendAsync(request, cancellation).ConfigureAwait(false);
    }

    private static async Task<JsonNode?> ReadAsync(HttpResponseMessage response, CancellationToken cancellation) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false));
}
