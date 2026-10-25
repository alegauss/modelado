using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;

namespace Modelado.Cloud.Providers;

/// <summary>
/// Ideogram (MOD39), over its v2 REST API with the user's key in an Api-Key header, as probed on
/// 2026-10-25: Ideogram 3.0 at POST /v2/image/generate/ideogram-3 (multipart), run with
/// async=true and polled at GET /v2/generations/{id}. Its dry_run asks the same request to be
/// validated and priced without generating or billing, which is how a key is tested for nothing.
/// The options, defaulted to what reconstruction wants:
/// <list type="bullet">
/// <item>count: 1 to 8 candidates, 4 unless set (num_images);</item>
/// <item>speed: turbo, default or quality (rendering_speed), turbo unless set;</item>
/// <item>aspect: 1x1, 4x3, 3x4, 16x9 and the others Ideogram lists (aspect_ratio), 1x1 unless set;</item>
/// <item>style: auto, general, realistic, design, fiction (style_type), realistic unless set;</item>
/// <item>seed: a number, for the same image again.</item>
/// </list>
/// It is priced per image by speed: $0.03, $0.06, $0.09, which a dry run confirmed.
/// </summary>
public sealed class IdeogramProvider : ICloudProvider
{
    private const string GenerationJob = "gen:";
    private static readonly Dictionary<string, double> PerImage = new(StringComparer.Ordinal)
    {
        ["turbo"] = 0.03,
        ["default"] = 0.06,
        ["quality"] = 0.09,
    };

    private readonly HttpClient http;
    private readonly Uri api;

    /// <summary>A provider over a client the caller owns.</summary>
    /// <param name="http">The client.</param>
    /// <param name="api">The API's root; https://api.ideogram.ai unless a test stands one in.</param>
    public IdeogramProvider(HttpClient http, Uri? api = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        this.http = http;
        this.api = api ?? new Uri("https://api.ideogram.ai/");
    }

    /// <inheritdoc/>
    public string Id => "ideogram";

    /// <inheritdoc/>
    public string DisplayName => "Ideogram";

    /// <inheritdoc/>
    public Uri KeyPage => new("https://ideogram.ai/manage-api");

    /// <inheritdoc/>
    public CloudCapability Capabilities => CloudCapability.TextToImage;

    /// <summary>The request's options with the defaults filled in, which is what is sent and priced.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The options.</returns>
    public static IReadOnlyDictionary<string, string> OptionsOf(CloudJobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<string, string> options = new(StringComparer.Ordinal) { ["count"] = "4", ["speed"] = "turbo", ["aspect"] = "1x1", ["style"] = "realistic" };
        foreach ((string name, string value) in request.Options ?? new Dictionary<string, string>())
        {
            options[name] = value;
        }

        return options;
    }

    /// <inheritdoc/>
    public async Task<KeyCheck> ValidateKeyAsync(string key, CancellationToken cancellation)
    {
        using HttpResponseMessage response = await GenerateAsync(key, new CloudJobRequest(CloudCapability.TextToImage, Prompt: "a chair", Options: new Dictionary<string, string> { ["count"] = "1" }), dryRun: true, cancellation).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new KeyCheck(false, "Ideogram refused the key.");
        }

