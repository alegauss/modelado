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

    /// <summary>The variant a quality setting runs on a provider.</summary>
    /// <param name="provider">Where it runs.</param>
    /// <param name="high">True for High quality.</param>
    /// <returns>fp32, or fp16 on the GPU in Standard quality: half the download and the video memory.</returns>
    public static string VariantFor(ExecutionProvider provider, bool high) =>
        high || provider == ExecutionProvider.Cpu ? "fp32" : "fp16";

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
