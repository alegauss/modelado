using Microsoft.ML.OnnxRuntime;

namespace Modelado.Inference;

/// <summary>Where the user asked inference to run.</summary>
public enum ProviderChoice
{
    /// <summary>DirectML on the GPU when it works, the CPU otherwise.</summary>
    Auto,

    /// <summary>DirectML only: a GPU that fails is an error, not a fallback.</summary>
    DirectML,

    /// <summary>The CPU only.</summary>
    Cpu,
}

/// <summary>Where sessions are built.</summary>
public enum ExecutionProvider
{
    /// <summary>DirectML (DirectX 12), any vendor's GPU on Windows.</summary>
    DirectML,

    /// <summary>ONNX Runtime's own CPU kernels.</summary>
    Cpu,
}

/// <summary>Why a choice did not land on the GPU (MOD147), worded by whoever shows it.</summary>
public enum ProviderReason
{
    /// <summary>It runs where it was asked to.</summary>
    None,

    /// <summary>This build of ONNX Runtime has no DirectML.</summary>
    NoDirectML,

    /// <summary>Automatic found only integrated GPUs, which run these models slower than the CPU.</summary>
    IntegratedOnly,
}

/// <summary>Where a choice runs (MOD147): the provider, DirectML's device id and the adapter it is.</summary>
/// <param name="Provider">Where sessions are built.</param>
/// <param name="DeviceId">DirectML's device id, DXGI's index; 0 on the CPU.</param>
/// <param name="Adapter">The GPU it runs on, or null on the CPU or where none can be named.</param>
/// <param name="Reason">Why it is not the GPU, when it is not.</param>
public sealed record ProviderPick(ExecutionProvider Provider, int DeviceId, GpuAdapter? Adapter, ProviderReason Reason)
{
    /// <summary>The reason in English, for the CLI; the window words it from its own strings.</summary>
    public string? Sentence => Reason switch
    {
        ProviderReason.NoDirectML => "DirectML is not available in this build, so the CPU is used.",
        ProviderReason.IntegratedOnly => "the integrated GPU is slower here than the CPU, so the CPU is used; --provider dml forces it.",
        _ => null,
    };
}

/// <summary>Builds ONNX Runtime sessions on the provider chosen at start.</summary>
public static class ExecutionProviders
{
    private const string DmlName = "DmlExecutionProvider";

    /// <summary>Whether this ONNX Runtime build carries DirectML (the Windows package does).</summary>
    public static bool DirectMLAvailable => OrtEnv.Instance().GetAvailableProviders().Contains(DmlName, StringComparer.Ordinal);

    /// <summary>The provider a choice resolves to on this machine.</summary>
    /// <param name="choice">What was asked.</param>
    /// <returns>The provider, and why when it is not the one asked for.</returns>
    /// <exception cref="InvalidOperationException">DirectML was demanded and this build has none.</exception>
    public static (ExecutionProvider Provider, string? Reason) Resolve(ProviderChoice choice) => choice switch
    {
        ProviderChoice.Cpu => (ExecutionProvider.Cpu, null),
        ProviderChoice.DirectML when !DirectMLAvailable => throw new InvalidOperationException("This ONNX Runtime build has no DirectML provider."),
        ProviderChoice.DirectML => (ExecutionProvider.DirectML, null),
        _ when DirectMLAvailable => (ExecutionProvider.DirectML, null),
        _ => (ExecutionProvider.Cpu, "DirectML is not available in this build, so the CPU is used."),
    };

    /// <summary>
    /// MOD147: where a choice runs among these adapters. DirectML goes to the first discrete GPU,
    /// so a hybrid laptop whose integrated GPU DXGI lists first still runs on its discrete one; an
    /// adapter DXCore cannot place counts as discrete, as before it could be asked. Automatic on a
    /// PC whose only GPUs are integrated runs on the CPU instead, since MOD121 measured an Intel UHD
    /// 770 several times slower than its CPU; DirectML chosen in About still forces the GPU.
    /// </summary>
    /// <param name="choice">What was asked.</param>
    /// <param name="adapters">The hardware adapters, in DXGI's order (<see cref="GpuAdapter.All"/>).</param>
    /// <returns>The provider, the device and why it is not the GPU.</returns>
    /// <exception cref="InvalidOperationException">DirectML was demanded and this build has none.</exception>
    public static ProviderPick Pick(ProviderChoice choice, IReadOnlyList<GpuAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        (ExecutionProvider provider, string? reason) = Resolve(choice);
        if (provider == ExecutionProvider.Cpu)
        {
            return new ProviderPick(ExecutionProvider.Cpu, 0, null, reason is null ? ProviderReason.None : ProviderReason.NoDirectML);
        }

        GpuAdapter? discrete = adapters.FirstOrDefault(a => a.Integrated == false) ?? adapters.FirstOrDefault(a => a.Integrated is null);
        if (choice == ProviderChoice.Auto && adapters.Count > 0 && discrete is null)
        {
            return new ProviderPick(ExecutionProvider.Cpu, 0, null, ProviderReason.IntegratedOnly);
        }

        GpuAdapter? chosen = discrete ?? (adapters.Count > 0 ? adapters[0] : null);
        return new ProviderPick(ExecutionProvider.DirectML, chosen?.Index ?? 0, chosen, ProviderReason.None);
    }

    /// <summary>The provider chip's words (MOD52): "CPU", "DirectML", or the GPU's name and video memory beside it.</summary>
    /// <param name="provider">Where sessions are built.</param>
    /// <param name="adapter">The GPU DirectML runs on, or null when it cannot be named.</param>
    /// <returns>"DirectML · NVIDIA GeForce RTX 4060, 8 GB", "DirectML" or "CPU".</returns>
    public static string Label(ExecutionProvider provider, GpuAdapter? adapter) =>
        provider == ExecutionProvider.Cpu ? "CPU" : adapter is null ? "DirectML" : $"DirectML · {adapter.Label}";

    /// <summary>The variant a quality setting runs on a provider.</summary>
    /// <param name="provider">Where it runs.</param>
    /// <param name="high">True for High quality.</param>
    /// <param name="light">
    /// MOD116: Light quality, the INT8 variant on the CPU (a quarter of fp32's download and about
    /// 1 GB less memory, at a visible cost in shape that docs/local-3d-models.md measured). A model
    /// with no int8 variant falls back to fp32, in the manifest and on disk alike.
    /// </param>
    /// <returns>fp32, fp16 on the GPU in Standard quality, or int8 on the CPU in Light quality.</returns>
    public static string VariantFor(ExecutionProvider provider, bool high, bool light = false) =>
        high ? "fp32" : provider == ExecutionProvider.Cpu ? (light ? "int8" : "fp32") : "fp16";

    /// <summary>Loads a model file into a session.</summary>
    /// <param name="path">The .onnx file.</param>
    /// <param name="provider">Where it runs.</param>
    /// <param name="deviceId">The DirectML adapter index; 0 is the system's default GPU.</param>
    /// <returns>The session.</returns>
    public static InferenceSession Create(string path, ExecutionProvider provider, int deviceId = 0)
    {
        SessionOptions options = new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR,
        };
        try
        {
            if (provider == ExecutionProvider.DirectML)
            {
                // What DirectML requires: no memory pattern, operators run one at a time.
                options.EnableMemoryPattern = false;
                options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
                options.AppendExecutionProvider_DML(deviceId);
            }

            return new InferenceSession(path, options);
        }
        finally
        {
            options.Dispose();
        }
    }
}
