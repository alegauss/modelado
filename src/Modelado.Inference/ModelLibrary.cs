using System.Collections.Concurrent;

namespace Modelado.Inference;

/// <summary>A model on disk, as the About panel lists it.</summary>
/// <param name="Name">Its folder name.</param>
/// <param name="Meta">Its meta.json.</param>
/// <param name="Variants">The variants whose ONNX files are present.</param>
/// <param name="Root">The models folder it was found in.</param>
public sealed record InstalledModel(string Name, ModelMeta Meta, IReadOnlyList<string> Variants, string Root);

/// <summary>
/// The models folders: &lt;root&gt;/&lt;model&gt;/meta.json beside the ONNX variants. Several roots
/// are searched in order (the folder beside the app, then the one downloads go to), and a
/// model's variant is read from the first root that has it.
/// </summary>
public sealed class ModelLibrary
{
    private readonly ConcurrentDictionary<string, ModelMeta> metas = new(StringComparer.Ordinal);

    /// <summary>Opens the models folders.</summary>
    /// <param name="roots">The folders, searched in order; the last is where downloads go.</param>
    public ModelLibrary(IReadOnlyList<string> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentOutOfRangeException.ThrowIfZero(roots.Count);
        Roots = roots;
    }

    /// <summary>
    /// The folder downloads go to unless one is chosen: %LOCALAPPDATA%\Modelado\models on
    /// Windows, ~/.local/share/Modelado/models elsewhere.
    /// </summary>
    public static string DefaultDownloadFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Modelado", "models");

    /// <summary>The roots an app uses: models beside the executable, then the download folder.</summary>
    /// <returns>The library.</returns>
    public static ModelLibrary Default() => new([Path.Combine(AppContext.BaseDirectory, "models"), DefaultDownloadFolder]);

    /// <summary>The models folders, in the order they are searched.</summary>
    public IReadOnlyList<string> Roots { get; }

    /// <summary>Where downloads go: the last root.</summary>
    public string DownloadRoot => Roots[^1];

    /// <summary>Whether a model's variant is on disk.</summary>
    /// <param name="model">The model's folder name.</param>
    /// <param name="variant">fp32 or fp16.</param>
    /// <returns>True when one root holds both its meta.json and its ONNX file.</returns>
    public bool Has(string model, string variant) => RootOf(model, variant) is not null;

    /// <summary>The ONNX file a model's variant is read from.</summary>
    /// <param name="model">The model's folder name.</param>
    /// <param name="variant">fp32 or fp16.</param>
    /// <returns>The path, under the download root when no root has it.</returns>
    public string FileOf(string model, string variant) =>
        Path.Combine(RootOf(model, variant) ?? DownloadRoot, model, variant + ".onnx");

    /// <summary>
    /// The variant to run: <paramref name="preferred"/> when on disk, else fp32 when on disk, else
    /// <paramref name="preferred"/> (which then has to be downloaded).
    /// </summary>
    /// <param name="model">The model's folder name.</param>
    /// <param name="preferred">The variant the quality setting asks for.</param>
    /// <returns>The variant.</returns>
    public string VariantFor(string model, string preferred) =>
        Has(model, preferred) ? preferred : Has(model, "fp32") ? "fp32" : preferred;

    /// <summary>A model's meta.json, read once, from the first root that has one.</summary>
    /// <param name="model">The model's folder name.</param>
    /// <returns>Its description.</returns>
    /// <exception cref="FileNotFoundException">No root has it.</exception>
    public ModelMeta Meta(string model) => metas.GetOrAdd(model, name =>
    {
        string? root = Roots.FirstOrDefault(r => File.Exists(Path.Combine(r, name, "meta.json")));
        return root is null
            ? throw new FileNotFoundException($"The model {name} is not installed.", Path.Combine(DownloadRoot, name, "meta.json"))
            : ModelMeta.Load(Path.Combine(root, name, "meta.json"));
    });

    /// <summary>Forgets every meta.json read, after a download or a removal.</summary>
    public void Refresh() => metas.Clear();

    /// <summary>Every model in any root with a meta.json this build can read.</summary>
    /// <returns>The models, by folder name.</returns>
    public IReadOnlyList<InstalledModel> Installed()
    {
        List<InstalledModel> found = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string root in Roots.Where(Directory.Exists))
        {
            foreach (string folder in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
            {
                string name = Path.GetFileName(folder);
                if (!File.Exists(Path.Combine(folder, "meta.json")) || !seen.Add(name))
                {
                    continue;
                }

                try
                {
                    ModelMeta meta = Meta(name);
                    string[] onDisk = [.. meta.Variants.Keys.Where(v => Has(name, v)).Order(StringComparer.Ordinal)];
                    found.Add(new InstalledModel(name, meta, onDisk, root));
                }
                catch (InvalidDataException)
                {
                    // Listed nowhere: the operation that needs it says why when asked.
                }
            }
        }

        return found;
    }

    /// <summary>Deletes what was downloaded (never the files beside the app).</summary>
    /// <returns>How many model folders were removed.</returns>
    public int RemoveDownloads()
    {
        int removed = 0;
        if (Directory.Exists(DownloadRoot))
        {
            foreach (string folder in Directory.EnumerateDirectories(DownloadRoot))
            {
                Directory.Delete(folder, recursive: true);
                removed++;
            }
        }

        Refresh();
        return removed;
    }

    /// <summary>Deletes one downloaded model (MOD23); the files beside the app are never touched.</summary>
    /// <param name="model">The model's folder name.</param>
    /// <returns>Whether the download folder held it.</returns>
    /// <exception cref="ArgumentException">The name is not a bare folder name.</exception>
    public bool RemoveDownload(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        // Both separators on every system: on Linux a backslash is a file name's character, and a
        // model's name never holds one.
        if (Path.GetFileName(model) != model || model is "." or ".." || model.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new ArgumentException($"{model} is not a model's folder name.", nameof(model));
        }

        string folder = Path.Combine(DownloadRoot, model);
        bool found = Directory.Exists(folder);
        if (found)
        {
            Directory.Delete(folder, recursive: true);
        }

        Refresh();
        return found;
    }

    private string? RootOf(string model, string variant) => Roots.FirstOrDefault(root =>
        File.Exists(Path.Combine(root, model, "meta.json")) && File.Exists(Path.Combine(root, model, variant + ".onnx")));
}
