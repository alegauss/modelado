using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Modelado.Inference;

/// <summary>Where the latest release's description comes from: the network, behind a seam.</summary>
public interface IReleaseFeed
{
    /// <summary>Reads the latest release, as the JSON GitHub's API answers with.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The body, or throws.</returns>
    Task<string> LatestAsync(CancellationToken cancellation);
}

/// <summary>A published release, with the two files an install needs (MOD30).</summary>
/// <param name="Version">Its version, from the tag.</param>
/// <param name="Tag">The tag as published.</param>
/// <param name="InstallerName">The installer's file name, which is also its line in the sums file.</param>
/// <param name="InstallerUrl">Where the installer is.</param>
/// <param name="SumsUrl">Where SHA256SUMS is.</param>
/// <param name="PageUrl">The release's page, for a person to read.</param>
/// <param name="InstallerBytes">The installer's size, which its download is held to (MOD31).</param>
public sealed record AvailableRelease(Version Version, string Tag, string InstallerName, string InstallerUrl, string SumsUrl, string? PageUrl, long InstallerBytes);

/// <summary>What a check came to.</summary>
/// <param name="Newer">The newer release, or null.</param>
/// <param name="Reached">False when the feed could not be read or did not describe a release.</param>
public sealed record ReleaseAnswer(AvailableRelease? Newer, bool Reached);

/// <summary>
/// Whether a newer Modelado has been released (MOD30), after freewilly's ReleaseCheck (DD154). It
/// reads the latest release's description and compares its tag to the running build; downloading
/// and installing it is MOD31's.
/// <para>
/// Silent on every failure: offline, a proxy, a rate limit or no release yet each read as "nothing
/// newer", since a check nobody asked for must not complain. Only a check a person starts says it
/// could not reach GitHub, through <see cref="ReleaseAnswer.Reached"/>.
/// </para>
/// <para>
/// The installer's name carries the version, so it is found by a pattern; a release with two files
/// answering it, or with no SHA256SUMS beside it, offers nothing rather than a guess. The asset is
/// never derived from the tag: a tag and a file name that disagree is what release.yml refuses to
/// publish, and matching on the tag here would hide that.
/// </para>
/// </summary>
public sealed partial class ReleaseCheck
{
    /// <summary>The one release this asks about.</summary>
    public const string LatestReleaseApi = "https://api.github.com/repos/alegauss/modelado/releases/latest";

    /// <summary>The digest file release.yml publishes beside the installer (MOD29).</summary>
    public const string SumsAssetName = "SHA256SUMS";

    private readonly IReleaseFeed feed;
    private readonly Version running;

    /// <summary>A check against a feed.</summary>
    /// <param name="feed">Where the release description comes from.</param>
    /// <param name="running">The version to beat; this build's when null.</param>
    public ReleaseCheck(IReleaseFeed feed, Version? running = null)
    {
        ArgumentNullException.ThrowIfNull(feed);
        this.feed = feed;
        this.running = running ?? ThisBuild;
    }

