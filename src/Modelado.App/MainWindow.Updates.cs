using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// The release check (MOD30, after freewilly's DD154): once after the window shows and every 24
/// hours, never during an operation, it asks GitHub's release API for the latest release. A newer
/// one shows as a chip on the status bar that opens About at its Updates section, where a person
/// can also check by hand and turn the automatic check off. A check nobody asked for says nothing
/// when it fails; one a person asked for says it could not reach GitHub.
/// </summary>
public partial class MainWindow
{
    private readonly HttpClient releasesHttp = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromHours(24) };

    // An automatic check that came due during an operation waits for it, a minute at a time.
    private readonly DispatcherTimer updateRetry = new() { Interval = TimeSpan.FromMinutes(1) };
    private AvailableRelease? newerRelease;
    private string updateSaid = "Not checked yet.";
    private bool checkingUpdates;

    // --releases off stops the automatic check for this run; a URL stands in for GitHub's API.
    private bool AutomaticUpdates => options.Releases != "off" && UpdateSetting.Load(SettingsFile.DefaultPath);

    private string? ReleasesUrl => options.Releases is null or "off" ? null : options.Releases;

    private void StartUpdateChecks()
    {
        updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync(false).ConfigureAwait(true);
        updateRetry.Tick += async (_, _) =>
        {
            updateRetry.Stop();
            await CheckForUpdatesAsync(false).ConfigureAwait(true);
        };
        if (AutomaticUpdates)
        {
            updateTimer.Start();
            _ = CheckForUpdatesAsync(false);
        }
    }

    private async Task CheckForUpdatesAsync(bool asked)
    {
        if (checkingUpdates || (!asked && !AutomaticUpdates))
        {
            return;
        }

        if (!asked && busy)
        {
            updateRetry.Start();
            return;
        }

        checkingUpdates = true;
        AboutCheckUpdates.IsEnabled = false;
        if (asked)
        {
            AboutUpdateStatus.Text = "Checking…";
        }

        try
        {
            ReleaseAnswer answer = await new ReleaseCheck(new HttpReleaseFeed(releasesHttp, ReleasesUrl)).AskAsync().ConfigureAwait(true);
            if (answer.Reached)
            {
                newerRelease = answer.Newer;
                updateSaid = answer.Newer is { } newer
                    ? $"Modelado {newer.Version} is available; this is {ReleaseCheck.ThisBuild}."
                    : $"Modelado {ReleaseCheck.ThisBuild} is the latest release.";
            }
            else if (asked)
            {
                updateSaid = "Could not reach GitHub to check for updates.";
            }

            ShowUpdate();
        }
        finally
        {
            checkingUpdates = false;
            AboutCheckUpdates.IsEnabled = true;
        }
    }

    private void ShowUpdate()
    {
        UpdateChip.Content = newerRelease is { } newer ? $"Update {newer.Version} available" : "";
        UpdateChip.Visibility = newerRelease is null ? Visibility.Collapsed : Visibility.Visible;
        AboutUpdateStatus.Text = updateSaid;
        AboutReleasePage.Visibility = newerRelease?.PageUrl is null ? Visibility.Collapsed : Visibility.Visible;
        AboutInstallUpdate.Visibility = newerRelease is null ? Visibility.Collapsed : Visibility.Visible;
        AboutInstallUpdate.IsEnabled = !installingUpdate;
    }

    // MOD31: each version of the model shown, and the one last opened from or saved to a file; an
    // update restarts the app, which loses the history, so a version never saved is asked about.
    private int shownVersion;
    private int savedVersion;
    private bool installingUpdate;

    private bool UnsavedModel => mesh is not null && shownVersion != savedVersion;

    private async void OnInstallUpdate(object sender, RoutedEventArgs e)
    {
        if (newerRelease is null || installingUpdate)
        {
            return;
        }

        if (busy)
        {
            updateSaid = $"{ProgressTitle.Text} is running: let it finish or cancel it, then update.";
            ShowUpdate();
            return;
        }

        if (UnsavedModel)
        {
            AboutUpdateConfirm.Visibility = Visibility.Visible;
            return;
        }

        await InstallUpdateAsync(newerRelease).ConfigureAwait(true);
    }

    private async void OnUpdateAnyway(object sender, RoutedEventArgs e)
    {
        AboutUpdateConfirm.Visibility = Visibility.Collapsed;
        if (newerRelease is { } release && !busy)
        {
            await InstallUpdateAsync(release).ConfigureAwait(true);
        }
    }

    private void OnUpdateKeep(object sender, RoutedEventArgs e) => AboutUpdateConfirm.Visibility = Visibility.Collapsed;

    // Downloads and verifies the installer, starts it and exits; the installer upgrades in place and
    // starts Modelado again. A refused or failed download says why and installs nothing.
    private async Task InstallUpdateAsync(AvailableRelease release)
    {
        installingUpdate = true;
        ShowUpdate();
        Progress<DownloadProgress> progress = new(p =>
        {
            updateSaid = p.Stage == DownloadStage.Verifying
                ? $"Checking Modelado {release.Version} against {ReleaseCheck.SumsAssetName}…"
                : $"Downloading Modelado {release.Version}: {100 * p.Done / Math.Max(1, p.Total)}%";
            ShowUpdate();
        });
        try
        {
            string installer = await ReleaseUpdate.DownloadAsync(http, release, ReleaseUpdate.DefaultFolder, progress, CancellationToken.None).ConfigureAwait(true);
            updateSaid = $"Installing Modelado {release.Version}; it starts again when done.";
            ShowUpdate();
            Process.Start(ReleaseUpdate.InstallerStart(installer));
            Application.Current.Shutdown();
        }
        catch (InvalidDataException refused)
        {
            updateSaid = $"{refused.Message} Nothing was installed.";
        }
        catch (Exception error) when (error is HttpRequestException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            updateSaid = $"The update could not be installed: {error.Message}";
        }
        finally
        {
            installingUpdate = false;
        }

        ShowUpdate();
    }

    // About's half, filled each time it opens.
    private void FillUpdates()
    {
        fillingUpdates = true;
        AboutAutoUpdate.IsChecked = UpdateSetting.Load(SettingsFile.DefaultPath);
        fillingUpdates = false;
        ShowUpdate();
    }

    private bool fillingUpdates;

    private void OnUpdateChip(object sender, RoutedEventArgs e)
    {
        if (!AboutOpen)
        {
            ShowAbout(true);
        }

        AboutUpdatesHeader.BringIntoView();
    }

    private async void OnCheckForUpdates(object sender, RoutedEventArgs e) => await CheckForUpdatesAsync(true).ConfigureAwait(true);

    private void OnOpenReleasePage(object sender, RoutedEventArgs e)
    {
        if (newerRelease?.PageUrl is { } page)
        {
            Process.Start(new ProcessStartInfo(page) { UseShellExecute = true });
        }
    }

    private async void OnAutoUpdateChanged(object sender, RoutedEventArgs e)
    {
        if (fillingUpdates || !IsLoaded)
        {
            return;
        }

        bool on = AboutAutoUpdate.IsChecked == true;
        try
        {
            UpdateSetting.Save(SettingsFile.DefaultPath, on);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            updateSaid = $"The choice could not be saved: {error.Message}";
            ShowUpdate();
            return;
        }

        if (!on)
        {
            updateTimer.Stop();
            updateRetry.Stop();
            return;
        }

        updateTimer.Start();
        await CheckForUpdatesAsync(false).ConfigureAwait(true);
    }
}
