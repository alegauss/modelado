using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// What Modelado is, where its models are, under which licenses, and the buttons to download
/// every model for offline use or remove what was downloaded. Built in code: one list, a few
/// buttons.
/// </summary>
public sealed class AboutWindow : Window
{
    private readonly ModelLibrary library;
    private readonly StackPanel list = new();

    /// <summary>Builds the window.</summary>
    /// <param name="library">The models folders.</param>
    /// <param name="provider">Where inference runs.</param>
    /// <param name="manifest">Where the models manifest comes from, as shown.</param>
    public AboutWindow(ModelLibrary library, ExecutionProvider provider, string manifest)
    {
        ArgumentNullException.ThrowIfNull(library);
        this.library = library;
        Title = "Sobre o Modelado";
        Width = 640;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 760;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ThemeMode = ThemeMode.System;
        FontFamily = (FontFamily)Application.Current.FindResource("AppFont");

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
        StackPanel root = new() { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock { Text = $"Modelado {version}", FontSize = 20, FontWeight = FontWeights.SemiBold });
        root.Children.Add(Paragraph(
            "Transforma uma foto em um modelo 3D neste computador: um objeto completo com o TripoSR, ou um relevo com o Depth Anything V2. " +
            $"Inferência no ONNX Runtime, {(provider == ExecutionProvider.DirectML ? "na GPU com DirectML" : "na CPU")}. Nenhuma foto é enviada a lugar nenhum."));
        root.Children.Add(Paragraph($"Modelos baixados de huggingface.co/alegauss/modelado-models; lista de modelos: {manifest}."));

        root.Children.Add(new TextBlock { Text = "Modelos instalados", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 14, 0, 6) });
        root.Children.Add(new ScrollViewer { Content = list, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        WrapPanel buttons = new() { Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(Button("Baixar todos (uso offline)", (_, _) => DownloadAllRequested?.Invoke(this, EventArgs.Empty)));
        buttons.Children.Add(Button("Abrir a pasta dos modelos", (_, _) => OpenFolder(library.DownloadRoot)));
        buttons.Children.Add(Button("Remover modelos baixados", (_, _) => Remove()));
        buttons.Children.Add(Button("Fechar", (_, _) => Close()));
        root.Children.Add(buttons);
        Content = root;
        Fill();
    }

    /// <summary>Raised when the user asks for every model to be downloaded.</summary>
    public event EventHandler? DownloadAllRequested;

    private static TextBlock Paragraph(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    private static Button Button(string text, RoutedEventHandler click)
    {
        Button button = new() { Content = text, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 8) };
        button.Click += click;
        return button;
    }

    private static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private void Fill()
    {
        list.Children.Clear();
        IReadOnlyList<InstalledModel> installed = library.Installed();
        if (installed.Count == 0)
        {
            list.Children.Add(Paragraph("Nenhum ainda: cada operação baixa os seus na primeira vez."));
        }

        foreach (InstalledModel model in installed)
        {
            TextBlock row = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
            row.Inlines.Add(new Run(model.Name) { FontWeight = FontWeights.SemiBold });
            long bytes = model.Variants.Sum(v => model.Meta.Variants[v].Bytes);
            row.Inlines.Add(new Run($"  {string.Join(", ", model.Variants)} · {bytes / 1e6:0} MB · {model.Meta.Task}\n"));
            Hyperlink license = new(new Run(model.Meta.Upstream.License)) { NavigateUri = new Uri(model.Meta.Upstream.LicenseUrl) };
            license.RequestNavigate += (_, e) => Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            row.Inlines.Add(license);
            row.Inlines.Add(new Run($"  ·  {model.Root}") { Foreground = (Brush)FindResource("TextFillColorTertiaryBrush") });
            list.Children.Add(row);
        }
    }

    private void Remove()
    {
        if (MessageBox.Show(this, $"Apagar tudo o que foi baixado em\n{library.DownloadRoot}?\n\nCada operação baixa de novo na próxima vez.", "Remover modelos", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
        {
            library.RemoveDownloads();
            Fill();
        }
    }
}
