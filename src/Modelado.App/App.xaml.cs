using System.Windows;

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
}
