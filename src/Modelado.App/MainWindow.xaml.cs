using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Modelado.Geometry;
using Modelado.Imaging;
using Modelado.Inference;
using Modelado.Inference.Pipelines;

namespace Modelado.App;

/// <summary>
/// The main window: the photo or the model in the viewer on the left, the two operations on the
/// right. This part holds the photo, the viewer and the export; the operations and the
/// downloads are in the other MainWindow.*.cs files.
/// </summary>
public partial class MainWindow : Window, IDisposable
{
    private readonly AppOptions options;
    private readonly ModelLibrary library;
    private readonly ExecutionProvider provider;
    private readonly string? providerReason;
    private ModelRunner runner;

    private RgbaImage? photo;
    private string? photoPath;
    private Mesh? mesh;
    private MeshRenderer? renderer;
    private RgbaImage? modelInput;
    private RgbaImage? reference;
    private readonly MeshHistory history = new();
    private OrbitCamera camera = new();

    private WriteableBitmap? bitmap;
    private uint[] pixels = [];
    private bool renderQueued;
    private bool dragging;
    private Point dragFrom;
    private bool disposed;

    /// <summary>Opens the window with the command line's options.</summary>
    /// <param name="options">What the app was started with.</param>
    public MainWindow(AppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
        ThemeMode = Theme.Mode(Appearance.Default.Theme);
        InitializeComponent();

        // The toolbar's glyphs on the Ghost template, which dims a command that cannot run (MOD5).
        foreach (Button button in EditorToolbar.Items.OfType<Button>().Append(ResetViewButton))
        {
            button.Template = Templates.Ghost();
            button.Padding = new Thickness(6, 4, 6, 4);
        }

        library = options.Models is { } folder ? new ModelLibrary([folder]) : ModelLibrary.Default();
        try
        {
            (provider, providerReason) = ExecutionProviders.Resolve(options.Provider);
        }
        catch (InvalidOperationException error)
        {
            (provider, providerReason) = (ExecutionProvider.Cpu, error.Message);
        }

        StartFirstUse();

        // MOD10: the formats the drop zone lists are the ones the codecs read, not a copy of them.
        DropFormats.Text = $"Photos: {Formats(ImageCodec.Extensions)}\n3D models: {Formats(MeshImport.Extensions)}";

        QualityHigh.IsChecked = options.HighQuality;
        runner = new ModelRunner(library, provider, options.HighQuality);
        ProviderStatus.Text = ProviderLabel;
        if (providerReason is not null)
        {
            // Why it is not the GPU, where a person looking at "CPU" will look.
            ProviderChip.ToolTip = providerReason;
            AutomationProperties.SetHelpText(ProviderStatus, providerReason);
        }

        Drop += OnDrop;
        Closed += (_, _) => Dispose();
        Loaded += async (_, _) =>
        {
            await RefreshModelStatusAsync().ConfigureAwait(true);
            if (options.Open is { } open)
            {
                await OpenAsync(open).ConfigureAwait(true);
            }
        };
    }

    /// <summary>Releases the sessions and the download client.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        work?.Cancel();
        work?.Dispose();
        runner.Dispose();
        http.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Back to the front view, Home; the toolbar's button and a double-click on the model (MOD5).</summary>
    public static readonly RoutedUICommand ResetViewCommand = new(
        "Front view", "ResetView", typeof(MainWindow), [new KeyGesture(Key.Home)]);

