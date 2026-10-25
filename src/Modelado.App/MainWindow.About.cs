using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using Modelado.Inference;
using Modelado.Inference.Pipelines;

namespace Modelado.App;

/// <summary>
/// The About panel (MOD11, after PhotoBoost's PB46): the version, where inference runs, every model
/// the models folders hold with the license its meta.json carries, the downloads and their buttons,
/// and the third-party software. It takes the stage's place while open, so the toolbar and the
/// status bar, a running download's chip included, stay in view.
/// </summary>
public partial class MainWindow
{
    private bool AboutOpen => AboutPanel.Visibility == Visibility.Visible;

    private void OnAbout(object sender, RoutedEventArgs e) => ShowAbout(!AboutOpen);

    private void ShowAbout(bool open)
    {
        if (open)
        {
            FillAbout();
        }
        else
        {
            library.Refresh();
            _ = RefreshModelStatusAsync();
        }

        AboutPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ViewerFrame.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        Operations.Visibility = !open && (photo is not null || mesh is not null) ? Visibility.Visible : Visibility.Collapsed;
        AboutButton.Content = open ? "Back" : "About and models";
    }

    private void FillAbout()
    {
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";
        AboutVersion.Text = $"Version {version}";
        AboutInference.Text = "Turns a photo into a 3D model on this computer: a whole object with TripoSR, or a relief with Depth Anything V2. " +
            $"Inference runs in ONNX Runtime, {(provider == ExecutionProvider.DirectML ? "on the GPU with DirectML" : "on the CPU")}. No photo is uploaded anywhere.";
        fillingProvider = true;
        (providerChoice switch
        {
            ProviderChoice.Cpu => ProviderCpu,
            ProviderChoice.DirectML => ProviderDirectML,
            _ => ProviderAuto,
        }).IsChecked = true;
        fillingProvider = false;
        string reason = providerReason ?? (options.Provider == providerChoice ? "Chosen by --provider for this run." : "");
        AboutProviderReason.Text = reason;
        AboutProviderReason.Visibility = reason.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        AboutModelsFolder.Text = $"From {string.Join(" and ", library.Roots)}";

        library.Refresh();
        IReadOnlyList<InstalledModel> installed = library.Installed();
        AboutModels.ItemsSource = installed.Count == 0
            ? new[] { "None yet: each operation downloads its own on first use." }
            : installed.Select(Describe).ToList();

        string source = ModelDownloads.Embedded is null ? options.Manifest ?? ModelDownloads.LatestManifestUrl : "built into this version";
        AboutManifest.Text = $"Models are downloaded from huggingface.co/alegauss/modelado-models; model list: {source}.";

        List<InstalledModel> downloads = [.. installed.Where(m => IsUnder(m.Root, library.DownloadRoot))];
        long downloaded = downloads.Sum(Bytes);
        AboutDownloadNote.Text = downloaded > 0
            ? $"Downloads go to {library.DownloadRoot}, where they take {Size(downloaded)}."
            : $"Downloads go to {library.DownloadRoot}. Nothing is downloaded yet.";
        AboutDownloadedModels.ItemsSource = downloads
            .Select(m => new DownloadedRow(m.Name, $"{m.Name} · {string.Join(", ", m.Variants)} · {Size(Bytes(m))}", $"Download-{m.Name}", $"Remove-{m.Name}", $"Remove {m.Name}"))
            .ToList();
        AboutRemoveAll.IsEnabled = downloads.Count > 0;
    }

    /// <summary>One downloaded model, as About's list shows it (MOD23).</summary>
    /// <param name="Name">The model's folder name.</param>
    /// <param name="Label">Its name, variants and size.</param>
    /// <param name="LabelId">The label's AutomationId.</param>
    /// <param name="RemoveId">Its Remove button's AutomationId.</param>
    /// <param name="RemoveName">Its Remove button's accessible name.</param>
    private sealed record DownloadedRow(string Name, string Label, string LabelId, string RemoveId, string RemoveName);

    private static long Bytes(InstalledModel model) => model.Variants.Sum(v => model.Meta.Variants[v].Bytes);

    private static string Describe(InstalledModel model) =>
        $"{model.Name} · {string.Join(", ", model.Variants)} · {Size(Bytes(model))} · {model.Meta.Task} · {model.Meta.Upstream.License} ({model.Meta.Upstream.LicenseUrl})";

