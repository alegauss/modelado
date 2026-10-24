using System.Windows;
using System.Windows.Media;
using Modelado.Imaging;

namespace Modelado.App;

/// <summary>The application: parses the command line and opens the main window with it.</summary>
public partial class App : Application
{
    /// <summary>The exit code for a command line that was refused.</summary>
    public const int BadCommandLine = 2;

    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnStartup(e);
        AddBrandBrushes();

        (AppOptions? options, string? error) = CommandLine.Parse(e.Args);
        if (options is null)
        {
            MessageBox.Show(error, "Modelado", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(BadCommandLine);
            return;
        }

        MainWindow window = new(options);
        MainWindow = window;
        window.Show();
    }

    // Brand's colours become brushes here, the window's edge, under the keys the XAML names.
    private void AddBrandBrushes()
    {
        Resources["ViewportGroundBrush"] = Frozen(Brand.ViewportBottom);
        Resources["HintTitleBrush"] = Frozen(Brand.HintTitle);
        Resources["HintTextBrush"] = Frozen(Brand.HintText);
    }

    private static SolidColorBrush Frozen((byte R, byte G, byte B) colour)
    {
        SolidColorBrush brush = new(Color.FromRgb(colour.R, colour.G, colour.B));
        brush.Freeze();
        return brush;
    }
}
