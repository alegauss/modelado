using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modelado.Cloud;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.App;

/// <summary>
/// A photo drawn from a description (MOD39), on the drop zone, by the first service holding a key
/// that can make images (Ideogram). The description is wrapped in a template asking for what a 3D
/// reconstruction works from, shown and editable; the job is priced and agreed to first; the
/// candidates come back side by side, and the one pressed is saved with its prompt and seed in the
/// PNG and opened as if it had been dropped, so every operation takes it unchanged.
/// </summary>
public partial class MainWindow
{
    private string generatePrompt = "";

    private ICloudProvider? ImageService => CloudServices.FirstOrDefault(p => (p.Capabilities & CloudCapability.TextToImage) != 0 && cloudKeys.Has(p.Id));

    /// <summary>A mesh result, read off the window's thread and then landed.</summary>
    private static Func<CloudResult, Task> AsMesh(Action<Mesh> land) => async result =>
    {
        Mesh made = await Task.Run(() => CloudJobs.ToMesh(result)).ConfigureAwait(true);
        land(made);
    };

    private static string ComboTag(ComboBox box) => (string)((ComboBoxItem)box.SelectedItem).Tag;

    private void UpdateGenerate()
    {
        if (DropGenerate is null || GenerateRun is null || GenerateCount is null)
        {
            return;
        }

        ICloudProvider? service = ImageService;
        DropGenerate.Visibility = service is null ? Visibility.Collapsed : Visibility.Visible;
        if (service is null)
        {
            GeneratePanel.Visibility = Visibility.Collapsed;
            return;
        }

        // MOD93: the button says what pressing it now does, so it never sits beside its own open
        // panel looking as if it did nothing.
        DropGenerate.Content = GeneratePanel.Visibility == Visibility.Visible
            ? Strings.HideGenerate
            : Ui.Format(Strings.GeneratePhotoWith, service.DisplayName);
        CloudJobRequest request = GenerateRequest();
        string count = ComboTag(GenerateCount);
        GenerateRun.Content = service.EstimateCost(request) is { } cost
            ? Ui.Format(Strings.GenerateCountWithCost, count, service.DisplayName, cost)
            : Ui.Format(Strings.GenerateCountWith, count, service.DisplayName);
        GenerateRun.IsEnabled = !busy && GeneratePrompt.Text.Trim().Length > 0 && GenerateTemplate.Text.Contains("{object}", StringComparison.Ordinal);
    }

    private CloudJobRequest GenerateRequest() => new(
        CloudCapability.TextToImage,
        Prompt: GenerateTemplate.Text.Replace("{object}", GeneratePrompt.Text.Trim(), StringComparison.Ordinal),
        Options: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["count"] = ComboTag(GenerateCount),
            ["speed"] = ComboTag(GenerateSpeed),
            ["aspect"] = ComboTag(GenerateAspect),
        });

    private void OnDropGenerate(object sender, RoutedEventArgs e)
    {
        bool opening = GeneratePanel.Visibility != Visibility.Visible;
        GeneratePanel.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
        UpdateGenerate();

        // The panel opened is where the typing goes, once it has been laid out to take the focus.
        if (opening)
        {
            Dispatcher.BeginInvoke(() => GeneratePrompt.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnGenerateChanged(object sender, TextChangedEventArgs e) => UpdateGenerate();

    private void OnGenerateOptionChanged(object sender, SelectionChangedEventArgs e) => UpdateGenerate();

    // Nothing is sent yet: the consent line says what would go, to whom, for how much.
    private void OnGenerateRun(object sender, RoutedEventArgs e)
    {
        if (busy || ImageService is not { } service)
        {
            return;
        }

        CloudJobRequest request = GenerateRequest();
        (string note, bool blocked) = BudgetNote(service, request);
        GenerateConsentText.Text = service.EstimateCost(request) is { } estimate
            ? Ui.Format(Strings.ConsentDescriptionCost, service.DisplayName, estimate, note)
            : Ui.Format(Strings.ConsentDescription, service.DisplayName, note);
        GenerateSend.IsEnabled = !blocked;
        pendingCloud = request;
        GenerateConsent.Visibility = Visibility.Visible;
    }

    private void OnGenerateNotNow(object sender, RoutedEventArgs e)
    {
        pendingCloud = null;
        GenerateConsent.Visibility = Visibility.Collapsed;
    }

    private async void OnGenerateSend(object sender, RoutedEventArgs e)
    {
        GenerateConsent.Visibility = Visibility.Collapsed;
        if (pendingCloud is { } request && ImageService is { } service)
        {
            pendingCloud = null;
            generatePrompt = request.Prompt ?? "";
            generateObject = GeneratePrompt.Text.Trim();
            GenerateCandidates.Children.Clear();
            GenerateStatus.Text = Ui.Format(Strings.DrawingWith, service.DisplayName);
            await RunCloudAsync(service, request).ConfigureAwait(true);
            if (GenerateCandidates.Children.Count == 0)
            {
                GenerateStatus.Text = LastRun.Text;
            }
        }
    }

    // MOD95: every candidate is paid for, so each is kept the moment the job returns, then shown as
    // a button named by its place, so a reader and a case can choose. One that could not be kept is
    // said with where it was to go, and written again when pressed.
    private async Task ShowCandidatesAsync(CloudResult result)
    {
        GenerateCandidates.Children.Clear();
        IReadOnlyList<CloudImage> images = result.Images ?? [new CloudImage(result.Bytes, generatePrompt, null)];
        string? notKept = null;
        string?[] kept = new string?[images.Count];
        for (int i = 0; i < images.Count; i++)
        {
            try
            {
                kept[i] = await KeepAsync(images[i]).ConfigureAwait(true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                notKept = Ui.Format(Strings.GeneratedNotKept, GeneratedFolder, error.Message);
            }
        }

        for (int i = 0; i < images.Count; i++)
        {
            CloudImage candidate = images[i];
            string? path = kept[i];
            BitmapImage picture = new();
            picture.BeginInit();
            picture.StreamSource = new MemoryStream(candidate.Bytes);
            picture.DecodePixelWidth = 200;
            picture.CacheOption = BitmapCacheOption.OnLoad;
            picture.EndInit();
            picture.Freeze();
            Button choose = new()
            {
                Content = new Image { Source = picture, Width = 96, Height = 96, Stretch = System.Windows.Media.Stretch.Uniform },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(2),
                ToolTip = Strings.OpenThisOne,
            };
            int place = i + 1;
            AutomationProperties.SetAutomationId(choose, $"Candidate-{place}");
            AutomationProperties.SetName(choose, Ui.Format(Strings.CandidateName, place));
            choose.Click += async (_, _) => await OpenCandidateAsync(candidate, path).ConfigureAwait(true);
            GenerateCandidates.Children.Add(choose);
        }

        GenerateStatus.Text = notKept ?? (images.Count == 1 ? Strings.PressToOpen : Ui.Format(Strings.CandidatesPress, images.Count));
        FillGenerated();
    }

    // The chosen candidate opens as if dropped: from where it was kept, or written now if it was not.
    private async Task OpenCandidateAsync(CloudImage candidate, string? kept)
    {
        string path;
        try
        {
            path = kept ?? await KeepAsync(candidate).ConfigureAwait(true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            GenerateStatus.Text = Ui.Format(Strings.GeneratedNotKept, GeneratedFolder, error.Message);
            return;
        }

        GeneratePanel.Visibility = Visibility.Collapsed;
        GenerateCandidates.Children.Clear();
        UpdateGenerate();
        await OpenAsync(path).ConfigureAwait(true);
    }
}
