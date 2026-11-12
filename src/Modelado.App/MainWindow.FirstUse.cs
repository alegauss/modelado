using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Modelado.Inference;
using Modelado.Inference.Pipelines;

namespace Modelado.App;

/// <summary>
/// First-use cards (MOD22, after PhotoBoost's PB58). The 3D Object and Relief cards, and the AI
/// texture button, say before they are pressed that their models are a one-time download and how
/// large it is. Pressing offers it in the consent panel (MOD21); once agreed the download runs beside
/// the app, every other operation still usable, with its progress on the card and in the status bar.
/// It can be paused and resumed, a failure says what to do, and the operation that was pressed
/// starts by itself once its models are verified.
/// </summary>
public partial class MainWindow
{
    // One download at a time, About's Download everything included, so two never write one file.
    private static readonly SemaphoreSlim DownloadGate = new(1, 1);

    private FirstUseCard? objectFirstUse;
    private FirstUseCard? reliefFirstUse;
    private FirstUseCard? textureFirstUse;

    private enum FirstUseStage
    {
        Offered,
        Waiting,
        Downloading,
        Paused,
        Failed,
    }

    private IEnumerable<FirstUseCard> FirstUseCards => new[] { objectFirstUse, reliefFirstUse, textureFirstUse }.OfType<FirstUseCard>();

    private void StartFirstUse()
    {
        objectFirstUse = new(Strings.ObjectCardTitle, Strings.ObjectFirstUseAccept, () => ObjectModels(ObjectEnhancePhoto.IsChecked == true),
            ObjectFirstUse, ObjectFirstUseBadge, ObjectFirstUseDetail, ObjectFirstUseProgress, ObjectFirstUseAction, () => OnGenerateObject(this, new RoutedEventArgs()));
        reliefFirstUse = new(Strings.ReliefCardTitle, Strings.ReliefFirstUseAccept, () => ReliefReconstruction.Models(ReliefRemoveBackground.IsChecked == true, DepthModel),
            ReliefFirstUse, ReliefFirstUseBadge, ReliefFirstUseDetail, ReliefFirstUseProgress, ReliefFirstUseAction, () => OnGenerateRelief(this, new RoutedEventArgs()));
        textureFirstUse = new(Strings.TextureFirstUseTitle, Strings.TextureFirstUseAccept, () => [Upscaling.Model],
            TextureFirstUse, TextureFirstUseBadge, TextureFirstUseDetail, TextureFirstUseProgress, TextureFirstUseAction, () => OnUpscaleTexture(this, new RoutedEventArgs()));
        Closed += (_, _) =>
        {
            // The .part files stay, so the next start resumes them.
            foreach (FirstUseCard card in FirstUseCards)
            {
                card.Downloading?.Cancel();
            }
        };
    }

    private string Variant => ExecutionProviders.VariantFor(provider, runner.HighQuality);

