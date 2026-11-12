using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// MOD21, ported from PhotoBoost's PB58: a download's numbers in the words the cards, the consent
/// panel and the status bar use, so all three say the same thing: megabytes done of the total, the
/// speed, and the time left.
/// </summary>
public static class DownloadText
{
    /// <summary>A size, in MB below a gigabyte and in GB from there.</summary>
    /// <param name="bytes">The size.</param>
    /// <returns>e.g. 325 MB or 1.2 GB.</returns>
    public static string Size(long bytes) => bytes >= 1L << 30
        ? Ui.Format(Strings.SizeGigabytes, bytes / (double)(1L << 30))
        : Ui.Format(Strings.SizeMegabytes, Math.Max(1, (bytes + (1 << 19)) >> 20));

    /// <summary>The time left, rounded the way a person reads it.</summary>
    /// <param name="left">The estimate.</param>
    /// <returns>e.g. about 40 s left, about 3 min left.</returns>
    public static string Left(TimeSpan left) => left.TotalSeconds < 60
        ? Ui.Format(Strings.SecondsLeft, Math.Max(5, Math.Ceiling(left.TotalSeconds / 5) * 5))
        : Ui.Format(Strings.MinutesLeft, Math.Ceiling(left.TotalMinutes));

    /// <summary>One line for a download in progress.</summary>
    /// <param name="progress">Where it is.</param>
    /// <returns>e.g. 120 of 325 MB · 8.4 MB/s · about 25 s left, or Verifying… once the bytes are in.</returns>
    public static string Line(DownloadProgress progress) => progress.Stage switch
    {
        DownloadStage.Verifying => Strings.DownloadVerifying,
        DownloadStage.Retrying => Ui.Format(Strings.DownloadRetrying, Done(progress)),
        _ when progress.BytesPerSecond <= 0 => Ui.Format(Strings.DownloadStarting, Done(progress)),
        _ => Ui.Format(Strings.DownloadRunning, Done(progress), progress.BytesPerSecond / (1 << 20), Left(progress.Remaining!.Value)),
    };

    /// <summary>How much of the download is done, as a percentage.</summary>
    /// <param name="progress">Where it is.</param>
    /// <returns>0 to 100.</returns>
    public static int Percent(DownloadProgress progress) =>
        progress.Total <= 0 ? 0 : (int)Math.Clamp(100 * progress.Done / progress.Total, 0, 100);

    private static string Done(DownloadProgress progress) =>
        Ui.Format(Strings.DownloadDone, progress.Done >> 20, Size(progress.Total));
}