    private static bool IsUnder(string path, string folder) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase);

    private async void OnAboutDownloadAll(object sender, RoutedEventArgs e)
    {
        await DownloadAsync("Working offline", "Download everything", [.. ObjectReconstruction.Models.Concat(ReliefReconstruction.Models(true)).Append(Upscaling.Model).Distinct()]).ConfigureAwait(true);
        if (AboutOpen)
        {
            FillAbout();
        }
    }

    // About's Cancel stops whichever download is running: its own, or a card's first use (MOD22).
    private void OnAboutCancelDownload(object sender, RoutedEventArgs e)
    {
        pausing = false;
        work?.Cancel();
        foreach (FirstUseCard card in FirstUseCards)
        {
            card.Downloading?.Cancel();
        }
    }

    private void OnAboutOpenFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(library.DownloadRoot);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{library.DownloadRoot}\"") { UseShellExecute = true });
    }

    // MOD23: a model a running operation or a download is using cannot be removed under it.
    private string? InUse(string model)
    {
        if (busy && runningModels.Contains(model, StringComparer.Ordinal))
        {
            return $"{model} is in use by {ProgressTitle.Text}; remove it once that has finished.";
        }

        if (busy && work is not null && ProgressTitle.Text == "Downloading models")
        {
            return "A download is running; remove models once it has finished or been cancelled.";
        }

        return FirstUseCards.FirstOrDefault(card => card.Downloading is not null && card.Models().Contains(model, StringComparer.Ordinal)) is { } downloading
            ? $"{model} is being downloaded for {downloading.Title}; cancel or finish that first."
            : null;
    }

    private bool fillingProvider;

    private void ResolveProvider(ProviderChoice choice)
    {
        providerChoice = choice;
        try
        {
            (provider, providerReason) = ExecutionProviders.Resolve(choice);
        }
        catch (InvalidOperationException error)
        {
            (provider, providerReason) = (ExecutionProvider.Cpu, error.Message);
        }
    }

    private void ShowProvider()
    {
        ProviderStatus.Text = ProviderLabel;

        // Why it is not the GPU, where a person looking at "CPU" will look.
        ProviderChip.ToolTip = providerReason;
        AutomationProperties.SetHelpText(ProviderStatus, providerReason ?? "");
    }

    // MOD18: the next operation builds its sessions on the new provider; nothing restarts.
    private async void OnProviderChanged(object sender, RoutedEventArgs e)
    {
        ProviderChoice choice = sender switch
        {
            _ when sender == ProviderCpu => ProviderChoice.Cpu,
            _ when sender == ProviderDirectML => ProviderChoice.DirectML,
            _ => ProviderChoice.Auto,
        };
        if (fillingProvider || busy || choice == providerChoice)
        {
            return;
        }

        ResolveProvider(choice);
        ReleaseSessions();
        ShowProvider();
        string? unsaved = null;
        try
        {
            ProviderSetting.Save(ProviderSetting.DefaultPath, choice);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            unsaved = $"The choice is used until Modelado closes but could not be saved: {error.Message}";
        }

        FillAbout();
        if (unsaved is not null)
        {
            AboutProviderReason.Text = unsaved;
            AboutProviderReason.Visibility = Visibility.Visible;
        }

        await RefreshModelStatusAsync().ConfigureAwait(true);
    }

    // The sessions keep the ONNX files open, so they are let go before a file is deleted.
    private void ReleaseSessions()
    {
        runner.Dispose();
        runner = new ModelRunner(library, provider, QualityHigh.IsChecked == true);
    }

    private async void OnAboutRemoveModel(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string model)
        {
            return;
        }

        if (InUse(model) is { } reason)
        {
            SayRemoval(reason);
            return;
        }

        try
        {
            ReleaseSessions();
            library.RemoveDownload(model);
            SayRemoval(null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SayRemoval($"{model} could not be removed: {error.Message}");
        }

        FillAbout();
        await RefreshModelStatusAsync().ConfigureAwait(true);
    }

    private void OnAboutRemoveAll(object sender, RoutedEventArgs e)
    {
        AboutRemoveAllQuestion.Text = $"Delete everything downloaded to {library.DownloadRoot}? Each operation offers its download again when next used.";
        AboutRemoveAllConfirm.Visibility = Visibility.Visible;
    }

    private void OnAboutRemoveAllKeep(object sender, RoutedEventArgs e) => AboutRemoveAllConfirm.Visibility = Visibility.Collapsed;

    private async void OnAboutRemoveAllDelete(object sender, RoutedEventArgs e)
    {
        AboutRemoveAllConfirm.Visibility = Visibility.Collapsed;
        if (library.Installed().Select(m => InUse(m.Name)).FirstOrDefault(reason => reason is not null) is { } reason)
        {
            SayRemoval(reason);
            return;
        }

        try
        {
            ReleaseSessions();
            library.RemoveDownloads();
            SayRemoval(null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SayRemoval($"The downloads could not all be removed: {error.Message}");
        }

        FillAbout();
        await RefreshModelStatusAsync().ConfigureAwait(true);
    }

    private void SayRemoval(string? text)
    {
        AboutRemoveNote.Text = text ?? "";
        AboutRemoveNote.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
