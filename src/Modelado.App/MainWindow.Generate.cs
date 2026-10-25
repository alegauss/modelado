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

        DropGenerate.Content = $"Generate a photo with {service.DisplayName}…";
        CloudJobRequest request = GenerateRequest();
        string estimate = service.EstimateCost(request) is { } cost ? $" · ~{cost}" : "";
        string count = ComboTag(GenerateCount);
        GenerateRun.Content = $"Generate {count} with {service.DisplayName}{estimate}";
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
        GeneratePanel.Visibility = GeneratePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        UpdateGenerate();
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
        string cost = service.EstimateCost(request) is { } estimate ? $" for about {estimate}" : "";
        GenerateConsentText.Text = $"This description goes to {service.DisplayName}{cost}.";
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
            GenerateCandidates.Children.Clear();
            GenerateStatus.Text = $"Drawing with {service.DisplayName}…";
            await RunCloudAsync(service, request).ConfigureAwait(true);
            if (GenerateCandidates.Children.Count == 0)
            {
                GenerateStatus.Text = LastRun.Text;
            }
        }
    }

    // Each candidate a button showing it, named by its place, so a reader and a case can choose.
    private Task ShowCandidatesAsync(CloudResult result)
    {
        GenerateCandidates.Children.Clear();
        IReadOnlyList<CloudImage> images = result.Images ?? [new CloudImage(result.Bytes, generatePrompt, null)];
        for (int i = 0; i < images.Count; i++)
        {
            CloudImage candidate = images[i];
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
                ToolTip = "Open this one",
            };
            int place = i + 1;
            AutomationProperties.SetAutomationId(choose, $"Candidate-{place}");
            AutomationProperties.SetName(choose, $"Candidate {place}");
            choose.Click += async (_, _) => await OpenCandidateAsync(candidate).ConfigureAwait(true);
            GenerateCandidates.Children.Add(choose);
        }

        GenerateStatus.Text = images.Count == 1 ? "Press it to open it." : $"{images.Count} candidates: press one to open it.";
        return Task.CompletedTask;
    }

    // Saved where generated photos are kept, carrying what made it, then opened as if dropped.
    private async Task OpenCandidateAsync(CloudImage candidate)
    {
        string folder = Path.Combine(Path.GetDirectoryName(CloudLedger.DefaultPath)!, "generated");
        string path = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"ideogram-{DateTime.Now:yyyyMMdd-HHmmss}-{candidate.Seed ?? 0}.png"));
        try
        {
            RgbaImage image;
            using (MemoryStream stream = new(candidate.Bytes))
            {
                image = ImageCodec.Decode(stream, "the generated image");
            }

            Dictionary<string, string> text = new(StringComparer.Ordinal) { ["Prompt"] = candidate.Prompt.Length > 0 ? candidate.Prompt : generatePrompt, ["Software"] = "Modelado, Ideogram 3.0" };
            if (candidate.Seed is { } seed)
            {
                text["Seed"] = seed.ToString(CultureInfo.InvariantCulture);
            }

            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(path, ImageCodec.EncodePng(image, text)).ConfigureAwait(true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            GenerateStatus.Text = $"The image could not be kept: {error.Message}";
            return;
        }

        GeneratePanel.Visibility = Visibility.Collapsed;
        GenerateCandidates.Children.Clear();
        await OpenAsync(path).ConfigureAwait(true);
    }
}
