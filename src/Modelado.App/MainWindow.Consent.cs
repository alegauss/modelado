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
            .Select(n => Ui.Format(Strings.ConsentFile, n.Model.Name, n.Variant.ToUpperInvariant(), Size(n.Model.Meta.Bytes + n.Model.Variants[n.Variant].Bytes), SourceOf(n.Model.Variants[n.Variant].Url)))
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

    private void OnConsentAccept(object sender, RoutedEventArgs e) => HideConsent(accepted: true);

    private void OnConsentDecline(object sender, RoutedEventArgs e) => HideConsent(accepted: false);
}