    /// <summary>This build's version, in the three parts a tag has.</summary>
    public static Version ThisBuild =>
        typeof(ReleaseCheck).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, Math.Max(0, v.Build)) : new Version(0, 0, 0);

    /// <summary>Asks whether there is something newer.</summary>
    /// <param name="cancellation">Cancellation.</param>
    /// <returns>The answer; a failure is no newer release, not reached.</returns>
    public async Task<ReleaseAnswer> AskAsync(CancellationToken cancellation = default)
    {
        try
        {
            AvailableRelease? latest = In(await feed.LatestAsync(cancellation).ConfigureAwait(false));
            return new ReleaseAnswer(latest is not null && latest.Version > running ? latest : null, latest is not null);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return new ReleaseAnswer(null, false);
        }
    }

    /// <summary>Reads a release description and says whether it beats a version.</summary>
    /// <param name="json">What the API answered.</param>
    /// <param name="running">The version to beat.</param>
    /// <returns>The release, or null where there is nothing newer to offer.</returns>
    public static AvailableRelease? NewerThan(string json, Version running)
    {
        ArgumentNullException.ThrowIfNull(running);
        return In(json) is { } release && release.Version > running ? release : null;
    }

    /// <summary>Reads a release description, whatever version it names.</summary>
    /// <param name="json">What the API answered.</param>
    /// <returns>Null where it is not a release, names no version, or offers no single installer and its sums.</returns>
    public static AvailableRelease? In(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("tag_name", out JsonElement named) || named.ValueKind != JsonValueKind.String
                || named.GetString() is not { } tag || !TryParseVersion(tag, out Version? version)
                || !root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? installerName = null, installerUrl = null, sumsUrl = null;
            long installerBytes = 0;
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                if (asset.ValueKind != JsonValueKind.Object
                    || !asset.TryGetProperty("name", out JsonElement nameOf) || nameOf.ValueKind != JsonValueKind.String
                    || !asset.TryGetProperty("browser_download_url", out JsonElement urlOf) || urlOf.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string name = nameOf.GetString()!, url = urlOf.GetString()!;
                if (string.Equals(name, SumsAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    sumsUrl = url;
                }
                else if (Installer().IsMatch(name))
                {
                    // Two installers is a release nobody can resolve from here.
                    if (installerName is not null)
                    {
                        return null;
                    }

                    (installerName, installerUrl) = (name, url);
                    installerBytes = asset.TryGetProperty("size", out JsonElement size) && size.ValueKind == JsonValueKind.Number && size.TryGetInt64(out long bytes) ? bytes : 0;
                }
            }

            // An installer of no stated size cannot be held to one, so it is not offered.
            string? page = root.TryGetProperty("html_url", out JsonElement html) && html.ValueKind == JsonValueKind.String ? html.GetString() : null;
            return installerName is null || installerUrl is null || sumsUrl is null || installerBytes <= 0
                ? null
                : new AvailableRelease(version, tag, installerName, installerUrl, sumsUrl, page, installerBytes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads a tag as a three-part version, skipping a leading v.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="version">What it means.</param>
    /// <returns>True where it names a version.</returns>
    public static bool TryParseVersion(string tag, [NotNullWhen(true)] out Version? version)
    {
        version = null;
        Match match = TagVersion().Match(tag ?? "");
        if (!match.Success)
        {
            return false;
        }

        int Part(int group) => match.Groups[group].Success && int.TryParse(match.Groups[group].Value, System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : 0;
        version = new Version(Part(1), Part(2), Part(3));
        return true;
    }

    // Modelado-Setup-1.2.3.exe, as build-installer.cmd names it (MOD28); not a hand-attached
    // Modelado-Setup-notes.exe.
    [GeneratedRegex(@"^Modelado-Setup-\d+(\.\d+)*\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Installer();

    [GeneratedRegex(@"^\s*v?(\d{1,9})(?:\.(\d{1,9}))?(?:\.(\d{1,9}))?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TagVersion();
}

/// <summary>An <see cref="IReleaseFeed"/> over GitHub's release API, or a URL standing in for it.</summary>
public sealed class HttpReleaseFeed : IReleaseFeed
{
    private readonly HttpClient client;
    private readonly string url;

    /// <summary>A feed over a client the caller owns.</summary>
    /// <param name="client">The client.</param>
    /// <param name="url">The API URL; <see cref="ReleaseCheck.LatestReleaseApi"/> when null.</param>
    public HttpReleaseFeed(HttpClient client, string? url = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
        this.url = url ?? ReleaseCheck.LatestReleaseApi;
    }

    /// <inheritdoc/>
    public async Task<string> LatestAsync(CancellationToken cancellation)
    {
        // The API answers 403 to a request with no user agent. The product and its version, and
        // nothing about the user.
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Modelado", ReleaseCheck.ThisBuild.ToString()));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using HttpResponseMessage response = await client.SendAsync(request, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
    }
}