        await RefuseAsync(response, cancellation).ConfigureAwait(false);
        return new KeyCheck(true, "The key works.");
    }

    /// <inheritdoc/>
    public Task<CloudUsage?> UsageAsync(string key, CancellationToken cancellation) => Task.FromResult<CloudUsage?>(null);

    /// <inheritdoc/>
    public CloudCost? EstimateCost(CloudJobRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyDictionary<string, string> options = OptionsOf(request);
        return request.Action == CloudCapability.TextToImage && PerImage.TryGetValue(options["speed"], out double each)
            && int.TryParse(options["count"], NumberStyles.None, CultureInfo.InvariantCulture, out int count)
            ? new CloudCost(Math.Round(each * count, 2), "USD")
            : null;
    }

    /// <inheritdoc/>
    public async Task<string> SubmitAsync(string key, CloudJobRequest request, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Action != CloudCapability.TextToImage)
        {
            throw new InvalidOperationException($"Ideogram cannot do {request.Action}.");
        }

        using HttpResponseMessage response = await GenerateAsync(key, request, dryRun: false, cancellation).ConfigureAwait(false);
        await RefuseAsync(response, cancellation).ConfigureAwait(false);
        JsonNode? started = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false));
        return GenerationJob + (started?["generation_id"]?.GetValue<string>() ?? throw new InvalidDataException("Ideogram accepted the request but named no generation."));
    }

    /// <inheritdoc/>
    public async Task<CloudJobStatus> PollAsync(string key, string jobId, CancellationToken cancellation)
    {
        JsonNode generation = await GenerationAsync(key, jobId, cancellation).ConfigureAwait(false);
        return generation["status"]?.GetValue<string>() switch
        {
            "pending" => new CloudJobStatus(CloudJobState.Running, 0.5, "Drawing"),
            "completed" => new CloudJobStatus(CloudJobState.Succeeded, 1),
            "failed" => new CloudJobStatus(CloudJobState.Failed, 0, generation["failure_reason"]?.GetValue<string>()?.Replace('_', ' ')),
            var other => throw new InvalidDataException($"Ideogram reported a status this build does not know: {other}."),
        };
    }

    /// <summary>Ideogram has no cancel: a generation runs to its end, which is seconds.</summary>
    /// <param name="key">The key.</param>
    /// <param name="jobId">The job.</param>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>A task.</returns>
    public Task CancelAsync(string key, string jobId, CancellationToken cancellation) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task<CloudResult> ResultAsync(string key, string jobId, CancellationToken cancellation)
    {
        JsonNode generation = await GenerationAsync(key, jobId, cancellation).ConfigureAwait(false);
        List<CloudImage> images = [];
        foreach (JsonNode? item in generation["data"]?.AsArray() ?? [])
        {
            // An image Ideogram judged unsafe comes back without its URL.
            if (item?["url"]?.GetValue<string>() is { Length: > 0 } url)
            {
                byte[] bytes = await http.GetByteArrayAsync(new Uri(url), cancellation).ConfigureAwait(false);
                images.Add(new CloudImage(bytes, item["prompt"]?.GetValue<string>() ?? "", item["seed"]?.GetValue<long>()));
            }
        }

        if (images.Count == 0)
        {
            throw new InvalidDataException("Ideogram returned no image it could show.");
        }

        CloudCost? actual = generation["usage_cost_usd_micros"] is JsonNode micros
            ? new CloudCost(double.Parse(micros.ToString(), CultureInfo.InvariantCulture) / 1_000_000, "USD")
            : null;
        return new CloudResult(images[0].Bytes, ".png", actual, images);
    }

    private async Task<JsonNode> GenerationAsync(string key, string jobId, CancellationToken cancellation)
    {
        if (!jobId.StartsWith(GenerationJob, StringComparison.Ordinal) || jobId.Length == GenerationJob.Length)
        {
            throw new ArgumentException($"'{jobId}' is not an Ideogram job.", nameof(jobId));
        }

        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(api, $"v2/generations/{jobId[GenerationJob.Length..]}"));
        request.Headers.Add("Api-Key", key);
        using HttpResponseMessage response = await http.SendAsync(request, cancellation).ConfigureAwait(false);
        await RefuseAsync(response, cancellation).ConfigureAwait(false);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false)) ?? throw new InvalidDataException("Ideogram answered with nothing.");
    }

    private async Task<HttpResponseMessage> GenerateAsync(string key, CloudJobRequest request, bool dryRun, CancellationToken cancellation)
    {
        IReadOnlyDictionary<string, string> options = OptionsOf(request);
        using HttpRequestMessage message = new(HttpMethod.Post, new Uri(api, dryRun ? "v2/image/generate/ideogram-3?dry_run=true" : "v2/image/generate/ideogram-3"));
        message.Headers.Add("Api-Key", key);
        MultipartFormDataContent form = new()
        {
            { new StringContent(request.Prompt ?? throw new ArgumentException("Text to image needs a prompt.", nameof(request))), "prompt" },
            { new StringContent(options["count"]), "num_images" },
            { new StringContent(options["speed"]), "rendering_speed" },
            { new StringContent(options["aspect"]), "aspect_ratio" },
            { new StringContent(options["style"]), "style_type" },
        };
        if (options.TryGetValue("seed", out string? seed))
        {
            form.Add(new StringContent(seed), "seed");
        }

        if (!dryRun)
        {
            form.Add(new StringContent("true"), "async");
        }

        message.Content = form;
        return await http.SendAsync(message, cancellation).ConfigureAwait(false);
    }

    // What Ideogram said, which names the problem (a prompt refused for safety, an option out of
    // range) better than a status code; never the key, which is only in the request's header.
    private static async Task RefuseAsync(HttpResponseMessage response, CancellationToken cancellation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string said = "";
        try
        {
            JsonNode? body = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false));
            said = body?["detail"]?.GetValue<string>() ?? body?["message"]?.GetValue<string>() ?? body?["error"]?.GetValue<string>() ?? "";
        }
        catch (Exception unreadable) when (unreadable is System.Text.Json.JsonException or InvalidOperationException)
        {
            // Not JSON; the status says enough.
        }

        throw new HttpRequestException($"Ideogram answered {(int)response.StatusCode}{(said.Length > 0 ? $": {said}" : ".")}", null, response.StatusCode);
    }
}
