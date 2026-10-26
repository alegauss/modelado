using System.IO;
using System.Net.Http;
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
    private string? manifestError;
    private bool pausing;

    private static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Modelado/0.1");
        return client;
    }

    private static string Size(long bytes) => DownloadText.Size(bytes);

    private async Task<ModelManifest?> ManifestAsync()
    {
        if (manifest is not null)
        {
            return manifest;
        }

        try
        {
            manifest = await WithSourcesAsync(await ModelDownloads.LoadManifestAsync(http, options.Manifest, CancellationToken.None).ConfigureAwait(true)).ConfigureAwait(true);
        }
        catch (Exception error) when (error is HttpRequestException or InvalidDataException or TaskCanceledException)
        {
            // Said on the cards whose models it would have listed (MOD9), not in a status line.
            manifestError = error.Message;
        }

        return manifest;
    }

    // Each card says whether its models are here, or what its first use downloads (MOD22).
    private Task RefreshModelStatusAsync() => RefreshFirstUseAsync();

    /// <summary>Downloads what the models need after asking; true when they are all on disk.</summary>
    /// <param name="purpose">What needs them, as the consent panel's title says it: "Relief".</param>
    /// <param name="accept">What agreeing does, as its button says it.</param>
    /// <param name="models">The models.</param>
    private async Task<bool> DownloadAsync(string purpose, string accept, IReadOnlyList<string> models)
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
        if (bytes > 0 && !await AskConsentAsync(purpose, accept, listed, requests, bytes).ConfigureAwait(true))
        {
            return false;
        }

        // MOD22: through the gate a card's first-use download takes too, so two never write one file.
        await DownloadGate.WaitAsync().ConfigureAwait(true);
        try
        {
            return await DownloadHeldAsync(listed, requests).ConfigureAwait(true);
        }
        finally
        {
            DownloadGate.Release();
        }
    }

    private async Task<bool> DownloadHeldAsync(ModelManifest listed, ModelRequest[] requests)
    {
        while (true)
        {
            using CancellationTokenSource cancel = new();
            work = cancel;
            pausing = false;
            SetBusy(true, "Downloading models", download: true);
            Progress<DownloadProgress> progress = new(p =>
            {
                ProgressBar.Value = (double)p.Done / Math.Max(1, p.Total);
                ProgressText.Text = DownloadText.Line(p);
                DownloadChipText.Text = $"Downloading models: {ProgressText.Text}";
            });

            try
            {
                DownloadResult result = await ModelDownloads.DownloadAsync(http, listed, library.DownloadRoot, requests, progress, cancel.Token, library.Roots).ConfigureAwait(true);
                library.Refresh();
                LastRunSaid($"Models ready · {result.Fetched} file(s) downloaded", "Each file was checked by SHA-256.");
                return true;
            }
            catch (OperationCanceledException) when (pausing)
            {
                // Paused: the .part files stay; Continue picks up where it stopped.
                SetBusy(false, "", download: false);
                if (MessageBox.Show(this, "Download paused. Continue now?", "Download models", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    LastRunSaid("Download paused", "It resumes where it stopped next time.");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                LastRunSaid("Download cancelled", "It resumes where it stopped next time.");
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
