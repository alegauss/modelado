using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modelado.Inference;

/// <summary>One file the manifest lists: where it is, and what it must be.</summary>
/// <param name="Url">A resolve/&lt;commit&gt;/ URL, so its bytes cannot change under the app.</param>
/// <param name="Bytes">Its size.</param>
/// <param name="Sha256">Its SHA-256, lowercase hex.</param>
public sealed record ManifestFile(string Url, long Bytes, string Sha256);

/// <summary>One model the manifest lists.</summary>
/// <param name="Name">Its folder name.</param>
/// <param name="Task">What it does, as its meta.json says.</param>
/// <param name="Meta">Its meta.json.</param>
/// <param name="Variants">Its ONNX files, by variant.</param>
/// <param name="License">Its license, as the download panel shows it.</param>
/// <param name="LicenseUrl">The license text.</param>
public sealed record ManifestModel(string Name, string Task, ManifestFile Meta, IReadOnlyDictionary<string, ManifestFile> Variants, string License, string LicenseUrl);

/// <summary>scripts/build_manifest.py's manifest.json, schema 1.</summary>
/// <param name="Schema">The format version.</param>
/// <param name="Version">The manifest's own version.</param>
/// <param name="Models">The models it lists.</param>
public sealed record ModelManifest(int Schema, string Version, IReadOnlyList<ManifestModel> Models)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        RespectRequiredConstructorParameters = true,
    };

    /// <summary>Reads a manifest and refuses one this build cannot trust.</summary>
    /// <param name="json">manifest.json's text.</param>
    /// <param name="loopback">Also accept plain http on this machine's loopback, for tests and a local mirror.</param>
    /// <returns>The manifest.</returns>
    /// <exception cref="InvalidDataException">It is not schema 1, or a URL is not https.</exception>
    public static ModelManifest Parse(string json, bool loopback = false)
    {
        ModelManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ModelManifest>(json, Options) ?? throw new InvalidDataException("The manifest is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"The manifest is not valid: {error.Message}", error);
        }

        if (manifest.Schema != 1)
        {
            throw new InvalidDataException($"The manifest is schema {manifest.Schema}; this build reads 1.");
        }

        if (manifest.Models.SelectMany(m => m.Variants.Values.Append(m.Meta)).FirstOrDefault(f => !Trusted(f.Url, loopback)) is { } plain)
        {
            throw new InvalidDataException($"The manifest lists {plain.Url}, which is not https.");
        }

        return manifest;
    }

    /// <summary>A model by name, or null.</summary>
    /// <param name="name">Its folder name.</param>
    /// <returns>The model.</returns>
    public ManifestModel? Model(string name) => Models.FirstOrDefault(m => m.Name == name);

    private static bool Trusted(string url, bool loopback) =>
        url.StartsWith("https://", StringComparison.Ordinal)
        || (loopback && Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) && parsed.Scheme == Uri.UriSchemeHttp && parsed.IsLoopback);
}

/// <summary>A model and the variant of it wanted.</summary>
/// <param name="Model">The model's folder name.</param>
/// <param name="Variant">fp32 or fp16; a model the manifest has only in fp32 is fetched in fp32.</param>
public readonly record struct ModelRequest(string Model, string Variant);

/// <summary>What a download is doing.</summary>
public enum DownloadStage
{
    /// <summary>Fetching bytes.</summary>
    Downloading,

    /// <summary>Checking a fetched file's SHA-256.</summary>
    Verifying,

    /// <summary>Waiting to try again after a network failure.</summary>
    Retrying,
}

/// <summary>How far a download is, for the progress bar and its words.</summary>
/// <param name="File">The file being fetched, e.g. triposr-encoder/fp16.onnx.</param>
/// <param name="Done">Bytes on disk across the whole download, resumed bytes included.</param>
/// <param name="Total">Bytes the whole download needs.</param>
/// <param name="Stage">Fetching, verifying or waiting to retry.</param>
/// <param name="BytesPerSecond">The recent speed, or 0 before there is one.</param>
public readonly record struct DownloadProgress(string File, long Done, long Total, DownloadStage Stage = DownloadStage.Downloading, double BytesPerSecond = 0)
{
    /// <summary>The time left at the recent speed, or null before there is a speed.</summary>
    public TimeSpan? Remaining => BytesPerSecond > 0 ? TimeSpan.FromSeconds((Total - Done) / BytesPerSecond) : null;
}

