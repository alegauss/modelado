using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using Modelado.Inference;
using Modelado.Inference.Pipelines;

namespace Modelado.App;

/// <summary>
/// The models, fetched the first time an operation needs them: the manifest says what each file
/// weighs and its SHA-256, the user agrees to the size, and the download can be paused (its .part
/// files stay, so it resumes where it stopped) or cancelled.
/// </summary>
public partial class MainWindow
{
    private readonly HttpClient http = CreateClient();
    private ModelManifest? manifest;
    private bool pausing;

    private static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Modelado/0.1");
        return client;
    }

    private static string Size(long bytes) => bytes >= 1e9
        ? string.Format(CultureInfo.CurrentCulture, "{0:0.0} GB", bytes / 1e9)
        : string.Format(CultureInfo.CurrentCulture, "{0:0} MB", Math.Max(1, bytes / 1e6));

    private async Task<ModelManifest?> ManifestAsync()
    {
        if (manifest is not null)
        {
            return manifest;
        }

        try
        {
            manifest = await ModelDownloads.LoadManifestAsync(http, options.Manifest, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception error) when (error is HttpRequestException or InvalidDataException or TaskCanceledException)
        {
            Status($"The model list could not be read ({error.Message}). Without internet, only models already downloaded work.");
        }

        return manifest;
    }

    // Each card says whether its models are here, or how much the first use downloads.
    private async Task RefreshModelStatusAsync()
    {
        ObjectModelsText.Text = await DescribeAsync(ObjectModels(ObjectEnhancePhoto.IsChecked == true)).ConfigureAwait(true);
        ReliefModelsText.Text = await DescribeAsync(ReliefReconstruction.Models(ReliefRemoveBackground.IsChecked == true)).ConfigureAwait(true);
    }

    private async Task<string> DescribeAsync(IReadOnlyList<string> models)
    {
        IReadOnlyList<ModelRequest> missing = runner.Missing(models);
        if (missing.Count == 0)
        {
            return "Models installed: works offline.";
        }

        ModelManifest? listed = await ManifestAsync().ConfigureAwait(true);
        if (listed is null || missing.Any(m => listed.Model(m.Model) is null))
        {
            return "The models are not installed yet.";
        }

        long bytes = ModelDownloads.PendingBytes(listed, library.DownloadRoot, missing, library.Roots);
        return $"First use downloads {Size(bytes)} of models ({string.Join(", ", missing.Select(m => m.Model))}).";
    }

    /// <summary>Downloads what the models need after asking; true when they are all on disk.</summary>
    private async Task<bool> DownloadAsync(IReadOnlyList<string> models)
    {
        if (busy)
        {
            return false;
        }

        ModelManifest? listed = await ManifestAsync().ConfigureAwait(true);
        if (listed is null)
        {
            MessageBox.Show(this, "The model list could not be read from Hugging Face. Check the connection and try again.", "Modelado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        string variant = ExecutionProviders.VariantFor(provider, runner.HighQuality);
        ModelRequest[] requests = [.. models.Select(m => new ModelRequest(m, variant))];
        if (requests.FirstOrDefault(r => listed.Model(r.Model) is null) is { Model: not null } absent)
        {
            MessageBox.Show(this, $"The model list ({listed.Version}) does not include {absent.Model}.", "Modelado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        long bytes = ModelDownloads.PendingBytes(listed, library.DownloadRoot, requests, library.Roots);
        if (bytes > 0)
        {
            StringBuilder text = new($"This downloads {Size(bytes)} from Hugging Face to\n{library.DownloadRoot}\n\n");
            foreach (ModelRequest request in requests)
            {
                ManifestModel model = listed.Model(request.Model)!;
                string v = ModelDownloads.VariantOf(listed, request)!;
                text.AppendLine(CultureInfo.CurrentCulture, $"• {model.Name} ({v}, {Size(model.Variants[v].Bytes)}): {model.License}");
            }

            text.Append("\nOnly the models are downloaded; photos never leave this computer. Continue?");
            if (MessageBox.Show(this, text.ToString(), "Download models", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
            {
                return false;
            }
        }

        while (true)
        {
            using CancellationTokenSource cancel = new();
            work = cancel;
            pausing = false;
            SetBusy(true, "Downloading models", download: true);
            Progress<DownloadProgress> progress = new(p =>
            {
                ProgressBar.Value = (double)p.Done / Math.Max(1, p.Total);
                string speed = p.BytesPerSecond > 0 ? $" · {p.BytesPerSecond / 1e6:0.0} MB/s" : "";
                string left = p.Remaining is { } r ? $" · {(r.TotalMinutes >= 1 ? $"{r.TotalMinutes:0} min" : $"{r.TotalSeconds:0} s")} left" : "";
                ProgressText.Text = p.Stage switch
                {
                    DownloadStage.Verifying => $"Checking {p.File}",
                    DownloadStage.Retrying => $"Connection dropped; retrying {p.File}",
                    _ => $"{p.File}: {Size(p.Done)} of {Size(p.Total)}{speed}{left}",
                };
            });

            try
            {
                DownloadResult result = await ModelDownloads.DownloadAsync(http, listed, library.DownloadRoot, requests, progress, cancel.Token, library.Roots).ConfigureAwait(true);
                library.Refresh();
                Status($"Models ready: {result.Fetched} file(s) downloaded and checked by SHA-256.");
                return true;
            }
            catch (OperationCanceledException) when (pausing)
            {
                // Paused: the .part files stay; Continue picks up where it stopped.
                SetBusy(false, "", download: false);
                if (MessageBox.Show(this, "Download paused. Continue now?", "Download models", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    Status("Download paused: it resumes where it stopped next time.");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                Status("Download cancelled: it resumes where it stopped next time.");
                return false;
            }
            catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException)
            {
                MessageBox.Show(this, $"The download failed.\n\n{error.Message}", "Download models", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            finally
            {
                work = null;
                SetBusy(false, "", download: false);
                await RefreshModelStatusAsync().ConfigureAwait(true);
            }
        }
    }

    private void OnPauseDownload(object sender, RoutedEventArgs e)
    {
        pausing = true;
        ProgressText.Text = "Pausing…";
        work?.Cancel();
    }
}
