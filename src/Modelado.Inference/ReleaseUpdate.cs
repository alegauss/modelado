using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Modelado.Inference;

/// <summary>
/// Installs a newer release (MOD31), after freewilly's ReleaseUpdate and ReleaseSums and
/// claude-tray's Updater. The installer is fetched the way a model is (resumed, retried, held to
/// its size) and must match its line in the release's SHA256SUMS: a missing line or another digest
/// is refused and the file deleted, so nothing unverified ever runs. It is then started silently,
/// upgrading in place through the AppId every installer keeps (MOD28), and relaunches Modelado.
/// Nothing here runs by itself: only a click in About starts it.
/// </summary>
public static partial class ReleaseUpdate
{
    /// <summary>The installer's switches: silent, no message boxes, and Modelado started again after.</summary>
    public const string InstallerArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /relaunch=1";

    /// <summary>Where installers are downloaded: %TEMP%\Modelado-update.</summary>
    public static string DefaultFolder => Path.Combine(Path.GetTempPath(), "Modelado-update");

    /// <summary>A file's digest in a SHA256SUMS text, as sha256sum writes it.</summary>
    /// <param name="sums">The file's text.</param>
    /// <param name="fileName">The file whose line is wanted.</param>
    /// <returns>Its SHA-256 in lowercase hex, or null where no well-formed line names it.</returns>
    public static string? SumFor(string sums, string fileName)
    {
        ArgumentNullException.ThrowIfNull(sums);
        foreach (string line in sums.Split('\n'))
        {
            // "<64 hex>  name", or "<64 hex> *name" for sha256sum's binary mode.
            Match match = SumLine().Match(line.TrimEnd('\r'));
            if (match.Success && string.Equals(match.Groups[2].Value, fileName, StringComparison.Ordinal))
            {
                return match.Groups[1].Value.ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>Downloads a release's installer and checks it against SHA256SUMS.</summary>
    /// <param name="http">The client.</param>
    /// <param name="release">The release.</param>
    /// <param name="folder">Where it goes.</param>
    /// <param name="progress">Told as it downloads.</param>
    /// <param name="cancellation">Cancels it; what arrived stays, so a later download resumes.</param>
    /// <returns>The verified installer's path.</returns>
    /// <exception cref="InvalidDataException">SHA256SUMS has no line for it, or it does not match; nothing was kept.</exception>
    public static async Task<string> DownloadAsync(HttpClient http, AvailableRelease release, string folder, IProgress<DownloadProgress>? progress, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(release);
        string sums = await http.GetStringAsync(new Uri(release.SumsUrl), cancellation).ConfigureAwait(false);
        string digest = SumFor(sums, release.InstallerName)
            ?? throw new InvalidDataException($"{ReleaseCheck.SumsAssetName} has no line for {release.InstallerName}, so it was not downloaded.");
        string path = Path.Combine(folder, release.InstallerName);
        await ModelDownloads.DownloadFileAsync(http, new ManifestFile(release.InstallerUrl, release.InstallerBytes, digest), path, release.InstallerName, progress, cancellation).ConfigureAwait(false);
        return path;
    }

    /// <summary>How the verified installer is started.</summary>
    /// <param name="installer">Its path.</param>
    /// <returns>The start info; the caller exits once it has started.</returns>
    public static ProcessStartInfo InstallerStart(string installer) => new(installer, InstallerArguments) { UseShellExecute = false };

    [GeneratedRegex(@"^([0-9a-fA-F]{64}) [ *](.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SumLine();
}
