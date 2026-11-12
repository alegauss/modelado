using System.IO;
using System.Windows;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// Recent files (MOD94): the photos and models opened or saved lately, listed on the drop zone
/// under Open… newest first, each by its name and folder. One that is no longer there stays on the
/// list, greyed, with Remove from list, rather than vanishing without a word.
/// </summary>
public partial class MainWindow
{
    /// <summary>One file as the drop zone lists it.</summary>
    /// <param name="Path">Its full path.</param>
    /// <param name="Name">Its file name, which the entry announces.</param>
    /// <param name="Folder">The folder it is in.</param>
    /// <param name="Exists">Whether it is still there to open.</param>
    /// <param name="OpenId">The entry's AutomationId.</param>
    /// <param name="RemoveId">Its Remove from list button's AutomationId.</param>
    private sealed record RecentRow(string Path, string Name, string Folder, bool Exists, string OpenId, string RemoveId)
    {
        public Visibility RemoveVisibility => Exists ? Visibility.Collapsed : Visibility.Visible;
    }

    private void FillRecent()
    {
        IReadOnlyList<string> files = RecentFiles.Load(SettingsFile.DefaultPath);
        RecentList.ItemsSource = files
            .Select((file, i) => new RecentRow(file, Path.GetFileName(file), Path.GetDirectoryName(file) ?? "", File.Exists(file), $"Recent-{i + 1}", $"RecentRemove-{i + 1}"))
            .ToList();
        RecentPanel.Visibility = files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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

    private async void OnOpenRecent(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is string file)
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