    // Each card says what its first use downloads, or how its download is going.
    private async Task RefreshFirstUseAsync()
    {
        // An added source is read too, so the Relief card can offer its depth model (MOD27).
        ModelManifest? listed = Sources.Count > 0 || FirstUseCards.Any(card => runner.Missing(card.Models()).Count > 0) ? await ManifestAsync().ConfigureAwait(true) : manifest;
        FillDepthChoices(listed);
        foreach (FirstUseCard card in FirstUseCards)
        {
            ShowFirstUse(card, listed);
        }

        // The texture card is the texture button's, and only means something with a texture to sharpen.
        if (textureFirstUse is not null && mesh?.Texture is null)
        {
            textureFirstUse.Box.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowFirstUse(FirstUseCard card, ModelManifest? listed)
    {
        IReadOnlyList<ModelRequest> missing = runner.Missing(card.Models());

        // MOD98: the bar and the action are set once, to where this reading leaves them, never
        // collapsed and shown again on each progress report: a control flickered four times a
        // second drops out of the accessibility tree once its card has been hidden and shown.
        card.Action.Tag = null;
        Visibility bar = Visibility.Collapsed;
        try
        {
            ShowFirstUseStage(card, listed, missing, ref bar);
        }
        finally
        {
            card.Bar.Visibility = bar;
            card.Action.Visibility = card.Action.Tag is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void ShowFirstUseStage(FirstUseCard card, ModelManifest? listed, IReadOnlyList<ModelRequest> missing, ref Visibility bar)
    {
        if (missing.Count == 0 && card.Downloading is null)
        {
            card.Stage = FirstUseStage.Offered;
            card.Badge.Visibility = Visibility.Collapsed;
            card.Detail.Text = card.Ready ?? "";
            card.Box.Visibility = card.Ready is null ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        card.Ready = null;
        card.Box.Visibility = Visibility.Visible;
        card.Badge.Visibility = Visibility.Visible;
        if (listed is null || missing.Any(m => listed.Model(m.Model) is null))
        {
            card.Badge.Text = Strings.ModelsNotInstalled;
            card.Detail.Text = listed is null
                ? manifestError is null ? Strings.ModelListUnreadableOffline : Ui.Format(Strings.ModelListUnreadableOfflineBecause, manifestError)
                : Ui.Format(Strings.ModelListLacks, listed.Version, string.Join(Strings.ListComma, missing.Where(m => listed.Model(m.Model) is null).Select(m => m.Model)));
            return;
        }

        long pending = ModelDownloads.PendingBytes(listed, library.DownloadRoot, missing, library.Roots);
        long total = ModelDownloads.TotalBytes(listed, missing);
        switch (card.Stage)
        {
            case FirstUseStage.Waiting:
                card.Badge.Text = Strings.DownloadQueued;
                card.Detail.Text = Strings.DownloadQueuedDetail;
                ShowAction(card, Strings.Cancel);
                break;
            case FirstUseStage.Downloading:
                DownloadProgress progress = card.Last ?? new DownloadProgress("", total - pending, total);
                card.Badge.Text = Ui.Format(Strings.DownloadingPercent, DownloadText.Percent(progress));
                card.Detail.Text = DownloadText.Line(progress);
                card.Bar.Value = DownloadText.Percent(progress);
                bar = Visibility.Visible;
                ShowAction(card, Strings.PauseButton);
                break;
            case FirstUseStage.Paused:
                card.Badge.Text = Ui.Format(Strings.PausedPercent, DownloadText.Percent(new DownloadProgress("", total - pending, total)));
                card.Detail.Text = Strings.PausedDetail;
                card.Bar.Value = total == 0 ? 0 : 100 * (total - pending) / total;
                bar = Visibility.Visible;
                ShowAction(card, Strings.ResumeDownload);
                break;
            case FirstUseStage.Failed:
                card.Badge.Text = Strings.DownloadStopped;
                card.Detail.Text = card.Failure ?? "";
                ShowAction(card, Strings.TryAgain);
                break;
            default:
                card.Badge.Text = pending < total
                    ? Ui.Format(Strings.OneTimeDownloadLeft, DownloadText.Size(pending))
                    : Ui.Format(Strings.OneTimeDownload, DownloadText.Size(total));
                card.Detail.Text = Ui.Format(
                    Strings.FirstUseDetail,
                    string.Join(Strings.ListComma, missing.Select(m => m.Model)),
                    string.Join(Strings.ListAnd, missing.Select(m => SourceOf(listed.Model(m.Model)!.Variants[ModelDownloads.VariantOf(listed, m)!].Url)).Distinct(StringComparer.Ordinal)));
                break;
        }
    }

    // Its caption and the card it acts on; ShowFirstUse shows it once the reading is done.
    private static void ShowAction(FirstUseCard card, string label)
    {
        if (!Equals(card.Action.Content, label))
        {
            card.Action.Content = label;
        }

        card.Action.Tag = card;
    }

    /// <summary>
    /// What an operation's button does when its models are missing: the offer, then the download,
    /// and the operation once they are in. Answers whether the models were missing.
    /// </summary>
    private async Task<bool> OfferFirstUseAsync(FirstUseCard card)
    {
        IReadOnlyList<ModelRequest> missing = runner.Missing(card.Models());
        if (missing.Count == 0)
        {
            return false;
        }

        if (card.Downloading is not null)
        {
            return true; // Already on its way; the card shows how far.
        }

        ModelManifest? listed = await ManifestAsync().ConfigureAwait(true);
        if (listed is null || missing.Any(m => listed.Model(m.Model) is null))
        {
            ShowFirstUse(card, listed);
            return true;
        }

        long pending = ModelDownloads.PendingBytes(listed, library.DownloadRoot, missing, library.Roots);
        if (pending > 0 && !await AskConsentAsync(card.Title, card.Accept, listed, missing, pending).ConfigureAwait(true))
        {
            return true;
        }

        card.RunWhenReady = true;
        _ = DownloadFirstUseAsync(card);
        return true;
    }

    private void OnFirstUseAction(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not FirstUseCard card)
        {
            return;
        }

        if (card.Downloading is { } inFlight)
        {
            inFlight.Cancel();
        }
        else
        {
            _ = DownloadFirstUseAsync(card);
        }
    }

    /// <summary>Fetches the card's models, then runs its operation when it was asked for.</summary>
    private async Task DownloadFirstUseAsync(FirstUseCard card)
    {
        if (await ManifestAsync().ConfigureAwait(true) is not { } listed)
        {
            return;
        }

        ModelRequest[] requests = [.. card.Models().Select(m => new ModelRequest(m, Variant))];
        card.Downloading = new CancellationTokenSource();
        card.Failure = null;
        card.Last = null;
        CancellationToken token = card.Downloading.Token;
        bool entered = false;
        try
        {
            card.Stage = FirstUseStage.Waiting;
            ShowFirstUse(card, listed);
            await DownloadGate.WaitAsync(token).ConfigureAwait(true);
            entered = true;
            card.Stage = FirstUseStage.Downloading;
            ShowFirstUse(card, listed);
            Progress<DownloadProgress> progress = new(p =>
            {
                if (card.Stage != FirstUseStage.Downloading)
                {
                    return; // A report that arrived after the download ended.
                }

                // A report per chunk is more than a person reads; four a second is enough.
                bool turned = card.Last?.Stage != p.Stage;
                card.Last = p;
                if (!turned && card.Shown.IsRunning && card.Shown.ElapsedMilliseconds < 250)
                {
                    return;
                }

                card.Shown.Restart();
                ShowFirstUse(card, listed);
                DownloadChipText.Text = Ui.Format(Strings.CardDownloadStatus, card.Title, DownloadText.Line(p));
                ProgressBar.Value = (double)p.Done / Math.Max(1, p.Total);
                DownloadChip.Visibility = Visibility.Visible;
            });
            await ModelDownloads.DownloadAsync(http, listed, library.DownloadRoot, requests, progress, token, library.Roots).ConfigureAwait(true);
            library.Refresh();
            card.Stage = FirstUseStage.Offered;
            card.Ready = Ui.Format(Strings.ReadyOffline, card.Title);
            if (card.RunWhenReady)
            {
                card.RunWhenReady = false;
                card.Ready = Ui.Format(Strings.ReadyStartsNow, card.Title);
                pendingRun = card.Run;
            }
        }
        catch (OperationCanceledException)
        {
            card.Stage = entered ? FirstUseStage.Paused : FirstUseStage.Offered;
        }
        catch (Exception failure) when (failure is HttpRequestException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            card.Failure = failure is HttpRequestException { StatusCode: HttpStatusCode.NotFound }
                ? Strings.DownloadGone
                : Ui.Format(Strings.DownloadFailedKept, failure.Message);
            card.Stage = FirstUseStage.Failed;
        }
        finally
        {
            if (entered)
            {
                DownloadGate.Release();
            }

            card.Downloading.Dispose();
            card.Downloading = null;
            if (!FirstUseCards.Any(c => c.Downloading is not null) && !busy)
            {
                DownloadChip.Visibility = Visibility.Collapsed;
            }

            if (AboutOpen)
            {
                FillAbout();
            }

            await RefreshModelStatusAsync().ConfigureAwait(true);
            RunPending();
        }
    }

    /// <summary>
    /// Runs the operation whose download just finished, once nothing else is running: the person
    /// asked for it when they agreed to the download, so they are not asked again.
    /// </summary>
    private void RunPending()
    {
        if (pendingRun is not { } run || busy)
        {
            return;
        }

        pendingRun = null;
        Dispatcher.BeginInvoke(run);
    }

    private Action? pendingRun;

    /// <summary>One card's first-use controls and where its download stands.</summary>
    private sealed class FirstUseCard(string title, string accept, Func<IReadOnlyList<string>> models, FrameworkElement box, TextBlock badge, TextBlock detail, ProgressBar bar, Button action, Action run)
    {
        public string Title { get; } = title;

        public string Accept { get; } = accept;

        public Func<IReadOnlyList<string>> Models { get; } = models;

        public FrameworkElement Box { get; } = box;

        public TextBlock Badge { get; } = badge;

        public TextBlock Detail { get; } = detail;

        public ProgressBar Bar { get; } = bar;

        public Button Action { get; } = action;

        public Action Run { get; } = run;

        public FirstUseStage Stage { get; set; }

        public CancellationTokenSource? Downloading { get; set; }

        public DownloadProgress? Last { get; set; }

        public string? Failure { get; set; }

        public string? Ready { get; set; }

        public bool RunWhenReady { get; set; }

        public System.Diagnostics.Stopwatch Shown { get; } = new();
    }
}
