using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Modelado.Imaging;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// Recent files (MOD94): the photos and models opened or saved lately, newest first, on the drop
/// zone under Open… and in the toolbar's Recent menu, each by its name and folder, a photo with a
/// thumbnail. One that is no longer there stays on the drop zone's list, greyed, with Remove from
/// list, rather than vanishing without a word; the menu leaves it out, as nothing there opens.
/// </summary>
public partial class MainWindow
{
    // How large a thumbnail is decoded: twice the 40 px it is drawn at, for a screen at 200%.
    private const int ThumbnailSide = 80;

    /// <summary>
    /// One file as the lists show it. A record for its brevity, but equal only to itself: an
    /// ItemsControl's automation peers are kept per item by Equals, so a row equal by value to the
    /// one it replaces is handed the old row's peer, whose element is gone, and the new entry never
    /// reaches the accessibility tree (MOD94, found when the thumbnails swapped the list in).
    /// </summary>
    /// <param name="Path">Its full path.</param>
    /// <param name="Name">Its file name, which the entry announces.</param>
    /// <param name="Folder">The folder it is in.</param>
    /// <param name="Exists">Whether it is still there to open.</param>
    /// <param name="Place">Its place in the list, from 1, which its AutomationIds carry.</param>
    /// <param name="Thumbnail">A photo's picture, small, or null.</param>
    private sealed record RecentRow(string Path, string Name, string Folder, bool Exists, int Place, ImageSource? Thumbnail = null)
    {
        /// <inheritdoc/>
        public bool Equals(RecentRow? other) => ReferenceEquals(this, other);

        /// <inheritdoc/>
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        public string OpenId => $"Recent-{Place}";

        public string RemoveId => $"RecentRemove-{Place}";

        public string ThumbnailId => $"RecentThumbnail-{Place}";

        public Visibility RemoveVisibility => Exists ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ThumbnailVisibility => Thumbnail is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void FillRecent()
    {
        List<RecentRow> rows = [.. RecentFiles.Load(SettingsFile.DefaultPath)
            .Select((file, i) => new RecentRow(file, Path.GetFileName(file), Path.GetDirectoryName(file) ?? "", File.Exists(file), i + 1))];
        RecentList.ItemsSource = rows;
        RecentPanel.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _ = ShowThumbnailsAsync(rows);
    }

    // The photos' pictures, decoded small on another thread and put in once they all are, unless
    // the list changed meanwhile.
    private async Task ShowThumbnailsAsync(List<RecentRow> rows)
    {
        List<RecentRow> shown = await Task.Run(() => rows.Select(row => row with { Thumbnail = Thumbnail(row) }).ToList()).ConfigureAwait(true);
        if (ReferenceEquals(RecentList.ItemsSource, rows) && shown.Any(row => row.Thumbnail is not null))
        {
            RecentList.ItemsSource = shown;
        }
    }

    private static BitmapImage? Thumbnail(RecentRow row)
    {
        if (!row.Exists || !ImageCodec.Extensions.Contains(Path.GetExtension(row.Path).ToLowerInvariant()))
        {
            return null;
        }

        try
        {
            BitmapImage picture = new();
            picture.BeginInit();
            picture.UriSource = new Uri(row.Path);
            picture.DecodePixelWidth = ThumbnailSide;
            picture.CacheOption = BitmapCacheOption.OnLoad;
            picture.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            picture.EndInit();
            picture.Freeze();
            return picture;
        }
        catch (Exception error) when (error is IOException or NotSupportedException or UnauthorizedAccessException or FileFormatException)
        {
            // A photo WPF cannot decode itself (WebP on a Windows without its codec) is listed without one.
            System.Diagnostics.Trace.WriteLine($"MOD94: no thumbnail for {row.Path}: {error.Message}");
            return null;
        }
    }

    // A file opened or saved goes to the top. Settings that cannot be written lose the list entry,
    // never the open or the save.
    private static void Remember(string file)
    {
        try
        {
            RecentFiles.Add(SettingsFile.DefaultPath, file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.WriteLine($"MOD94: {file} was not added to the recent files: {error.Message}");
        }
    }

    // The toolbar's menu, read when it opens, so it lists what was opened since the start; a file
    // no longer there is left out, as nothing in the menu could open it.
    private void OnToolbarRecent(object sender, RoutedEventArgs e)
    {
        RecentMenu.Items.Clear();
        int place = 0;
        foreach (string file in RecentFiles.Load(SettingsFile.DefaultPath).Where(File.Exists))
        {
            place++;
            MenuItem item = new() { Header = Path.GetFileName(file), Tag = file, ToolTip = file, InputGestureText = Path.GetDirectoryName(file) ?? "" };
            AutomationProperties.SetAutomationId(item, $"RecentMenu-{place}");
            item.Click += OnOpenRecent;
            RecentMenu.Items.Add(item);
        }

        if (place == 0)
        {
            MenuItem none = new() { Header = Strings.RecentNone, IsEnabled = false };
            AutomationProperties.SetAutomationId(none, "RecentMenu-None");
            RecentMenu.Items.Add(none);
        }

        RecentMenu.PlacementTarget = ToolbarRecent;
        RecentMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        RecentMenu.IsOpen = true;
    }

    private async void OnOpenRecent(object sender, RoutedEventArgs e)
    {
        if (!busy && ((FrameworkElement)sender).Tag is string file)
        {
            await OpenAsync(file).ConfigureAwait(true);
        }
    }

    private void OnRemoveRecent(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not string file)
        {
            return;
        }

        try
        {
            RecentFiles.Remove(SettingsFile.DefaultPath, file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            ShowOpenError(Ui.Format(Strings.ChoiceNotSaved, error.Message));
        }

        FillRecent();
    }
}
