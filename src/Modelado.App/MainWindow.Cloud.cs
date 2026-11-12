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
        TextBlock status = new() { Style = (Style)FindResource("StatCaption"), TextWrapping = TextWrapping.Wrap, Text = KeyStatus(service.Id) };
        PasswordBox key = new() { Margin = new Thickness(0, 4, 0, 4) };
        Button save = new() { Content = Strings.SaveAndTest, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 8, 0) };
        Button replace = new() { Content = Strings.ReplaceKey, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 8, 0) };
        Button remove = new() { Content = Strings.Remove, Padding = new Thickness(12, 2, 12, 2), Margin = new Thickness(0, 0, 8, 0) };
        Button page = new() { Content = Strings.GetAKey, Padding = new Thickness(12, 2, 12, 2) };
        // Named by its own text, which is what a screen reader should say.
        AutomationProperties.SetAutomationId(status, $"CloudStatus-{service.Id}");
        Identify(key, $"CloudKey-{service.Id}", Ui.Format(Strings.ApiKeyName, service.DisplayName));
        Identify(save, $"CloudSave-{service.Id}", Ui.Format(Strings.SaveAndTestName, service.DisplayName));
        Identify(replace, $"CloudReplace-{service.Id}", Ui.Format(Strings.ReplaceKeyName, service.DisplayName));
        Identify(remove, $"CloudRemove-{service.Id}", Ui.Format(Strings.RemoveKeyName, service.DisplayName));
        Identify(page, $"CloudKeyPage-{service.Id}", Ui.Format(Strings.GetKeyName, service.DisplayName));

        // MOD96: with no key, the box to type one; with a stored one, Replace key… opens the box
        // and Remove forgets it; with one set in the environment, neither: the variable is changed.
        void Show(bool typing)
        {
            KeySource source = cloudKeys.Source(service.Id);
            bool editing = source == KeySource.None || (typing && source == KeySource.Stored);
            key.Visibility = save.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
            replace.Visibility = source == KeySource.Stored && !editing ? Visibility.Visible : Visibility.Collapsed;
            remove.Visibility = source == KeySource.Stored ? Visibility.Visible : Visibility.Collapsed;
        }

        Show(typing: false);
        replace.Click += (_, _) =>
        {
            Show(typing: true);
            key.Focus();
        };

        save.Click += async (_, _) =>
        {
            string typed = key.Password.Trim();
            if (typed.Length == 0)
            {
                status.Text = Strings.TypeKeyFirst;
                return;
            }

            save.IsEnabled = false;
            status.Text = Strings.TestingKey;
            try
            {
                KeyCheck check = await service.ValidateKeyAsync(typed, CancellationToken.None).ConfigureAwait(true);
                if (check.Valid)
                {
                    cloudKeys.Save(service.Id, typed);
                    key.Clear();
                }

                status.Text = check.Valid ? check.Message : Ui.Format(Strings.KeyNotSaved, check.Message);
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or JsonException or InvalidOperationException)
            {
                status.Text = Ui.Format(Strings.ServiceUnreachable, service.DisplayName, error.Message);
            }
            finally
            {
                save.IsEnabled = true;
                Show(typing: key.Password.Length > 0);
                UpdateCloudCard();
            }
        };
        remove.Click += (_, _) =>
        {
            cloudKeys.Remove(service.Id);
            status.Text = KeyStatus(service.Id);
            Show(typing: false);
            UpdateCloudCard();
        };
        page.Click += (_, _) => Process.Start(new ProcessStartInfo(service.KeyPage.AbsoluteUri) { UseShellExecute = true });

        row.Children.Add(status);
        row.Children.Add(key);
        WrapPanel buttons = new();
        buttons.Children.Add(save);
        buttons.Children.Add(replace);
        buttons.Children.Add(remove);
        buttons.Children.Add(page);
        row.Children.Add(buttons);
        row.Children.Add(CloudUsageRow(service));
        return row;
    }

    // Where the key comes from and its last four characters, never more of it.
    private string KeyStatus(string providerId) => cloudKeys.Source(providerId) switch
    {
        KeySource.Environment => Ui.Format(Strings.KeyFromEnvironment, CloudKeys.Variable(providerId), cloudKeys.Hint(providerId)),
        KeySource.Stored => Ui.Format(Strings.KeyStoredBy, cloudKeys.Hint(providerId)),
        _ => Strings.NoKey,
    };

    private static void Identify(FrameworkElement element, string id, string name)
    {
        AutomationProperties.SetAutomationId(element, id);
        AutomationProperties.SetName(element, name);
    }

    // The card's half: shown once a 3D service holds a key, its button priced as the options stand.
    private void UpdateCloudCard()
    {
        // MOD60: a cloud option checked in XAML raises its event inside InitializeComponent, before
        // the elements after it exist; Loaded fills the card once they all do.
        if (!IsInitialized)
        {
            return;
        }

        UpdateCloudEdit();
        UpdateGenerate();
        UpdateNewTexture();
        UpdateCloudChip();

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
        CloudGenerate.Content = service.EstimateCost(request) is { } cost
            ? Ui.Format(Strings.GenerateWithCost, service.DisplayName, cost)
            : Ui.Format(Strings.GenerateWith, service.DisplayName);
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
        CloudCost? estimate = service.EstimateCost(request);
        (string note, bool blocked) = BudgetNote(service, request);
        CloudConsentText.Text = (request.Image, estimate) switch
        {
            ({ } image, null) => Ui.Format(Strings.ConsentPhoto, Size(image.Length), service.DisplayName, note),
            ({ } image, _) => Ui.Format(Strings.ConsentPhotoCost, Size(image.Length), service.DisplayName, estimate, note),
            (null, null) => Ui.Format(Strings.ConsentDescription, service.DisplayName, note),
            _ => Ui.Format(Strings.ConsentDescriptionCost, service.DisplayName, estimate, note),
        };
        CloudConsentSend.IsEnabled = !blocked;
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
            LastRunSaid(Ui.Format(Strings.KeyUnreadable, title, service.DisplayName));
            return;
        }

        RgbaImage? input = request.Action == CloudCapability.ImageTo3D ? photo : null;

        // A new model is the first version of a new history; an edit of the open one is pushed onto
        // its history, so Undo returns to the local version.
        Func<CloudResult, Task> land = request.Action switch
        {
            CloudCapability.Remesh or CloudCapability.Retexture => AsMesh(made =>
            {
                history.Push(made, title);
                Display(made);
            }),
            CloudCapability.TextToImage when drawingTexture => ShowTextureCandidatesAsync,
            CloudCapability.TextToImage => ShowCandidatesAsync,
            _ => AsMesh(made => ShowMesh(made, input, null, title)),
        };
        await FollowCloudAsync(service, request.Action, title, land, (spending, progress, tracked, token) =>
            CloudJobs.RunAsync(service, key, request, progress, token, spending: spending, tracked: tracked)).ConfigureAwait(true);
    }

    // MOD38's half: the open model remeshed or retextured by the first service with a key that can.
    private ICloudProvider? CloudEditService => CloudServices.FirstOrDefault(p =>
        (p.Capabilities & (CloudCapability.Remesh | CloudCapability.Retexture)) != CloudCapability.None && cloudKeys.Has(p.Id));

    private int? RemeshPolycount =>
        int.TryParse(CloudRemeshPolycount.Text.Replace(",", "", StringComparison.Ordinal).Replace(".", "", StringComparison.Ordinal), NumberStyles.None, CultureInfo.InvariantCulture, out int count)
        && count is >= 100 and <= 300_000 ? count : null;

    private void UpdateCloudEdit()
    {
        // The card's check boxes raise Checked while the window is still being built, before this
        // group below them exists.
        if (CloudEditGroup is null || CloudRetexture is null || CloudKeepUv is null)
        {
            return;
        }

        ICloudProvider? service = CloudEditService;
        CloudEditGroup.Visibility = service is not null && mesh is not null ? Visibility.Visible : Visibility.Collapsed;
        if (service is null || mesh is null)
        {
            return;
        }

        CloudEditTitle.Text = Ui.Format(Strings.WithService, service.DisplayName);
        CloudKeepUv.IsEnabled = mesh.TexCoords is not null;
        CloudRemesh.Content = Priced(service, CloudCapability.Remesh, Strings.Remesh, Strings.RemeshCost);
        CloudRetexture.Content = Priced(service, CloudCapability.Retexture, Strings.Retexture, Strings.RetextureCost);
        CloudRemesh.IsEnabled = !busy && (service.Capabilities & CloudCapability.Remesh) != 0 && RemeshPolycount is not null;
        CloudRetexture.IsEnabled = !busy && (service.Capabilities & CloudCapability.Retexture) != 0 && !string.IsNullOrWhiteSpace(CloudRetexturePrompt.Text);
    }

    // The action's label, with its estimate where the service gives one.
    private static string Priced(ICloudProvider service, CloudCapability action, string label, string labelWithCost) =>
        service.EstimateCost(new CloudJobRequest(action)) is { } cost ? Ui.Format(labelWithCost, cost) : label;

    private CloudJobRequest CloudEditRequest(CloudCapability action, byte[] model) => action == CloudCapability.Remesh
        ? CloudRequests.Remesh(model, RemeshPolycount!.Value, CloudRemeshQuads.IsChecked == true)
        : CloudRequests.Retexture(model, CloudRetexturePrompt.Text, CloudKeepUv.IsEnabled && CloudKeepUv.IsChecked == true);

    private void OnCloudEditChanged(object sender, TextChangedEventArgs e) => UpdateCloudEdit();

    private void OnCloudRemesh(object sender, RoutedEventArgs e) => AskCloudEdit(CloudCapability.Remesh);

    private void OnCloudRetexture(object sender, RoutedEventArgs e) => AskCloudEdit(CloudCapability.Retexture);

    private void AskCloudEdit(CloudCapability action)
    {
        if (busy || mesh is null || CloudEditService is not { } service)
        {
            return;
        }

        using MemoryStream glb = new();
        MeshExport.WriteGlb(mesh, glb);
        CloudJobRequest request = CloudEditRequest(action, glb.ToArray());
        (string note, bool blocked) = BudgetNote(service, request);
        CloudEditConsentText.Text = service.EstimateCost(request) is { } estimate
            ? Ui.Format(Strings.ConsentModelCost, mesh.TriangleCount, Size(glb.Length), service.DisplayName, estimate, note)
            : Ui.Format(Strings.ConsentModel, mesh.TriangleCount, Size(glb.Length), service.DisplayName, note);
        CloudEditSend.IsEnabled = !blocked;
        pendingCloud = request;
        CloudEditConsent.Visibility = Visibility.Visible;
    }

    private void OnCloudEditNotNow(object sender, RoutedEventArgs e)
    {
        pendingCloud = null;
        CloudEditConsent.Visibility = Visibility.Collapsed;
    }

    private async void OnCloudEditSend(object sender, RoutedEventArgs e)
    {
        CloudEditConsent.Visibility = Visibility.Collapsed;
        if (pendingCloud is { } request && CloudEditService is { } service)
        {
            pendingCloud = null;
            await RunCloudAsync(service, request).ConfigureAwait(true);
        }
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

        string title = Ui.Format(Strings.ResumedJob, CloudServices.FirstOrDefault(p => p.Id == id) is { } found ? CloudJobs.Label(found, action) : id);
        if (CloudServices.FirstOrDefault(p => p.Id == id) is not { } service || cloudKeys.Load(id) is not { } key)
        {
            SettingsFile.Remove(SettingsFile.DefaultPath, KeptJob);
            LastRunSaid(Ui.Format(Strings.ResumedKeyGone, title));
            return;
        }

        Func<CloudResult, Task> land = action == CloudCapability.TextToImage ? ShowCandidatesAsync : AsMesh(made => ShowMesh(made, null, null, title));
        await FollowCloudAsync(service, action, title, land, (spending, progress, tracked, token) =>
            CloudJobs.ResumeAsync(service, key, action, job, progress, token, spending: spending, tracked: tracked)).ConfigureAwait(true);
    }

    // As a local operation runs: the job panel, Cancel (which the service is told of), and the
    // result shown as the model's first version, labelled with the service. Closing the window
    // leaves the job running at the service, kept by id for the next start.
    private async Task FollowCloudAsync(
        ICloudProvider service, CloudCapability action, string title, Func<CloudResult, Task> land,
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
                CloudJobState.Queued => Ui.Format(Strings.WaitingAt, service.DisplayName),
                CloudJobState.Running => Ui.Format(Strings.WorkingProgress, status.Message ?? Strings.Working, status.Progress),
                _ => Strings.FetchingModel,
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
            CloudResult result = await follow(Spending(), progress, Keep, cancel.Token).ConfigureAwait(true);
            SettingsFile.Remove(SettingsFile.DefaultPath, KeptJob);
            await land(result).ConfigureAwait(true);
            LastRunSaid(result.Actual is { } actual ? Ui.Format(Strings.CloudDone, title, actual) : title);
        }
        catch (OperationCanceledException)
        {
            SettingsFile.Remove(SettingsFile.DefaultPath, KeptJob);
            LastRunSaid(Ui.Format(Strings.StepCancelled, title));
        }
        catch (Exception error) when (error is InvalidOperationException or HttpRequestException or InvalidDataException or IOException or JsonException)
        {
            SettingsFile.Remove(SettingsFile.DefaultPath, KeptJob);
            LastRunSaid(Ui.Format(Strings.StepFailed, title), error.Message);
        }
        finally
        {
            cloudRunning = false;
            work = null;
            SetBusy(false, title, download: false);
            UpdateCloudChip();
        }
    }
}
