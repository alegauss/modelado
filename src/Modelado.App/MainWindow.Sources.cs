using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Modelado.Inference;
using Modelado.Inference.Pipelines;

namespace Modelado.App;

/// <summary>
/// Model sources (MOD27): repositories a person adds in About, read after the built-in manifest
/// through <see cref="ModelSources"/>, and the choice of depth model they bring to the Relief card.
/// --manifest replaces the built-in models and the added sources for that one run.
/// </summary>
public partial class MainWindow
{
    private IReadOnlyList<ModelSource>? sources;
    private ModelManifest? builtInManifest;
    private string? sourcesError;
    private (ModelSource Source, ModelManifest Manifest)? proposed;

    // The source each added model came from, for the card's choice: built-in models are not in it.
    private Dictionary<string, string> sourceOfModel = new(StringComparer.Ordinal);

    private IReadOnlyList<ModelSource> Sources => sources ??= options.Manifest is null ? ModelSources.Load(SettingsFile.DefaultPath) : [];

    /// <summary>The built-in manifest with the added sources merged after it; a source that fails is said in About.</summary>
    private async Task<ModelManifest> WithSourcesAsync(ModelManifest loaded)
    {
        builtInManifest = loaded;
        sourcesError = null;
        sourceOfModel = new(StringComparer.Ordinal);
        if (Sources.Count == 0)
        {
            return loaded;
        }

        try
        {
            ModelManifest merged = await ModelSources.WithSourcesAsync(http, loaded, Sources, CancellationToken.None).ConfigureAwait(true);
            foreach (ManifestModel model in merged.Models.Skip(loaded.Models.Count))
            {
                sourceOfModel[model.Name] = SourceListing(model.Meta.Url);
            }

            return merged;
        }
        catch (Exception error) when (error is HttpRequestException or InvalidDataException or TaskCanceledException)
        {
            // The built-in models still work; About says why the added ones are missing.
            sourcesError = $"The added sources could not be read, so only the built-in models are offered: {error.Message}";
            return loaded;
        }
    }

    // Which source listed a file: the one whose manifest URL shares its host and folder.
    private string SourceListing(string url) =>
        Sources.FirstOrDefault(s => url.StartsWith(s.ManifestUrl[..(s.ManifestUrl.LastIndexOf('/') + 1)], StringComparison.Ordinal))?.Given ?? SourceOf(url);

    /// <summary>One source as About lists it.</summary>
    private sealed record SourceRow(int Index, string Label, string LabelId, string RemoveId, string RemoveName, Visibility RemoveVisibility);

    private void FillSources()
    {
        List<SourceRow> rows = [new(-1, "alegauss/modelado-models (built in)", "Source-0", "RemoveSource-0", "", Visibility.Collapsed)];
        rows.AddRange(Sources.Select((s, i) => new SourceRow(i, s.Given == s.ManifestUrl ? s.Given : $"{s.Given} · {s.ManifestUrl}", $"Source-{i + 1}", $"RemoveSource-{i + 1}", $"Remove {s.Given}", Visibility.Visible)));
        AboutSources.ItemsSource = rows;
        if (options.Manifest is not null)
        {
            SaySource($"--manifest {options.Manifest} is used for this run instead of the built-in models and the added sources.");
        }
        else
        {
            SaySource(sourcesError);
        }
    }

    private async void OnAddSource(object sender, RoutedEventArgs e)
    {
        string given = AboutSourceInput.Text.Trim();
        if (given.Length == 0)
        {
            SaySource("Type a Hugging Face repository (owner/name) or a manifest URL first.");
            return;
        }

        if (Sources.Any(s => string.Equals(s.Given, given, StringComparison.OrdinalIgnoreCase)))
        {
            SaySource($"{given} is already a source.");
            return;
        }

        AboutAddSource.IsEnabled = false;
        AboutSourcePreview.Visibility = Visibility.Collapsed;
        SaySource(null);
        try
        {
            if (await ManifestAsync().ConfigureAwait(true) is null || builtInManifest is null)
            {
                SaySource("The built-in model list could not be read, so nothing can be checked against it. Check the connection and try again.");
                return;
            }

            ModelSource source = await ModelSources.ResolveAsync(http, given, CancellationToken.None).ConfigureAwait(true);
            Uri url = new(source.ManifestUrl);
            ModelManifest listed = ModelManifest.Parse(await http.GetStringAsync(url).ConfigureAwait(true), loopback: url.IsLoopback);
            _ = ModelSources.Merge(builtInManifest, [.. await ReadSourcesAsync().ConfigureAwait(true), (source, listed)]);
            proposed = (source, listed);
            AboutSourcePreviewTitle.Text = listed.Models.Count == 0
                ? $"{source.Given} lists no models."
                : $"{source.Given} lists {listed.Models.Count} model{(listed.Models.Count == 1 ? "" : "s")}. Add it?";
            AboutSourcePreviewModels.ItemsSource = listed.Models
                .Select(m => $"{m.Name} · {m.Task} · {Size(m.Variants.Values.Max(v => v.Bytes))} · {m.License}")
                .ToList();
            AboutSourceKeep.IsEnabled = listed.Models.Count > 0;
            AboutSourcePreview.Visibility = Visibility.Visible;
        }
        catch (Exception error) when (error is ArgumentException or HttpRequestException or InvalidDataException or TaskCanceledException or UriFormatException)
        {
            SaySource(error.Message);
        }
        finally
        {
            AboutAddSource.IsEnabled = true;
        }
    }