/// <summary>What a download did.</summary>
/// <param name="Fetched">Files fetched and verified.</param>
/// <param name="Present">Files already on disk with the right bytes.</param>
public sealed record DownloadResult(int Fetched, int Present);

/// <summary>
/// Models fetched from the manifest scripts/publish_models.py put on Hugging Face. Each file is
/// streamed to a .part file, checked against the manifest's size and SHA-256, and only then
/// renamed into place, so ONNX Runtime never loads a file that arrived corrupt. A file already
/// present with the right bytes is not fetched; an interrupted one resumes by HTTP Range; a
/// network failure is retried with a growing pause before it is reported.
/// </summary>
public static class ModelDownloads
{
    /// <summary>
    /// The manifest fetched when this build embeds none: the repository's latest. Its files are
    /// pinned to a commit inside it, so a newer manifest can add models but never change bytes
    /// the app already verified. publish_models.py embeds the pinned copy for release builds.
    /// </summary>
    public const string LatestManifestUrl = "https://huggingface.co/alegauss/modelado-models/resolve/main/manifest.json";

    /// <summary>How many times a file is tried after a network failure before it is reported.</summary>
    public const int Attempts = 4;

    /// <summary>How long the connection may stay silent before the attempt is given up and retried.</summary>
    public static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

    private static readonly Lazy<ModelManifest?> EmbeddedManifest = new(() =>
    {
        using Stream? stream = typeof(ModelDownloads).Assembly.GetManifestResourceStream("Modelado.Inference.models-manifest.json");
        if (stream is null)
        {
            return null;
        }

        using StreamReader reader = new(stream);
        ModelManifest manifest = ModelManifest.Parse(reader.ReadToEnd());
        return manifest.Models.Count > 0 ? manifest : null;
    });

    /// <summary>The pinned manifest this build carries, or null before the models were first published.</summary>
    public static ModelManifest? Embedded => EmbeddedManifest.Value;

    /// <summary>The embedded manifest, or the latest one fetched from <paramref name="url"/>.</summary>
    /// <param name="http">The client.</param>
    /// <param name="url">A manifest to use instead of the embedded one, or null.</param>
    /// <param name="cancellation">Cancels the fetch.</param>
    /// <returns>The manifest.</returns>
    public static async Task<ModelManifest> LoadManifestAsync(HttpClient http, string? url, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (url is null && Embedded is { } embedded)
        {
            return embedded;
        }

        Uri source = new(url ?? LatestManifestUrl);
        string json = await http.GetStringAsync(source, cancellation).ConfigureAwait(false);
        return ModelManifest.Parse(json, loopback: source.IsLoopback);
    }

