using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Modelado.Cloud;
using Modelado.Imaging;

namespace Modelado.App;

/// <summary>
/// Generated photos (MOD95): every candidate a service draws is paid for, so each is written the
/// moment the job returns, to Pictures\Modelado (the user's own library, which the Store package
/// does not hide), named after the object and the seed and carrying its prompt and seed in the
/// PNG. The drop zone shows the latest as a strip that opens like a drop, and a link to the folder.
/// </summary>
public partial class MainWindow
{
    /// <summary>The variable that puts generated photos elsewhere, as a UI case does in its own folder.</summary>
    private const string GeneratedVariable = "MODELADO_GENERATED";

    // How many of the latest the drop zone shows.
    private const int GeneratedShown = 6;

    private string generateObject = "";

    /// <summary>Where generated photos are kept.</summary>
    private static string GeneratedFolder =>
        Environment.GetEnvironmentVariable(GeneratedVariable) is { Length: > 0 } folder
            ? folder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Modelado");

    // Written with what made it, under a name that is the object's and the seed's: a-wooden-chair-7.png.
    private async Task<string> KeepAsync(CloudImage candidate)
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

        string folder = GeneratedFolder;
        Directory.CreateDirectory(folder);
        string path = FreeName(folder, Slug(generateObject), candidate.Seed?.ToString(CultureInfo.InvariantCulture) ?? DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        await File.WriteAllBytesAsync(path, ImageCodec.EncodePng(image, text)).ConfigureAwait(true);
        return path;
    }

    // "A wooden chair!" -> "a-wooden-chair": letters and digits of any script, the rest one hyphen.
    private static string Slug(string described)
    {
        StringBuilder slug = new();
        foreach (char c in described.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        string trimmed = slug.ToString().Trim('-');
        return trimmed.Length == 0 ? "ideogram" : trimmed[..Math.Min(trimmed.Length, 40)].Trim('-');
    }

    // The same object and seed twice is the same picture drawn twice, kept as a second file.
    private static string FreeName(string folder, string slug, string seed)
    {
        string path = Path.Combine(folder, $"{slug}-{seed}.png");
        for (int n = 2; File.Exists(path); n++)
        {
            path = Path.Combine(folder, $"{slug}-{seed}-{n}.png");
        }

        return path;
    }

    /// <summary>The drop zone's strip: the latest generated photos, each opening like a drop.</summary>
    private void FillGenerated()
    {
        GeneratedStrip.Children.Clear();
        string folder = GeneratedFolder;
        List<FileInfo> latest = Directory.Exists(folder)
            ? [.. new DirectoryInfo(folder).EnumerateFiles("*.png").OrderByDescending(file => file.LastWriteTimeUtc).Take(GeneratedShown)]
            : [];
        for (int i = 0; i < latest.Count; i++)
        {
            string path = latest[i].FullName;
            Button open = new()
            {
                Content = new Image { Source = SmallPicture(path), Width = 56, Height = 56, Stretch = System.Windows.Media.Stretch.UniformToFill },
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(2),
                ToolTip = path,
            };
            AutomationProperties.SetAutomationId(open, $"Generated-{i + 1}");
            AutomationProperties.SetName(open, latest[i].Name);
            open.Click += async (_, _) => await OpenAsync(path).ConfigureAwait(true);
            GeneratedStrip.Children.Add(open);
        }

        GeneratedPanel.Visibility = latest.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static BitmapImage? SmallPicture(string path)
    {
        try
        {
            BitmapImage picture = new();
            picture.BeginInit();
            picture.UriSource = new Uri(path);
            picture.DecodePixelWidth = 112;
            picture.CacheOption = BitmapCacheOption.OnLoad;
            picture.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            picture.EndInit();
            picture.Freeze();
            return picture;
        }
        catch (Exception error) when (error is IOException or NotSupportedException or UnauthorizedAccessException or FileFormatException)
        {
            Trace.WriteLine($"MOD95: no picture for {path}: {error.Message}");
            return null;
        }
    }

    private void OnOpenGeneratedFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(GeneratedFolder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{GeneratedFolder}\"") { UseShellExecute = true });
    }
}
