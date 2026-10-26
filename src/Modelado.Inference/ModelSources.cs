using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Modelado.Inference;

/// <summary>
/// A model repository added beside the built-in one (MOD27): what it was given as, and the manifest
/// it was pinned to when added, so a later push to the repository cannot change what it lists.
/// </summary>
/// <param name="Given">What it was added as: a Hugging Face repository id (owner/name) or a manifest URL.</param>
/// <param name="ManifestUrl">The manifest it reads: for a repository id, its manifest.json at the commit it was at.</param>
public sealed record ModelSource(string Given, string ManifestUrl);

/// <summary>
/// The model repositories the app reads beyond the built-in one (MOD27): added by a person, kept per
/// user in settings.json, and read after the built-in manifest. A source whose model shares a name
/// with one already listed is refused, since a model's name is its folder in the models folder and
/// what an operation asks for.
/// </summary>
public static partial class ModelSources
{
    private const string Key = "modelSources";

    /// <summary>Resolves what a person typed to the source to keep.</summary>
    /// <param name="http">The client.</param>
    /// <param name="given">owner/name on Hugging Face, or a manifest URL (https, or http on loopback).</param>
    /// <param name="cancellation">Cancels the lookup.</param>
    /// <returns>The source, its manifest pinned where it was given as a repository.</returns>
    /// <exception cref="ArgumentException">It is neither a repository id nor a URL this build reads.</exception>
    /// <exception cref="HttpRequestException">Hugging Face could not say which commit the repository is at.</exception>
    public static async Task<ModelSource> ResolveAsync(HttpClient http, string given, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(given);
        string trimmed = given.Trim();
        if (RepositoryId().IsMatch(trimmed))
        {
            // The commit main is at now, so the source reads that manifest from then on.
            string answer = await http.GetStringAsync(new Uri($"https://huggingface.co/api/models/{trimmed}/revision/main"), cancellation).ConfigureAwait(false);
            string commit = (JsonNode.Parse(answer)?["sha"]?.GetValue<string>()) is { Length: 40 } sha && sha.All(char.IsAsciiHexDigit)
                ? sha
                : throw new HttpRequestException($"Hugging Face did not say which commit {trimmed} is at.");
            return new ModelSource(trimmed, $"https://huggingface.co/{trimmed}/resolve/{commit}/manifest.json");
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? url) && (url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback))
            ? new ModelSource(trimmed, url.AbsoluteUri)
            : throw new ArgumentException($"{trimmed} is neither a Hugging Face repository (owner/name) nor an https manifest URL.", nameof(given));
    }

    /// <summary>The built-in manifest with each added source's models after it.</summary>
    /// <param name="builtIn">The built-in manifest; its models stay first and keep their names.</param>
    /// <param name="added">Each source with the manifest it read.</param>
    /// <returns>One manifest listing them all, under the built-in's version.</returns>
    /// <exception cref="InvalidDataException">A source lists a model whose name is already taken, naming both.</exception>
    public static ModelManifest Merge(ModelManifest builtIn, IReadOnlyList<(ModelSource Source, ModelManifest Manifest)> added)
    {
        ArgumentNullException.ThrowIfNull(builtIn);
        ArgumentNullException.ThrowIfNull(added);
        Dictionary<string, string> owner = builtIn.Models.ToDictionary(m => m.Name, _ => "the built-in models", StringComparer.Ordinal);
        List<ManifestModel> models = [.. builtIn.Models];
        foreach ((ModelSource source, ModelManifest manifest) in added)
        {
            foreach (ManifestModel model in manifest.Models)
            {
                if (!owner.TryAdd(model.Name, source.Given))
                {
                    throw new InvalidDataException($"{source.Given} lists {model.Name}, a name {owner[model.Name]} already use; a model there needs a name of its own.");
                }

                models.Add(model);
            }
        }

        return builtIn with { Models = models };
    }

    /// <summary>Reads each source's manifest and merges them after the built-in one.</summary>
    /// <param name="http">The client.</param>
    /// <param name="builtIn">The built-in manifest.</param>
    /// <param name="sources">The added sources, in order.</param>
    /// <param name="cancellation">Cancels the fetches.</param>
    /// <returns>The merged manifest.</returns>
    /// <exception cref="InvalidDataException">A manifest does not read, or a name is taken twice.</exception>
    public static async Task<ModelManifest> WithSourcesAsync(HttpClient http, ModelManifest builtIn, IReadOnlyList<ModelSource> sources, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(sources);
        List<(ModelSource, ModelManifest)> read = [];
        foreach (ModelSource source in sources)
        {
            Uri url = new(source.ManifestUrl);
            read.Add((source, ModelManifest.Parse(await http.GetStringAsync(url, cancellation).ConfigureAwait(false), loopback: url.IsLoopback)));
        }

        return Merge(builtIn, read);
    }

    /// <summary>The models a manifest lists for a task, in its order: the built-in first.</summary>
    /// <param name="manifest">The merged manifest.</param>
    /// <param name="task">The task, as meta.json names it (depth, upscale, ...).</param>
    /// <returns>The models' names.</returns>
    public static IReadOnlyList<string> ForTask(ModelManifest manifest, string task)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return [.. manifest.Models.Where(m => m.Task == task).Select(m => m.Name)];
    }

    /// <summary>The sources a person added, in the order they were added.</summary>
    /// <param name="path">The settings file.</param>
    /// <returns>The sources; none when nothing was added or the file does not read.</returns>
    public static IReadOnlyList<ModelSource> Load(string path)
    {
        JsonArray? kept;
        try
        {
            kept = File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject settings ? settings[Key] as JsonArray : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return [.. (kept ?? []).OfType<JsonObject>()
            .Select(one => (Given: one["given"]?.GetValue<string>(), Manifest: one["manifest"]?.GetValue<string>()))
            .Where(one => one.Given is not null && one.Manifest is not null)
            .Select(one => new ModelSource(one.Given!, one.Manifest!))];
    }

    /// <summary>Keeps the sources, replacing the ones kept before.</summary>
    /// <param name="path">The settings file.</param>
    /// <param name="sources">The sources, in order.</param>
    public static void Save(string path, IReadOnlyList<ModelSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        SettingsFile.Write(path, Key, new JsonArray([.. sources.Select(s => new JsonObject { ["given"] = s.Given, ["manifest"] = s.ManifestUrl })]));
    }

    // owner/name as Hugging Face spells a repository: letters, digits, '-', '_' and '.', no '..'.
    [GeneratedRegex(@"^(?!.*\.\.)[A-Za-z0-9][A-Za-z0-9._-]*/[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex RepositoryId();
}