    /// <summary>The variant the manifest has for a request: the one asked for, else fp32.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="request">The model and variant wanted.</param>
    /// <returns>The variant, or null when the manifest does not list the model.</returns>
    public static string? VariantOf(ModelManifest manifest, ModelRequest request)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ManifestModel? model = manifest.Model(request.Model);
        return model is null ? null : model.Variants.ContainsKey(request.Variant) ? request.Variant : "fp32";
    }

    /// <summary>Bytes the requests need in all, meta.json files included.</summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="requests">The models and variants.</param>
    /// <returns>The total.</returns>
    public static long TotalBytes(ModelManifest manifest, IReadOnlyCollection<ModelRequest> requests) =>
        Files(manifest, "", requests).Sum(f => f.Expected.Bytes);

    /// <summary>
    /// Bytes still to fetch, by size alone: cheap enough for every refresh of a card, where hashing
    /// 800 MB is not. The download itself still hashes.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="root">The folder downloads go to.</param>
    /// <param name="requests">The models and variants.</param>
    /// <param name="elsewhere">Other models folders whose file of the same size counts.</param>
    /// <returns>The bytes still to fetch.</returns>
    public static long PendingBytes(ModelManifest manifest, string root, IReadOnlyCollection<ModelRequest> requests, IReadOnlyList<string>? elsewhere = null) =>
        Files(manifest, root, requests)
            .Where(file => !Sized(file.Path, file.Expected) && !(elsewhere ?? []).Any(other => Sized(Path.Combine(other, file.Label), file.Expected)))
            .Sum(file => file.Expected.Bytes - Math.Min(PartialBytes(file.Path), file.Expected.Bytes));

    /// <summary>Fetches every file the requests need that is not on disk with the right bytes.</summary>
    /// <param name="http">The client.</param>
    /// <param name="manifest">The manifest.</param>
    /// <param name="root">The folder downloads go to.</param>
    /// <param name="requests">The models and variants.</param>
    /// <param name="progress">Told after each chunk, and when a file is verified or retried.</param>
    /// <param name="cancellation">Cancels between chunks; the .part file stays, so a later download resumes it.</param>
    /// <param name="elsewhere">Other models folders: a file already there with the manifest's bytes is not fetched.</param>
    /// <param name="retryDelay">The first pause before a retry, doubled each time; one second unless set.</param>
    /// <returns>How many files were fetched and how many were already there.</returns>
    /// <exception cref="InvalidDataException">A file arrived with another size or SHA-256; it was deleted.</exception>
    /// <exception cref="HttpRequestException">The server could not be reached after every attempt.</exception>
    public static async Task<DownloadResult> DownloadAsync(
        HttpClient http,
        ModelManifest manifest,
        string root,
        IReadOnlyCollection<ModelRequest> requests,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellation,
        IReadOnlyList<string>? elsewhere = null,
        TimeSpan? retryDelay = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(requests);
        long total = TotalBytes(manifest, requests);
        long done = 0;
        int fetched = 0, present = 0;
        Speed speed = new();
        foreach ((string path, string label, ManifestFile expected) in Files(manifest, root, requests))
        {
            if (Valid(path, expected) || (elsewhere ?? []).Any(other => Valid(Path.Combine(other, label), expected)))
            {
                present++;
                done += expected.Bytes;
                progress?.Report(new DownloadProgress(label, done, total));
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string part = path + ".part";
            long before = done;
            TimeSpan pause = retryDelay ?? TimeSpan.FromSeconds(1);
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await FetchAsync(http, expected, part, label, before, total, speed, progress, cancellation).ConfigureAwait(false);
                    break;
                }
                catch (Exception failure) when (Transient(failure) && !cancellation.IsCancellationRequested && attempt < Attempts)
                {
                    progress?.Report(new DownloadProgress(label, before + PartialBytes(path), total, DownloadStage.Retrying));
                    await Task.Delay(pause, cancellation).ConfigureAwait(false);
                    pause *= 2;
                }
            }

            progress?.Report(new DownloadProgress(label, before + expected.Bytes, total, DownloadStage.Verifying, speed.BytesPerSecond));
            if (!Valid(part, expected))
            {
                File.Delete(part);
                throw new InvalidDataException($"{label} arrived different from the manifest (size or SHA-256), so it was deleted and nothing was loaded.");
            }

            File.Move(part, path, overwrite: true);
            fetched++;
            done = before + expected.Bytes;
        }

        return new DownloadResult(fetched, present);
    }

    // One attempt at one file: resumes from the .part when the server honours a range, and
    // starts over when it does not or when the .part is already as long as the file should be.
    private static async Task FetchAsync(HttpClient http, ManifestFile expected, string part, string label, long before, long total, Speed speed, IProgress<DownloadProgress>? progress, CancellationToken cancellation)
    {
        long have = File.Exists(part) ? new FileInfo(part).Length : 0;
        if (have >= expected.Bytes)
        {
            if (have == expected.Bytes)
            {
                return; // Fetched last time and never verified; the caller verifies it now.
            }

            File.Delete(part);
            have = 0;
        }

        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(expected.Url));
        if (have > 0)
        {
            request.Headers.Range = new RangeHeaderValue(have, null);
        }

        // The client has no timeout, since an 800 MB file may take many minutes; what fails is a
        // connection that goes silent, so each wait gets its own limit and a stall is retried.
        using CancellationTokenSource stall = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        stall.CancelAfter(StallTimeout);
        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stall.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            bool resumed = have > 0 && response.StatusCode == HttpStatusCode.PartialContent;
            if (!resumed)
            {
                have = 0;
            }

            await using Stream source = await response.Content.ReadAsStreamAsync(stall.Token).ConfigureAwait(false);
            await using FileStream target = new(part, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
            byte[] buffer = new byte[1 << 20];
            int read;
            while ((read = await source.ReadAsync(buffer, stall.Token).ConfigureAwait(false)) > 0)
            {
                stall.CancelAfter(StallTimeout);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);
                have += read;
                speed.Add(read);
                progress?.Report(new DownloadProgress(label, before + have, total, DownloadStage.Downloading, speed.BytesPerSecond));
            }
        }
        catch (OperationCanceledException silent) when (!cancellation.IsCancellationRequested)
        {
            throw new IOException($"The connection went silent for {StallTimeout.TotalSeconds:0} seconds.", silent);
        }
    }

    // A dropped or silent connection and a server error can pass; a 4xx is the file not being
    // there, which no retry changes, so it is reported at once.
    private static bool Transient(Exception failure) => failure switch
    {
        HttpRequestException { StatusCode: { } status } => (int)status >= 500,
        HttpRequestException or IOException => true,
        _ => false,
    };

    private static IEnumerable<(string Path, string Label, ManifestFile Expected)> Files(ModelManifest manifest, string root, IReadOnlyCollection<ModelRequest> requests)
    {
        HashSet<string> listed = new(StringComparer.Ordinal);
        foreach (ModelRequest request in requests)
        {
            ManifestModel model = manifest.Model(request.Model) ?? throw new InvalidDataException($"The models manifest {manifest.Version} does not list {request.Model}.");
            string variant = VariantOf(manifest, request)!;
            if (model.Variants.TryGetValue(variant, out ManifestFile? onnx) && listed.Add($"{model.Name}/{variant}"))
            {
                yield return (Path.Combine(root, model.Name, variant + ".onnx"), $"{model.Name}/{variant}.onnx", onnx);
            }

            // After the ONNX file, so a model folder never holds a meta.json without a variant.
            if (listed.Add($"{model.Name}/meta"))
            {
                yield return (Path.Combine(root, model.Name, "meta.json"), $"{model.Name}/meta.json", model.Meta);
            }
        }
    }

    private static long PartialBytes(string path)
    {
        FileInfo part = new(path + ".part");
        return part.Exists ? part.Length : 0;
    }

    private static bool Sized(string path, ManifestFile expected)
    {
        FileInfo file = new(path);
        return file.Exists && file.Length == expected.Bytes;
    }

    private static bool Valid(string path, ManifestFile expected)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.Length != expected.Bytes)
        {
            return false;
        }

        using FileStream stream = file.OpenRead();
        return Convert.ToHexStringLower(SHA256.HashData(stream)) == expected.Sha256;
    }

    /// <summary>The recent speed: bytes over the last few seconds, so a stall shows and a burst does not mislead.</summary>
    private sealed class Speed
    {
        private static readonly TimeSpan Window = TimeSpan.FromSeconds(4);
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Queue<(TimeSpan At, long Bytes)> samples = new();
        private long inWindow;

        public double BytesPerSecond { get; private set; }

        public void Add(long bytes)
        {
            TimeSpan now = clock.Elapsed;
            samples.Enqueue((now, bytes));
            inWindow += bytes;
            while (samples.Count > 1 && now - samples.Peek().At > Window)
            {
                inWindow -= samples.Dequeue().Bytes;
            }

            double seconds = (now - samples.Peek().At).TotalSeconds;
            BytesPerSecond = seconds > 0.5 ? inWindow / seconds : 0;
        }
    }
}
