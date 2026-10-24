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
        InitializeComponent();

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
                OpenPhoto(open);
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

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Title = "Abrir a foto de um objeto",
            Filter = "Imagens|" + string.Join(';', ImageCodec.Extensions.Select(x => "*" + x)) + "|Todos os arquivos|*.*",
        };
        if (dialog.ShowDialog(this) == true)
        {
            OpenPhoto(dialog.FileName);
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            OpenPhoto(files[0]);
        }
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
            MessageBox.Show(this, $"Não foi possível abrir {Path.GetFileName(path)}.\n\n{error.Message}", "Modelado", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        photoPath = path;
        mesh = null;
        renderer = null;
        modelInput = null;
        Title = $"{Path.GetFileName(path)} · Modelado";
        EmptyHint.Visibility = Visibility.Collapsed;
        MeshChip.Visibility = Visibility.Collapsed;
        ExportButton.IsEnabled = false;
        ResetViewButton.IsEnabled = false;
        ViewChoice.IsEnabled = false;
        ObjectButton.IsEnabled = ReliefButton.IsEnabled = true;
        ObjectRemoveBackground.IsChecked = !photo.HasTransparency;
        ShowImage(photo);
        Status($"{Path.GetFileName(path)}: {photo.Width} × {photo.Height}{(photo.HasTransparency ? ", com transparência (o recorte dela é usado)" : "")}.");
    }

    private void ShowMesh(Mesh result, RgbaImage input)
    {
        mesh = result;
        modelInput = input;
        renderer = new MeshRenderer(result);
        camera = new OrbitCamera();
        ViewChoice.IsEnabled = ExportButton.IsEnabled = ResetViewButton.IsEnabled = true;
        ViewChoice.SelectedIndex = ViewChoice.SelectedIndex == 3 ? 0 : ViewChoice.SelectedIndex;
        ViewerHint.Visibility = Visibility.Visible;
        MeshChip.Visibility = Visibility.Visible;
        MeshText.Text = $"{result.VertexCount:N0} vértices · {result.TriangleCount:N0} triângulos";
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

    private void OnExport(object sender, RoutedEventArgs e)
    {
        if (mesh is null || busy)
        {
            return;
        }

        SaveFileDialog dialog = new()
        {
            Title = "Exportar o modelo 3D",
            FileName = Path.GetFileNameWithoutExtension(photoPath ?? "modelo"),
            Filter = "GLB, glTF binário (Windows, Blender, web)|*.glb|OBJ com textura ou cores|*.obj|STL para impressão 3D (em mm)|*.stl|PLY com cores (MeshLab)|*.ply",
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
            Status($"Salvo: {string.Join(", ", written.Select(Path.GetFileName))}{(stl ? " (100 mm no lado maior; o fatiador pode escalar)" : "")}.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, $"Não foi possível salvar.\n\n{error.Message}", "Modelado", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        AboutWindow about = new(library, provider, ModelDownloads.Embedded is null ? ModelDownloads.LatestManifestUrl : "embutido nesta versão") { Owner = this };
        about.DownloadAllRequested += async (_, _) =>
        {
            about.Close();
            await DownloadAsync([.. ObjectReconstruction.Models.Concat(ReliefReconstruction.Models(true)).Distinct()]).ConfigureAwait(true);
        };
        about.ShowDialog();
        library.Refresh();
        _ = RefreshModelStatusAsync();
    }

    private void Status(string text) => StatusText.Text = text;
}