    private void CanOpen(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy;

    private void CanSave(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && mesh is not null && ExportSizeValue is not null;

    private void CanResetView(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = ShowingModel;

    private async void OnOpen(object sender, ExecutedRoutedEventArgs e)
    {
        string photos = string.Join(';', ImageCodec.Extensions.Select(x => "*" + x));
        string models = string.Join(';', MeshImport.Extensions.Select(x => "*" + x));
        OpenFileDialog dialog = new()
        {
            Title = "Open a photo or a 3D model",
            Filter = $"Photos and 3D models|{photos};{models}|Photos|{photos}|3D models (GLB, OBJ, PLY, STL)|{models}|All files|*.*",
        };
        if (dialog.ShowDialog(this) == true)
        {
            await OpenAsync(dialog.FileName).ConfigureAwait(true);
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            await OpenAsync(files[0]).ConfigureAwait(true);
        }
    }

    private async Task OpenAsync(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (MeshImport.Extensions.Contains(extension))
        {
            await OpenModelAsync(path).ConfigureAwait(true);
        }
        else if (ImageCodec.Extensions.Contains(extension))
        {
            OpenPhoto(path);
        }
        else
        {
            ShowOpenError($"{Path.GetFileName(path)} is not a photo or a 3D model Modelado can open.");
        }
    }

    // MOD10: why a file could not be opened, said in place and never in a dialog: on the drop zone
    // while nothing is open, over the viewer otherwise, where what is shown stays.
    private void ShowOpenError(string text)
    {
        bool empty = photo is null && mesh is null;
        OpenError.Text = empty ? text : "";
        OpenError.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        ViewerOpenError.Text = empty ? "" : text;
        ViewerOpenErrorBar.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string WhyNotOpened(string path, Exception error) => error is FileNotFoundException or DirectoryNotFoundException
        ? $"{Path.GetFileName(path)} was not found."
        : $"{Path.GetFileName(path)} could not be opened: {error.Message}";

    // A file is open: the errors about earlier ones go, and the operations panel appears.
    private void Opened()
    {
        if (AboutOpen)
        {
            ShowAbout(false);
        }

        OpenError.Visibility = Visibility.Collapsed;
        ViewerOpenErrorBar.Visibility = Visibility.Collapsed;
        EmptyHint.Visibility = Visibility.Collapsed;
        Operations.Visibility = Visibility.Visible;
        LastRunSaid(null);
    }

    // A model to edit and improve: no photo, so the two reconstructions wait for one.
    private async Task OpenModelAsync(string path)
    {
        if (busy)
        {
            return;
        }

        Mesh loaded;
        string? shown = SourceChip.Visibility == Visibility.Visible ? SourceText.Text : null;
        try
        {
            ShowSource($"Opening {Path.GetFileName(path)}…");
            loaded = await Task.Run(() => MeshImport.Load(path)).ConfigureAwait(true);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            ShowSource(shown);
            ShowOpenError(WhyNotOpened(path, error));
            return;
        }

        photo = null;
        photoPath = path;
        Title = $"{Path.GetFileName(path)} · Modelado";
        ObjectButton.IsEnabled = ReliefButton.IsEnabled = false;
        ShowMesh(loaded, null, null, "Opened");
        ShowSource(Path.GetFileName(path));
        Opened();
    }

    private void OpenPhoto(string path)
    {
        if (busy)
        {
            return;
        }

        RgbaImage loaded;
        try
        {
            loaded = ImageCodec.Load(path);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            ShowOpenError(WhyNotOpened(path, error));
            return;
        }

        photo = loaded;
        photoPath = path;
        mesh = null;
        renderer = null;
        modelInput = null;
        reference = null;
        UpdateEditButtons();
        Title = $"{Path.GetFileName(path)} · Modelado";
        MeshChip.Visibility = Visibility.Collapsed;
        HistoryCard.Visibility = Visibility.Collapsed;
        ExportCard.Visibility = Visibility.Collapsed;
        ExportStatus.Visibility = Visibility.Collapsed;
        ViewChoice.IsEnabled = false;
        ObjectButton.IsEnabled = ReliefButton.IsEnabled = true;
        ObjectRemoveBackground.IsChecked = !photo.HasTransparency;
        ShowImage(photo);
        ShowSource($"{Path.GetFileName(path)} · {photo.Width} × {photo.Height}{(photo.HasTransparency ? " · cut out" : "")}");
        Opened();
    }

    // A new model, from a reconstruction or a file: the history starts over and the view resets.
    private void ShowMesh(Mesh result, RgbaImage? input, RgbaImage? projectFrom, string label)
    {
        modelInput = input;
        reference = projectFrom;
        history.Reset(result, label);
        camera = new OrbitCamera();
        ViewChoice.SelectedIndex = ViewChoice.SelectedIndex == 3 ? 0 : ViewChoice.SelectedIndex;
        Display(result);
    }

    // The model now shown: after a new one, an edit, an undo or a redo; the camera stays.
    private void Display(Mesh result)
    {
        mesh = result;
        renderer = new MeshRenderer(result);
        ViewChoice.IsEnabled = true;
        ViewerHint.Visibility = Visibility.Visible;
        MeshChip.Visibility = Visibility.Visible;
        HistoryList.ItemsSource = history.Labels.Select((label, i) => $"{i + 1}. {label}").ToList();
        HistoryList.SelectedIndex = history.Position;
        HistoryList.ScrollIntoView(HistoryList.SelectedItem);
        HistoryCard.Visibility = Visibility.Visible;
        ExportCard.Visibility = Visibility.Visible;
        UpdateExportCard();
        _ = RefreshFirstUseAsync();
        MeshText.Text = $"{result.VertexCount:N0} vertices · {result.TriangleCount:N0} triangles" +
            $"{(result.Texture is null ? "" : $" · texture {result.Texture.Width} × {result.Texture.Height}")}{(result.DoubleSided ? " · open" : "")}";
        UpdateEditButtons();
        RequestRender();
    }

    private bool ShowingModel => renderer is not null && ViewChoice.SelectedIndex != 3;

    private RenderStyle ViewStyle => ViewChoice.SelectedIndex switch
    {
        1 => RenderStyle.Clay,
        2 => RenderStyle.Wireframe,
        _ => RenderStyle.Color,
    };

    private void OnViewChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (ShowingModel)
        {
            ViewerHint.Visibility = Visibility.Visible;
            RequestRender();
        }
        else if ((modelInput ?? photo) is { } shown)
        {
            ViewerHint.Visibility = Visibility.Collapsed;
            ShowImage(shown);
        }
    }

    private void OnResetView(object sender, RoutedEventArgs e)
    {
        camera = new OrbitCamera();
        RequestRender();
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e) => RequestRender();

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ShowingModel)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            OnResetView(sender, e);
            return;
        }

        dragging = true;
        dragFrom = e.GetPosition(Viewport);
        Viewport.CaptureMouse();
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        if (!dragging)
        {
            return;
        }

        Point now = e.GetPosition(Viewport);
        camera = camera.Dragged(now.X - dragFrom.X, now.Y - dragFrom.Y);
        dragFrom = now;
        RequestRender();
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (dragging)
        {
            dragging = false;
            Viewport.ReleaseMouseCapture();
            RequestRender();
        }
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ShowingModel)
        {
            camera = camera.Zoomed(e.Delta / 120.0);
            RequestRender();
        }
    }

    // Renders at most once per frame, whatever number of mouse moves asked for it.
    private void RequestRender()
    {
        if (!renderQueued && ShowingModel)
        {
            renderQueued = true;
            CompositionTarget.Rendering += RenderFrame;
        }
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        CompositionTarget.Rendering -= RenderFrame;
        renderQueued = false;
        if (!ShowingModel || Viewport.ActualWidth < 2 || Viewport.ActualHeight < 2)
        {
            return;
        }

        // The screen's own pixels; half of them while dragging, so turning stays smooth.
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double scale = dragging ? 0.5 : 1.0;
        int width = Math.Max(2, (int)(Viewport.ActualWidth * dpi.DpiScaleX * scale));
        int height = Math.Max(2, (int)(Viewport.ActualHeight * dpi.DpiScaleY * scale));
        if (bitmap is null || bitmap.PixelWidth != width || bitmap.PixelHeight != height)
        {
            bitmap = new WriteableBitmap(width, height, 96 * dpi.DpiScaleX * scale, 96 * dpi.DpiScaleY * scale, PixelFormats.Bgra32, null);
            pixels = new uint[width * height];
        }

        renderer!.Render(pixels, width, height, camera, ViewStyle);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        ViewImage.Stretch = Stretch.Fill;
        ViewImage.Source = bitmap;
    }

    private void ShowImage(RgbaImage image)
    {
        byte[] bgra = new byte[image.Pixels.Length];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = image.Pixels[i + 2];
            bgra[i + 1] = image.Pixels[i + 1];
            bgra[i + 2] = image.Pixels[i];
            bgra[i + 3] = image.Pixels[i + 3];
        }

        BitmapSource source = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, bgra, image.Width * 4);
        source.Freeze();
        ViewImage.Stretch = Stretch.Uniform;
        ViewImage.Source = source;
    }

    // ".png", ".jpg", ".jpeg" -> "PNG, JPG, JPEG".
    private static string Formats(IEnumerable<string> extensions) =>
        string.Join(", ", extensions.Select(x => x.TrimStart('.').ToUpperInvariant()));

    /// <summary>Where inference runs, as the provider chip names it.</summary>
    private string ProviderLabel => provider == ExecutionProvider.DirectML ? "DirectML" : "CPU";

    // The source chip: what is open, or being opened. Null hides it.
    private void ShowSource(string? text)
    {
        SourceText.Text = text ?? "";
        SourceChip.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // The last-run chip: what the last run, edit, save, undo or download came to, with the detail
    // behind it as the tooltip. Null hides it, as opening a new file does: it described another.
    private void LastRunSaid(string? text, string? detail = null)
    {
        LastRun.Text = text ?? "";
        LastRunChip.ToolTip = detail;
        LastRunChip.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }
}
