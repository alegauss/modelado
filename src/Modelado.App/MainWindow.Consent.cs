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

        ConsentTitle.Text = $"{purpose} needs a one-time download";
        ConsentLead.Text = $"{purpose} uses AI {(needed.Count == 1 ? "a model" : "models")} that {(needed.Count == 1 ? "is" : "are")} downloaded the first time: " +
            $"{Size(missing)}. After that {(needed.Count == 1 ? "it works" : "they work")} offline, for every photo and model you open.";
        ConsentFiles.ItemsSource = needed
            .Select(n => $"{n.Model.Name} · {n.Variant.ToUpperInvariant()} · {Size(n.Model.Meta.Bytes + n.Model.Variants[n.Variant].Bytes)} · from {SourceOf(n.Model.Variants[n.Variant].Url)}")
            .ToList();
        ConsentTotal.Text = missing < total
            ? $"Total: {Size(missing)}, since {Size(total - missing)} of it is already here."
            : $"Total: {Size(total)}.";
        ConsentFacts.Text = string.Join(Environment.NewLine,
            "• Only the models are downloaded. Your photo stays on this computer and is never uploaded.",
            $"• They are saved in {library.DownloadRoot}, and About can remove them at any time.",
            "• Each file is checked by SHA-256 before it is used.",
            "• Modelado keeps working while it downloads. A cancelled download resumes where it stopped.");
        ConsentLicense.Text = string.Join(Environment.NewLine, needed.Select(n => $"{n.Model.Name}: {n.Model.License} ({n.Model.LicenseUrl})."));
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
