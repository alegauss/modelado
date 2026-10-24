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
            Status($"A lista de modelos não pôde ser lida ({error.Message}). Sem internet, só os modelos já baixados funcionam.");
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
            return "Modelos instalados: funciona sem internet.";
        }

        ModelManifest? listed = await ManifestAsync().ConfigureAwait(true);
        if (listed is null || missing.Any(m => listed.Model(m.Model) is null))
        {
            return "Os modelos ainda não estão instalados.";
        }

        long bytes = ModelDownloads.PendingBytes(listed, library.DownloadRoot, missing, library.Roots);
        return $"Na primeira vez baixa {Size(bytes)} de modelos ({string.Join(", ", missing.Select(m => m.Model))}).";
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
            MessageBox.Show(this, "A lista de modelos não pôde ser lida do Hugging Face. Verifique a conexão e tente de novo.", "Modelado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        string variant = ExecutionProviders.VariantFor(provider, runner.HighQuality);
        ModelRequest[] requests = [.. models.Select(m => new ModelRequest(m, variant))];
        if (requests.FirstOrDefault(r => listed.Model(r.Model) is null) is { Model: not null } absent)
        {
            MessageBox.Show(this, $"A lista de modelos ({listed.Version}) não traz {absent.Model}.", "Modelado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        long bytes = ModelDownloads.PendingBytes(listed, library.DownloadRoot, requests, library.Roots);
        if (bytes > 0)
        {
            StringBuilder text = new($"Vai baixar {Size(bytes)} do Hugging Face para\n{library.DownloadRoot}\n\n");
            foreach (ModelRequest request in requests)
            {
                ManifestModel model = listed.Model(request.Model)!;
                string v = ModelDownloads.VariantOf(listed, request)!;
                text.AppendLine(CultureInfo.CurrentCulture, $"• {model.Name} ({v}, {Size(model.Variants[v].Bytes)}): {model.License}");
            }

            text.Append("\nSó os modelos são baixados; as fotos nunca saem deste computador. Continuar?");
            if (MessageBox.Show(this, text.ToString(), "Baixar modelos", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
            {
                return false;
            }
        }

        while (true)
        {
            using CancellationTokenSource cancel = new();
            work = cancel;
            pausing = false;
            SetBusy(true, "Baixando modelos", download: true);
            Progress<DownloadProgress> progress = new(p =>
            {
                ProgressBar.Value = (double)p.Done / Math.Max(1, p.Total);
                string speed = p.BytesPerSecond > 0 ? $" · {p.BytesPerSecond / 1e6:0.0} MB/s" : "";
                string left = p.Remaining is { } r ? $" · faltam {(r.TotalMinutes >= 1 ? $"{r.TotalMinutes:0} min" : $"{r.TotalSeconds:0} s")}" : "";
                ProgressText.Text = p.Stage switch
                {
                    DownloadStage.Verifying => $"Conferindo {p.File}",
                    DownloadStage.Retrying => $"Conexão caiu; tentando de novo {p.File}",
                    _ => $"{p.File}: {Size(p.Done)} de {Size(p.Total)}{speed}{left}",
                };
            });

            try
            {
                DownloadResult result = await ModelDownloads.DownloadAsync(http, listed, library.DownloadRoot, requests, progress, cancel.Token, library.Roots).ConfigureAwait(true);
                library.Refresh();
                Status($"Modelos prontos: {result.Fetched} arquivo(s) baixado(s) e conferido(s) por SHA-256.");
                return true;
            }
            catch (OperationCanceledException) when (pausing)
            {
                // Paused: the .part files stay; Continuar picks up where it stopped.
                SetBusy(false, "", download: false);
                if (MessageBox.Show(this, "Download pausado. Continuar agora?", "Baixar modelos", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    Status("Download pausado: continua de onde parou na próxima vez.");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                Status("Download cancelado: continua de onde parou na próxima vez.");
                return false;
            }
            catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException)
            {
                MessageBox.Show(this, $"O download falhou.\n\n{error.Message}", "Baixar modelos", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        ProgressText.Text = "Pausando…";
        work?.Cancel();
    }
}
