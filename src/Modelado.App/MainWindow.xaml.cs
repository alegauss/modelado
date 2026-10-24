using System.IO;
using System.Windows;
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

        QualityHigh.IsChecked = options.HighQuality;
        runner = new ModelRunner(library, provider, options.HighQuality);
        ProviderText.Text = provider == ExecutionProvider.DirectML ? "GPU · DirectML" : "CPU";
        if (providerReason is not null)
        {
            Status(providerReason);
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

    private void CanSave(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && mesh is not null;

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
        if (MeshImport.Extensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
        {
            await OpenModelAsync(path).ConfigureAwait(true);
        }
        else
        {
            OpenPhoto(path);
        }
    }

    // A model to edit and improve: no photo, so the two reconstructions wait for one.
    private async Task OpenModelAsync(string path)
    {
        if (busy)
        {
            return;
        }

        Mesh loaded;
        try
        {
            Status($"Opening {Path.GetFileName(path)}…");
            loaded = await Task.Run(() => MeshImport.Load(path)).ConfigureAwait(true);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"{Path.GetFileName(path)} could not be opened.\n\n{error.Message}", "Modelado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        photo = null;
        photoPath = path;
        Title = $"{Path.GetFileName(path)} · Modelado";
        EmptyHint.Visibility = Visibility.Collapsed;
        ObjectButton.IsEnabled = ReliefButton.IsEnabled = false;
        ShowMesh(loaded, null, null, "Opened");
        Status($"{Path.GetFileName(path)}: {loaded.VertexCount:N0} vertices, {loaded.TriangleCount:N0} triangles{(loaded.Texture is null ? "" : $", texture {loaded.Texture.Width} × {loaded.Texture.Height}")}{(loaded.DoubleSided ? ", open surface" : ", closed")}.");
    }

    private void OpenPhoto(string path)
    {
        if (busy)
        {
            return;
        }

        try
        {
            photo = ImageCodec.Load(path);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"{Path.GetFileName(path)} could not be opened.\n\n{error.Message}", "Modelado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        photoPath = path;
        mesh = null;
        renderer = null;
        modelInput = null;
        reference = null;
        UpdateEditButtons();
        Title = $"{Path.GetFileName(path)} · Modelado";
        EmptyHint.Visibility = Visibility.Collapsed;
        MeshChip.Visibility = Visibility.Collapsed;
        ViewChoice.IsEnabled = false;
        ObjectButton.IsEnabled = ReliefButton.IsEnabled = true;
        ObjectRemoveBackground.IsChecked = !photo.HasTransparency;
        ShowImage(photo);
        Status($"{Path.GetFileName(path)}: {photo.Width} × {photo.Height}{(photo.HasTransparency ? ", with transparency (its cut-out is used)" : "")}.");
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
        MeshText.Text = $"{result.VertexCount:N0} vertices · {result.TriangleCount:N0} triangles{(result.DoubleSided ? " · open" : "")}";
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

    private void OnExport(object sender, ExecutedRoutedEventArgs e)
    {
        if (mesh is null || busy)
        {
            return;
        }

        SaveFileDialog dialog = new()
        {
            Title = "Save the 3D model",
            FileName = Path.GetFileNameWithoutExtension(photoPath ?? "model"),
            Filter = "GLB, binary glTF (Windows, Blender, web)|*.glb|OBJ with texture or colours|*.obj|STL for 3D printing (in mm)|*.stl|PLY with colours (MeshLab)|*.ply",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            // Slicers read STL as millimetres: 100 mm on the largest side. Elsewhere 1 unit (a metre in glTF).
            bool stl = MeshExport.FormatOf(dialog.FileName) == MeshFormat.Stl;
            IReadOnlyList<string> written = MeshExport.Save(mesh.Placed(stl ? 100 : 1), dialog.FileName);
            Status($"Saved: {string.Join(", ", written.Select(Path.GetFileName))}{(stl ? " (100 mm on the longest side; the slicer can rescale it)" : "")}.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, $"The model could not be saved.\n\n{error.Message}", "Modelado", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        AboutWindow about = new(library, provider, ModelDownloads.Embedded is null ? ModelDownloads.LatestManifestUrl : "built into this version") { Owner = this };
        about.DownloadAllRequested += async (_, _) =>
        {
            about.Close();
            await DownloadAsync([.. ObjectReconstruction.Models.Concat(ReliefReconstruction.Models(true)).Append(Upscaling.Model).Distinct()]).ConfigureAwait(true);
        };
        about.ShowDialog();
        library.Refresh();
        _ = RefreshModelStatusAsync();
    }

    private void Status(string text) => StatusText.Text = text;
}
