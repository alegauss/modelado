using System.Globalization;
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

        // MOD49: numbers are formatted in one culture on every machine, 980,000 triangles and 0.8 s,
        // which the files and the cloud requests written from them depend on. MOD86: the text is in
        // the language About chose, Windows' own otherwise, where a translation of Strings.resx
        // (MOD85) ships for it. Both are set before any window or thread exists.
        CultureInfo numbers = CultureInfo.GetCultureInfo("en-US");
        CultureInfo language = InterfaceLanguage.ForThisRun();
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = numbers;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = language;
        base.OnStartup(e);
        AddBrandBrushes();

        (AppOptions? options, string? error) = CommandLine.Parse(e.Args);
        if (options is null)
        {
            MessageBox.Show(error, Strings.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
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