    // The manifests of the sources already kept, for the name check a new one is held to.
    private async Task<IReadOnlyList<(ModelSource, ModelManifest)>> ReadSourcesAsync()
    {
        List<(ModelSource, ModelManifest)> read = [];
        foreach (ModelSource source in Sources)
        {
            Uri url = new(source.ManifestUrl);
            read.Add((source, ModelManifest.Parse(await http.GetStringAsync(url).ConfigureAwait(true), loopback: url.IsLoopback)));
        }

        return read;
    }

    private async void OnKeepSource(object sender, RoutedEventArgs e)
    {
        if (proposed is not { } kept)
        {
            return;
        }

        proposed = null;
        AboutSourcePreview.Visibility = Visibility.Collapsed;
        AboutSourceInput.Text = "";
        await SaveSourcesAsync([.. Sources, kept.Source]).ConfigureAwait(true);
    }

    private void OnCancelSource(object sender, RoutedEventArgs e)
    {
        proposed = null;
        AboutSourcePreview.Visibility = Visibility.Collapsed;
    }

    private async void OnRemoveSource(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is int index && index >= 0 && index < Sources.Count)
        {
            await SaveSourcesAsync([.. Sources.Where((_, i) => i != index)]).ConfigureAwait(true);
        }
    }

    private async Task SaveSourcesAsync(IReadOnlyList<ModelSource> next)
    {
        sources = next;
        try
        {
            ModelSources.Save(SettingsFile.DefaultPath, next);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            sourcesError = $"The sources are used until Modelado closes but could not be saved: {error.Message}";
        }

        // Read again with the sources as they now are, so the cards offer what they list.
        manifest = null;
        await ManifestAsync().ConfigureAwait(true);
        FillAbout();
        await RefreshModelStatusAsync().ConfigureAwait(true);
    }

    private void SaySource(string? text)
    {
        AboutSourceNote.Text = text ?? "";
        AboutSourceNote.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>A depth model the Relief card offers.</summary>
    private sealed record DepthChoice(string Name, string Label)
    {
        // What the list item announces, which is what a case and a screen reader read.
        public override string ToString() => Label;
    }

    private string DepthModel => (ReliefDepthModel.SelectedItem as DepthChoice)?.Name ?? ReliefReconstruction.Depth;

    // The built-in depth model first, then each one an added source lists; shown when there is a choice.
    private void FillDepthChoices(ModelManifest? listed)
    {
        IReadOnlyList<string> names = listed is null ? [] : ModelSources.ForTask(listed, ReliefReconstruction.DepthTask);
        List<DepthChoice> choices = [new(ReliefReconstruction.Depth, "Depth Anything V2 Small (built in)")];
        choices.AddRange(names.Where(sourceOfModel.ContainsKey).Select(n => new DepthChoice(n, $"{n} ({sourceOfModel[n]})")));
        string keep = DepthModel;
        fillingDepth = true;
        ReliefDepthModel.ItemsSource = choices;
        ReliefDepthModel.SelectedItem = choices.FirstOrDefault(c => c.Name == keep) ?? choices[0];
        fillingDepth = false;
        ReliefDepthModelPanel.Visibility = choices.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool fillingDepth;

    private async void OnReliefDepthModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!fillingDepth)
        {
            await RefreshModelStatusAsync().ConfigureAwait(true);
        }
    }
}
