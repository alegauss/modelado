using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
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
        AboutModelsFolder.Text = $"From {string.Join(" and ", library.Roots)}";

        library.Refresh();
        IReadOnlyList<InstalledModel> installed = library.Installed();
        AboutModels.ItemsSource = installed.Count == 0
            ? new[] { "None yet: each operation downloads its own on first use." }
            : installed.Select(Describe).ToList();

        string source = ModelDownloads.Embedded is null ? options.Manifest ?? ModelDownloads.LatestManifestUrl : "built into this version";
        AboutManifest.Text = $"Models are downloaded from huggingface.co/alegauss/modelado-models; model list: {source}.";

        long downloaded = installed.Where(m => IsUnder(m.Root, library.DownloadRoot)).Sum(Bytes);
        AboutDownloadNote.Text = downloaded > 0
            ? $"Downloads go to {library.DownloadRoot}, where they take {Size(downloaded)}."
            : $"Downloads go to {library.DownloadRoot}. Nothing is downloaded yet.";
    }

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

    private void OnAboutOpenFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(library.DownloadRoot);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{library.DownloadRoot}\"") { UseShellExecute = true });
    }

    private void OnAboutRemoveModels(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, $"Delete everything downloaded to\n{library.DownloadRoot}?\n\nEach operation downloads again next time.", "Remove models", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
        {
            library.RemoveDownloads();
            FillAbout();
        }
    }
}
