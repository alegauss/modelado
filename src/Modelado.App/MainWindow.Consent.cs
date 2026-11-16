using System.Windows;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// The consent panel (MOD21, after PhotoBoost's PB58): before a first-use download, what is
/// downloaded and how large, from where, where it goes, that the photo stays here and each model's
/// license, in the window rather than a dialog. Not now changes nothing else: every operation that
/// has its models stays usable, and the card that needed them still says so.
/// </summary>
public partial class MainWindow
{
    private TaskCompletionSource<bool>? consent;
    private bool aboutBeforeConsent;

    /// <summary>Shows the offer and answers whether it was accepted.</summary>
    private Task<bool> AskConsentAsync(string purpose, string accept, ModelManifest listed, IReadOnlyList<ModelRequest> requests, long missing)
    {
        consent?.TrySetResult(false);
        consent = new TaskCompletionSource<bool>();

        List<(ManifestModel Model, string Variant)> needed = [.. requests.Select(r => (listed.Model(r.Model)!, ModelDownloads.VariantOf(listed, r)!))];
        long total = ModelDownloads.TotalBytes(listed, requests);

        ConsentTitle.Text = Ui.Format(Strings.ConsentTitleText, purpose);
        ConsentLead.Text = Ui.Format(needed.Count == 1 ? Strings.ConsentLeadOne : Strings.ConsentLeadMany, purpose, Size(missing));
        ConsentFiles.ItemsSource = needed
            .Select(n => Ui.Format(Strings.ConsentFile, n.Model.Name, n.Variant.ToUpperInvariant(), Size(n.Model.Meta.Bytes + n.Model.Variants[n.Variant].TotalBytes), SourceOf(n.Model.Variants[n.Variant].Url)))
            .ToList();
        ConsentTotal.Text = missing < total
            ? Ui.Format(Strings.ConsentTotalPartial, Size(missing), Size(total - missing))
            : Ui.Format(Strings.ConsentTotalAll, Size(total));
        ConsentFacts.Text = string.Join(Environment.NewLine,
            Strings.ConsentFactPhoto,
            Ui.Format(Strings.ConsentFactFolder, ShownDownloadRoot, RemovedWithTheApp),
            Strings.ConsentFactChecked,
            Strings.ConsentFactResume);
        ConsentLicense.Text = string.Join(Environment.NewLine, needed.Select(n => Ui.Format(Strings.ConsentLicenseLine, n.Model.Name, n.Model.License, n.Model.LicenseUrl)));
        ConsentAccept.Content = accept;
        ShowConditions([.. needed.Select(n => n.Model).Where(m => m.Conditions is not null).DistinctBy(m => m.Name)]);

        aboutBeforeConsent = AboutOpen;
        AboutPanel.Visibility = Visibility.Collapsed;
        ViewerFrame.Visibility = Visibility.Collapsed;
        Operations.Visibility = Visibility.Collapsed;
        ConsentPanel.Visibility = Visibility.Visible;
        ConsentAccept.Focus();
        return consent.Task;
    }

    // Where a file comes from, as a person would name it: huggingface.co/alegauss/modelado-models.
    // The owner and the repository, which are the first two folders; never the file itself.
    private static string SourceOf(string url)
    {
        Uri uri = new(url);
        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/', segments.SkipLast(1).Take(2).Prepend(uri.Authority));
    }

    private void HideConsent(bool accepted)
    {
        ConsentPanel.Visibility = Visibility.Collapsed;
        if (aboutBeforeConsent)
        {
            AboutPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ViewerFrame.Visibility = Visibility.Visible;
            Operations.Visibility = photo is not null || mesh is not null ? Visibility.Visible : Visibility.Collapsed;
        }

        TaskCompletionSource<bool>? answered = consent;
        consent = null;
        answered?.TrySetResult(accepted);
    }

    // MOD123: the conditioned models in this download not yet agreed to, which Accept waits on.
    private List<ManifestModel> unagreed = [];

    /// <summary>
    /// MOD123: each model whose license sets conditions, its terms in plain words, the full text,
    /// the notice it requires and where a commercial user registers; the box to agree when any is not
    /// yet agreed to (Accept waits on it), or when each was agreed to otherwise.
    /// </summary>
    private void ShowConditions(IReadOnlyList<ManifestModel> conditioned)
    {
        string settings = SettingsFile.DefaultPath;
        unagreed = [.. conditioned.Where(m => ModelConsents.Agreed(settings, m) is null)];
        ConsentConditions.Visibility = conditioned.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ConsentConditionsText.Text = string.Join(Environment.NewLine + Environment.NewLine, conditioned.Select(m =>
        {
            ModelConditions terms = m.Conditions!;
            List<string> lines = [Ui.Format(Strings.ConsentConditionsModel, m.Name, m.License, terms.Summary), Ui.Format(Strings.ConsentConditionsFullText, m.LicenseUrl)];
            if (terms.Attribution is { } notice)
            {
                lines.Add(Ui.Format(Strings.ConsentConditionsNotice, notice));
            }

            if (terms.RegisterUrl is { } register)
            {
                lines.Add(Ui.Format(Strings.ConsentConditionsRegister, register));
            }

            if (ModelConsents.Agreed(settings, m) is { } at)
            {
                lines.Add(Ui.Format(Strings.ConsentConditionsAgreed, at.ToLocalTime().ToString("d", System.Globalization.CultureInfo.CurrentCulture)));
            }

            return string.Join(Environment.NewLine, lines);
        }));
        ConsentAgree.IsChecked = false;
        ConsentAgree.Visibility = unagreed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ConsentAccept.IsEnabled = unagreed.Count == 0;
    }

    private void OnConsentAgreeChanged(object sender, RoutedEventArgs e) => ConsentAccept.IsEnabled = unagreed.Count == 0 || ConsentAgree.IsChecked == true;

    private void OnConsentAccept(object sender, RoutedEventArgs e)
    {
        if (unagreed.Count > 0)
        {
            if (ConsentAgree.IsChecked != true)
            {
                return;
            }

            ModelConsents.Record(SettingsFile.DefaultPath, unagreed, DateTimeOffset.UtcNow);
            unagreed = [];
        }

        HideConsent(accepted: true);
    }

    private void OnConsentDecline(object sender, RoutedEventArgs e) => HideConsent(accepted: false);
}
