using Microsoft.ML.OnnxRuntime;

namespace Modelado.Inference;

/// <summary>
/// MOD134: a model that failed on the GPU and runs on the CPU instead, as data, so the window words
/// it in its own language; <see cref="ModelRunner.Notice"/> is the CLI's English sentence for it.
/// </summary>
/// <param name="Model">The model's folder name.</param>
/// <param name="AtLoad">True when it could not be loaded on the GPU, false when a run failed there.</param>
/// <param name="Cause">ONNX Runtime's own first line, shown as it wrote it.</param>
public sealed record GpuFallback(string Model, bool AtLoad, string Cause);

/// <summary>
/// The sessions the pipelines run, loaded the first time a model is used and kept until
/// <see cref="Release"/>. A run that fails on the GPU (out of video memory, a driver fault) is
/// retried once on the CPU, and that model stays on the CPU from then on, which
/// <see cref="Notice"/> says. MOD120: only when the GPU came from Automatic; where the user chose
/// DirectML (<see cref="ProviderChoice.DirectML"/>), a GPU failure is an error that says so, since
/// someone forcing the GPU to compare times must not be measuring the CPU without knowing.
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
    /// <param name="fallBack">MOD120: whether a GPU failure is retried on the CPU (Automatic) or is an error (DirectML chosen).</param>
    /// <param name="deviceId">MOD121: the DirectML adapter, DXGI's index; 0 is the system's default GPU.</param>
    /// <param name="light">MOD116: Light quality, the INT8 variant on the CPU where a model has one.</param>
    public ModelRunner(ModelLibrary library, ExecutionProvider provider, bool highQuality = false, bool fallBack = true, int deviceId = 0, bool light = false)
    {
        Light = light;
        ArgumentOutOfRangeException.ThrowIfNegative(deviceId);
        DeviceId = deviceId;
        ArgumentNullException.ThrowIfNull(library);
        Library = library;
        Provider = provider;
        HighQuality = highQuality;
        FallBack = fallBack;
    }

    /// <summary>MOD116: whether the INT8 variant is wanted on the CPU (Light quality).</summary>
    public bool Light { get; }

    /// <summary>The variant this runner asks for: the manifest and the disk fall back to fp32 where a model has none.</summary>
    public string Wanted => ExecutionProviders.VariantFor(Provider, HighQuality, Light);

    /// <summary>MOD121: the DirectML adapter sessions run on, DXGI's index.</summary>
    public int DeviceId { get; }

    /// <summary>MOD120: whether a model that fails on the GPU runs on the CPU instead; false where DirectML was chosen.</summary>
    public bool FallBack { get; }

    /// <summary>MOD120: whether a choice lets a GPU failure fall back to the CPU: only Automatic does.</summary>
    /// <param name="choice">What the user chose.</param>
    /// <returns>True for Automatic.</returns>
    public static bool FallsBack(ProviderChoice choice) => choice == ProviderChoice.Auto;

    /// <summary>Where the models are.</summary>
    public ModelLibrary Library { get; }

    /// <summary>Where sessions run unless a model fell back to the CPU.</summary>
    public ExecutionProvider Provider { get; }

    /// <summary>Whether fp32 is used on the GPU too.</summary>
    public bool HighQuality { get; }

    /// <summary>The last fallback to the CPU, in English for the CLI; null when there was none.</summary>
    public string? Notice { get; private set; }

    /// <summary>MOD134: the last fallback to the CPU as data, for the window to word; null when there was none.</summary>
    public GpuFallback? Fallback { get; private set; }

    /// <summary>The variant of a model this runner loads.</summary>
    /// <param name="model">The model's folder name.</param>
    /// <returns>fp32 or fp16.</returns>
    public string VariantOf(string model) => Library.VariantFor(model, Wanted);

    /// <summary>What a set of models needs that is not on disk, as download requests.</summary>
    /// <param name="models">The models' folder names.</param>
    /// <returns>The requests; empty when everything is installed.</returns>
    public IReadOnlyList<ModelRequest> Missing(IEnumerable<string> models)
    {
        string wanted = Wanted;
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
        catch (OnnxRuntimeException failure) when (provider != ExecutionProvider.Cpu && !FallBack)
        {
            throw Chosen(model, "failed", failure);
        }
        catch (OnnxRuntimeException failure) when (provider != ExecutionProvider.Cpu)
        {
            Fallback = new GpuFallback(model, AtLoad: false, failure.Message.Split('\n')[0]);
            Notice = $"{model} failed on the GPU ({Fallback.Cause}) and now runs on the CPU.";
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

    // MOD120: the GPU was chosen, so its failure is the answer, not a run on the CPU in its place.
    private static InvalidOperationException Chosen(string model, string what, OnnxRuntimeException failure) =>
        new($"{model} {what} on the GPU ({failure.Message.Split('\n')[0]}). DirectML was chosen, so it is not run on the CPU instead; choose Automatic or the CPU in About to let it.", failure);

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
                loaded = (ExecutionProviders.Create(path, Provider, DeviceId), Provider);
            }
            catch (OnnxRuntimeException failure) when (Provider != ExecutionProvider.Cpu && !FallBack)
            {
                throw Chosen(model, "could not be loaded", failure);
            }
            catch (OnnxRuntimeException failure) when (Provider != ExecutionProvider.Cpu)
            {
                Fallback = new GpuFallback(model, AtLoad: true, failure.Message.Split('\n')[0]);
                Notice = $"{model} could not be loaded on the GPU ({Fallback.Cause}) and runs on the CPU.";
                loaded = (ExecutionProviders.Create(path, ExecutionProvider.Cpu), ExecutionProvider.Cpu);
            }

            sessions[model] = loaded;
            return loaded;
        }
    }
}
