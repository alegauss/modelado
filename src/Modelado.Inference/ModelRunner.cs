using Microsoft.ML.OnnxRuntime;

namespace Modelado.Inference;

/// <summary>
/// The sessions the pipelines run, loaded the first time a model is used and kept until
/// <see cref="Release"/>. A run that fails on the GPU (out of video memory, a driver fault) is
/// retried once on the CPU, and that model stays on the CPU from then on, which
/// <see cref="Notice"/> says.
/// </summary>
public sealed class ModelRunner : IDisposable
{
    private readonly Dictionary<string, (InferenceSession Session, ExecutionProvider Provider)> sessions = new(StringComparer.Ordinal);
    private readonly Lock gate = new();
    private bool disposed;

    /// <summary>Creates a runner over a library.</summary>
    /// <param name="library">Where the models are.</param>
    /// <param name="provider">Where sessions run.</param>
    /// <param name="highQuality">fp32 everywhere instead of fp16 on the GPU.</param>
    public ModelRunner(ModelLibrary library, ExecutionProvider provider, bool highQuality = false)
    {
        ArgumentNullException.ThrowIfNull(library);
        Library = library;
        Provider = provider;
        HighQuality = highQuality;
    }

    /// <summary>Where the models are.</summary>
    public ModelLibrary Library { get; }

    /// <summary>Where sessions run unless a model fell back to the CPU.</summary>
    public ExecutionProvider Provider { get; }

    /// <summary>Whether fp32 is used on the GPU too.</summary>
    public bool HighQuality { get; }

    /// <summary>The last fallback to the CPU, for the status bar; null when there was none.</summary>
    public string? Notice { get; private set; }

    /// <summary>The variant of a model this runner loads.</summary>
    /// <param name="model">The model's folder name.</param>
    /// <returns>fp32 or fp16.</returns>
    public string VariantOf(string model) => Library.VariantFor(model, ExecutionProviders.VariantFor(Provider, HighQuality));

    /// <summary>What a set of models needs that is not on disk, as download requests.</summary>
    /// <param name="models">The models' folder names.</param>
    /// <returns>The requests; empty when everything is installed.</returns>
    public IReadOnlyList<ModelRequest> Missing(IEnumerable<string> models)
    {
        string wanted = ExecutionProviders.VariantFor(Provider, HighQuality);
        return [.. models.Where(m => !Library.Has(m, Library.VariantFor(m, wanted))).Select(m => new ModelRequest(m, wanted))];
    }

    /// <summary>Runs a model.</summary>
    /// <param name="model">The model's folder name.</param>
    /// <param name="inputs">The inputs, by name.</param>
    /// <param name="outputs">The outputs to return, in order.</param>
    /// <returns>The outputs, owned by the caller.</returns>
    public IDisposableReadOnlyCollection<OrtValue> Run(string model, IReadOnlyDictionary<string, OrtValue> inputs, IReadOnlyCollection<string> outputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        (InferenceSession session, ExecutionProvider provider) = Session(model);
        try
        {
            using RunOptions options = new();
            return session.Run(options, inputs, outputs);
        }
        catch (OnnxRuntimeException failure) when (provider != ExecutionProvider.Cpu)
        {
            Notice = $"{model} failed on the GPU ({failure.Message.Split('\n')[0]}) and now runs on the CPU.";
            lock (gate)
            {
                sessions.Remove(model);
                session.Dispose();
                sessions[model] = (ExecutionProviders.Create(Library.FileOf(model, VariantOf(model)), ExecutionProvider.Cpu), ExecutionProvider.Cpu);
            }

            return Run(model, inputs, outputs);
        }
    }

    /// <summary>Loads a model's session now, so the first run does not pay for it.</summary>
    /// <param name="model">The model's folder name.</param>
    public void Load(string model) => Session(model);

    /// <summary>Releases a model's session, or every session.</summary>
    /// <param name="model">The model, or null for all.</param>
    public void Release(string? model = null)
    {
        lock (gate)
        {
            foreach (string name in sessions.Keys.Where(k => model is null || k == model).ToList())
            {
                sessions[name].Session.Dispose();
                sessions.Remove(name);
            }
        }
    }

    /// <summary>Releases every session.</summary>
    public void Dispose()
    {
        if (!disposed)
        {
            Release();
            disposed = true;
        }
    }

    private (InferenceSession Session, ExecutionProvider Provider) Session(string model)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (sessions.TryGetValue(model, out var loaded))
            {
                return loaded;
            }

            string variant = VariantOf(model);
            if (!Library.Has(model, variant))
            {
                throw new FileNotFoundException($"The model {model} ({variant}) is not installed; download it first.", Library.FileOf(model, variant));
            }

            string path = Library.FileOf(model, variant);
            try
            {
                loaded = (ExecutionProviders.Create(path, Provider), Provider);
            }
            catch (OnnxRuntimeException failure) when (Provider != ExecutionProvider.Cpu)
            {
                Notice = $"{model} could not be loaded on the GPU ({failure.Message.Split('\n')[0]}) and runs on the CPU.";
                loaded = (ExecutionProviders.Create(path, ExecutionProvider.Cpu), ExecutionProvider.Cpu);
            }

            sessions[model] = loaded;
            return loaded;
        }
    }
}
