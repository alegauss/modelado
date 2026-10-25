using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Modelado.Cloud;
using Modelado.Geometry;
using Modelado.Imaging;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// Cloud services in the window (MOD37): About's "Cloud services" rows, one per registered
/// provider, where a key is tested and kept; and the Cloud 3D card, shown once a service holds a
/// key, which estimates the action on its button, says what goes where before anything is sent,
/// runs the job in the job panel like a local operation, and puts what comes back in the history.
/// </summary>
public partial class MainWindow
{
    private readonly CloudKeys cloudKeys = new(CloudKeys.DefaultFolder);
    private IReadOnlyList<ICloudProvider>? cloudProviders;
    private CloudJobRequest? pendingCloud;

    private IReadOnlyList<ICloudProvider> CloudServices => cloudProviders ??= CloudProviders.Build(options.CloudApis);

    // About's half: one row per service, built here so a new provider needs no new screen.
    private void FillCloudServices()
    {
        AboutCloudServices.Children.Clear();
        foreach (ICloudProvider service in CloudServices)
        {
            AboutCloudServices.Children.Add(CloudRow(service));
        }
    }

    private StackPanel CloudRow(ICloudProvider service)
    {
        StackPanel row = new() { Margin = new Thickness(0, 0, 0, 10) };
        row.Children.Add(new TextBlock { Text = service.DisplayName, FontWeight = FontWeights.SemiBold });
        TextBlock status = new() { Style = (Style)FindResource("StatCaption"), TextWrapping = TextWrapping.Wrap, Text = cloudKeys.Has(service.Id) ? "A key is stored." : "No key." };
        PasswordBox key = new() { Margin = new Thickness(0, 4, 0, 4) };
        Button save = new() { Content = "Save and test", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 8, 0) };
        Button remove = new() { Content = "Remove", Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 8, 0), IsEnabled = cloudKeys.Has(service.Id) };
        Button page = new() { Content = "Get a key", Padding = new Thickness(12, 2, 12, 2) };
        // Named by its own text, which is what a screen reader should say.
        AutomationProperties.SetAutomationId(status, $"CloudStatus-{service.Id}");
        Identify(key, $"CloudKey-{service.Id}", $"{service.DisplayName} API key");
        Identify(save, $"CloudSave-{service.Id}", $"Save and test the {service.DisplayName} key");
        Identify(remove, $"CloudRemove-{service.Id}", $"Remove the {service.DisplayName} key");
        Identify(page, $"CloudKeyPage-{service.Id}", $"Get a {service.DisplayName} key");

        save.Click += async (_, _) =>
        {
            string typed = key.Password.Trim();
            if (typed.Length == 0)
            {
                status.Text = "Type the key first.";
                return;
            }

            save.IsEnabled = false;
            status.Text = "Testing the key…";
            try
            {
                KeyCheck check = await service.ValidateKeyAsync(typed, CancellationToken.None).ConfigureAwait(true);
                if (check.Valid)
                {
                    cloudKeys.Save(service.Id, typed);
                    key.Clear();
                }

                status.Text = check.Valid ? check.Message : $"{check.Message} It was not saved.";
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or JsonException or InvalidOperationException)
            {
                status.Text = $"{service.DisplayName} could not be reached: {error.Message}";
            }
            finally
            {
                save.IsEnabled = true;
                remove.IsEnabled = cloudKeys.Has(service.Id);
                UpdateCloudCard();
            }
        };
        remove.Click += (_, _) =>
        {
            cloudKeys.Remove(service.Id);
            status.Text = "No key.";
            remove.IsEnabled = false;
            UpdateCloudCard();
        };
        page.Click += (_, _) => Process.Start(new ProcessStartInfo(service.KeyPage.AbsoluteUri) { UseShellExecute = true });

        row.Children.Add(status);
        row.Children.Add(key);
        WrapPanel buttons = new();
        buttons.Children.Add(save);
        buttons.Children.Add(remove);
        buttons.Children.Add(page);
        row.Children.Add(buttons);
        return row;
    }

    private static void Identify(FrameworkElement element, string id, string name)
    {
        AutomationProperties.SetAutomationId(element, id);
        AutomationProperties.SetName(element, name);
    }

    // The card's half: shown once a 3D service holds a key, its button priced as the options stand.
    private void UpdateCloudCard()
    {
        if (CloudCard is null)
        {
            return;
        }

        List<ICloudProvider> ready = [.. CloudProviders.For(CloudCapability.ImageTo3D, CloudServices).Concat(CloudProviders.For(CloudCapability.TextTo3D, CloudServices))
            .Distinct().Where(p => cloudKeys.Has(p.Id))];
        CloudCard.Visibility = ready.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (ready.Count == 0)
        {
            return;
        }

        ICloudProvider? chosen = CloudService.SelectedItem as ICloudProvider;
        if (!ReferenceEquals(CloudService.ItemsSource, ready) && (CloudService.ItemsSource is not IEnumerable<ICloudProvider> shown || !shown.SequenceEqual(ready)))
        {
            CloudService.ItemsSource = ready;
            CloudService.SelectedItem = chosen is not null && ready.Contains(chosen) ? chosen : ready[0];
        }

        CloudPrompt.Visibility = CloudFromText.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        CloudPbr.IsEnabled = CloudTexture.IsChecked == true;
        if (CloudService.SelectedItem is not ICloudProvider service)
        {
            return;
        }

        CloudJobRequest request = CloudRequest(withImage: false);
        string estimate = service.EstimateCost(request) is { } cost ? string.Create(CultureInfo.InvariantCulture, $" · ~{cost.Amount:0} {cost.Unit}") : "";
        CloudGenerate.Content = $"Generate with {service.DisplayName}{estimate}";
        CloudGenerate.IsEnabled = !busy && (service.Capabilities & request.Action) == request.Action
            && (request.Action == CloudCapability.ImageTo3D ? photo is not null : !string.IsNullOrWhiteSpace(CloudPrompt.Text));
    }

    private CloudJobRequest CloudRequest(bool withImage)
    {
        bool fromPhoto = CloudFromPhoto.IsChecked == true;
        Dictionary<string, string> chosen = new(StringComparer.Ordinal)
        {
            ["texture"] = CloudTexture.IsChecked == true ? "true" : "false",
            ["pbr"] = CloudPbr.IsChecked == true && CloudTexture.IsChecked == true ? "true" : "false",
            ["topology"] = CloudQuads.IsChecked == true ? "quad" : "triangle",
        };
        return new CloudJobRequest(
            fromPhoto ? CloudCapability.ImageTo3D : CloudCapability.TextTo3D,
            Prompt: fromPhoto ? null : CloudPrompt.Text.Trim(),
            Image: fromPhoto && withImage && photo is not null ? ImageCodec.EncodePng(photo) : null,
            Options: chosen);
    }

    private void OnCloudOptionChanged(object sender, RoutedEventArgs e) => UpdateCloudCard();

    private void OnCloudPromptChanged(object sender, TextChangedEventArgs e) => UpdateCloudCard();

    // Nothing is sent yet: the consent line says what would go, to whom, for how much.
    private void OnCloudGenerate(object sender, RoutedEventArgs e)
    {
        if (busy || CloudService.SelectedItem is not ICloudProvider service)
        {
            return;
        }

        CloudJobRequest request = CloudRequest(withImage: true);
        string what = request.Image is { } image ? $"This photo, {Size(image.Length)}," : "This description";
        string cost = service.EstimateCost(request) is { } estimate ? string.Create(CultureInfo.InvariantCulture, $" for about {estimate.Amount:0} {estimate.Unit}") : "";
        CloudConsentText.Text = $"{what} goes to {service.DisplayName}{cost}.";
        pendingCloud = request;
        CloudConsent.Visibility = Visibility.Visible;
    }

    private void OnCloudNotNow(object sender, RoutedEventArgs e)
    {
        pendingCloud = null;
        CloudConsent.Visibility = Visibility.Collapsed;
    }

    private async void OnCloudSend(object sender, RoutedEventArgs e)
    {
        CloudConsent.Visibility = Visibility.Collapsed;
        if (pendingCloud is { } request && CloudService.SelectedItem is ICloudProvider service)
        {
            pendingCloud = null;
            await RunCloudAsync(service, request).ConfigureAwait(true);
        }
    }

    // As a local operation runs: the job panel, Cancel (which the service is told of), and the
    // result shown as the model's first version, labelled with the service.
    private async Task RunCloudAsync(ICloudProvider service, CloudJobRequest request)
    {
        if (busy)
        {
            return;
        }

        string title = CloudJobs.Label(service, request.Action);
        if (cloudKeys.Load(service.Id) is not { } key)
        {
            LastRunSaid($"{title}: the {service.DisplayName} key could not be read; store it again in About.");
            return;
        }

        RgbaImage? input = request.Action == CloudCapability.ImageTo3D ? photo : null;
        await FollowCloudAsync(service, request.Action, title, input, (spending, progress, tracked, token) =>
            CloudJobs.RunAsync(service, key, request, progress, token, spending: spending, tracked: tracked)).ConfigureAwait(true);
    }

    // MOD37: a job the app was following when it closed, kept in settings.json by its id, is
    // followed again at start: its result is what the user already agreed to pay for.
    private const string KeptJob = "cloudJob";
    private bool cloudRunning;

    private async Task ResumeCloudJobAsync()
    {
        if (SettingsFile.ReadObject(SettingsFile.DefaultPath, KeptJob) is not { } kept
            || kept["provider"]?.GetValue<string>() is not { } id || kept["job"]?.GetValue<string>() is not { } job
            || !Enum.TryParse(kept["action"]?.GetValue<string>(), out CloudCapability action))
        {
            return;
        }

        string title = $"{(CloudServices.FirstOrDefault(p => p.Id == id) is { } found ? CloudJobs.Label(found, action) : id)} (resumed)";
        if (CloudServices.FirstOrDefault(p => p.Id == id) is not { } service || cloudKeys.Load(id) is not { } key)
        {
            SettingsFile.Remove(SettingsFile.DefaultPath, KeptJob);
            LastRunSaid($"{title}: its key is gone, so it was left at the service.");
            return;
        }

        await FollowCloudAsync(service, action, title, null, (spending, progress, tracked, token) =>
            CloudJobs.ResumeAsync(service, key, action, job, progress, token, spending: spending, tracked: tracked)).ConfigureAwait(true);
    }

    // As a local operation runs: the job panel, Cancel (which the service is told of), and the
    // result shown as the model's first version, labelled with the service. Closing the window
    // leaves the job running at the service, kept by id for the next start.
    private async Task FollowCloudAsync(
        ICloudProvider service, CloudCapability action, string title, RgbaImage? input,
        Func<CloudSpending, IProgress<CloudJobStatus>, Action<string>, CancellationToken, Task<CloudResult>> follow)
    {
        if (busy)
        {
            return;
        }

        using CancellationTokenSource cancel = new();
        work = cancel;
        cloudRunning = true;
        SetBusy(true, title, download: false);
        Progress<CloudJobStatus> progress = new(status =>
        {
            ProgressText.Text = status.State switch
            {
                CloudJobState.Queued => $"Waiting at {service.DisplayName}",
                CloudJobState.Running => string.Create(CultureInfo.CurrentCulture, $"{status.Message ?? "Working"} · {status.Progress:P0}"),
                _ => "Fetching the model",
            };
            ProgressBar.Value = status.Progress;
        });
        void Keep(string job) => SettingsFile.Write(SettingsFile.DefaultPath, KeptJob, new System.Text.Json.Nodes.JsonObject
        {
            ["provider"] = service.Id,
            ["job"] = job,
            ["action"] = action.ToString(),
        });

        try
        {
            CloudSpending spending = new(new CloudLedger(CloudLedger.DefaultPath), new Dictionary<string, CloudBudget>());
            CloudResult result = await follow(spending, progress, Keep, cancel.Token).ConfigureAwait(true);
            SettingsFile.Remove(SettingsFile.DefaultPath, KeptJob);
            Mesh made = await Task.Run(() => CloudJobs.ToMesh(result), cancel.Token).ConfigureAwait(true);
            ShowMesh(made, input, null, title);
            string spent = result.Actual is { } actual ? string.Create(CultureInfo.InvariantCulture, $" · {actual.Amount:0} {actual.Unit}") : "";
            LastRunSaid($"{title}{spent}");
        }
        catch (OperationCanceledException)
        {
            SettingsFile.Remove(SettingsFile.DefaultPath, KeptJob);
            LastRunSaid($"{title}: cancelled");
        }
        catch (Exception error) when (error is InvalidOperationException or HttpRequestException or InvalidDataException or IOException or JsonException)
        {
            SettingsFile.Remove(SettingsFile.DefaultPath, KeptJob);
            LastRunSaid($"{title}: failed", error.Message);
        }
        finally
        {
            cloudRunning = false;
            work = null;
            SetBusy(false, title, download: false);
        }
    }
}
