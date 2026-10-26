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
/// A new texture from a description (MOD40), on the Edit card, by the image service MOD39 draws
/// photos with. The material is wrapped in a template asking for a flat, seamless square; the job
/// is priced and agreed to first; the image pressed is made to tile and put on the model through
/// <see cref="MaterialTexture"/> as a new version, so Undo returns to the surface it had.
/// </summary>
public partial class MainWindow
{
    private const string TextureTemplate = CloudRequests.TextureTemplate;
    private const int TextureCandidates = 4;
    private bool drawingTexture;
    private string textureMaterial = "";

    private void UpdateNewTexture()
    {
        if (NewTextureButton is null || NewTextureRun is null)
        {
            return;
        }

        ICloudProvider? service = ImageService;
        NewTextureButton.Visibility = service is not null && mesh is not null ? Visibility.Visible : Visibility.Collapsed;
        if (service is null || mesh is null)
        {
            NewTexturePanel.Visibility = Visibility.Collapsed;
            return;
        }

        NewTextureButton.Content = $"New texture with {service.DisplayName}…";
        NewTextureButton.IsEnabled = !busy;
        string estimate = service.EstimateCost(NewTextureRequest()) is { } cost ? $" · ~{cost}" : "";
        NewTextureRun.Content = $"Draw {TextureCandidates} with {service.DisplayName}{estimate}";
        NewTextureRun.IsEnabled = !busy && NewTexturePrompt.Text.Trim().Length > 0;
        NewTextureAsked.Text = $"Asked as: {TextureTemplate}";
    }

    // Before anything is typed it is priced on a placeholder, as the estimate does not read the words.
    private CloudJobRequest NewTextureRequest() =>
        CloudRequests.Texture(NewTexturePrompt.Text.Trim() is { Length: > 0 } material ? material : "material", TextureCandidates);

    private void OnNewTexture(object sender, RoutedEventArgs e)
    {
        NewTexturePanel.Visibility = NewTexturePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        UpdateNewTexture();
    }

    private void OnNewTextureChanged(object sender, TextChangedEventArgs e) => UpdateNewTexture();

    // Nothing is sent yet: the consent line says what would go, to whom, for how much.
    private void OnNewTextureRun(object sender, RoutedEventArgs e)
    {
        if (busy || ImageService is not { } service)
        {
            return;
        }

        CloudJobRequest request = NewTextureRequest();
        string cost = service.EstimateCost(request) is { } estimate ? $" for about {estimate}" : "";
        (string note, bool blocked) = BudgetNote(service, request);
        NewTextureConsentText.Text = $"This description goes to {service.DisplayName}{cost}; the model stays here.{note}";
        NewTextureSend.IsEnabled = !blocked;
        pendingCloud = request;
        NewTextureConsent.Visibility = Visibility.Visible;
    }

    private void OnNewTextureNotNow(object sender, RoutedEventArgs e)
    {
        pendingCloud = null;
        NewTextureConsent.Visibility = Visibility.Collapsed;
    }

    private async void OnNewTextureSend(object sender, RoutedEventArgs e)
    {
        NewTextureConsent.Visibility = Visibility.Collapsed;
        if (pendingCloud is not { } request || ImageService is not { } service)
        {
            return;
        }

        pendingCloud = null;
        textureMaterial = NewTexturePrompt.Text.Trim();
        NewTextureCandidates.Children.Clear();
        NewTextureStatus.Text = $"Drawing with {service.DisplayName}…";
        drawingTexture = true;
        try
        {
            await RunCloudAsync(service, request).ConfigureAwait(true);
        }
        finally
        {
            drawingTexture = false;
        }

        if (NewTextureCandidates.Children.Count == 0)
        {
            NewTextureStatus.Text = LastRun.Text;
        }
    }

    // Each candidate a button showing it, named by its place, so a reader and a case can choose.
    private Task ShowTextureCandidatesAsync(CloudResult result)
    {
        NewTextureCandidates.Children.Clear();
        IReadOnlyList<CloudImage> images = result.Images ?? [new CloudImage(result.Bytes, textureMaterial, null)];
        for (int i = 0; i < images.Count; i++)
        {
            CloudImage candidate = images[i];
            BitmapImage picture = new();
            picture.BeginInit();
            picture.StreamSource = new MemoryStream(candidate.Bytes);
            picture.DecodePixelWidth = 160;
            picture.CacheOption = BitmapCacheOption.OnLoad;
            picture.EndInit();
            picture.Freeze();
            Button choose = new()
            {
                Content = new Image { Source = picture, Width = 72, Height = 72, Stretch = System.Windows.Media.Stretch.UniformToFill },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(2),
                ToolTip = "Put this one on the model",
            };
            int place = i + 1;
            AutomationProperties.SetAutomationId(choose, $"TextureCandidate-{place}");
            AutomationProperties.SetName(choose, $"Texture {place}");
            choose.Click += async (_, _) => await ApplyTextureAsync(candidate).ConfigureAwait(true);
            NewTextureCandidates.Children.Add(choose);
        }

        NewTextureStatus.Text = images.Count == 1 ? "Press it to put it on the model." : $"{images.Count} textures: press one to put it on the model.";
        return Task.CompletedTask;
    }

    private async Task ApplyTextureAsync(CloudImage candidate)
    {
        RgbaImage tile;
        try
        {
            using MemoryStream stream = new(candidate.Bytes);
            tile = MaterialTexture.Seamless(ImageCodec.Decode(stream, "the drawn texture"));
        }
        catch (InvalidDataException error)
        {
            NewTextureStatus.Text = $"The texture could not be read: {error.Message}";
            return;
        }

        NewTexturePanel.Visibility = Visibility.Collapsed;
        NewTextureCandidates.Children.Clear();
        await EditAsync($"New texture: {textureMaterial}", (m, _, _) => MaterialTexture.Apply(m, tile)).ConfigureAwait(true);
    }
}
