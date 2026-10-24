using Modelado.Inference;

namespace Modelado.App;

/// <summary>What the app was started with.</summary>
/// <param name="Open">A photo to open, or null.</param>
/// <param name="Models">A models folder replacing the default ones, or null.</param>
/// <param name="Provider">Where inference runs.</param>
/// <param name="HighQuality">fp32 on the GPU too.</param>
/// <param name="Manifest">A models manifest URL replacing the embedded one, or null.</param>
public sealed record AppOptions(string? Open, string? Models, ProviderChoice Provider, bool HighQuality, string? Manifest);

/// <summary>
/// Parses Modelado.exe's arguments:
/// <c>[--open] foto.jpg  --models pasta  --provider auto|dml|cpu  --quality standard|high  --manifest url</c>.
/// A bare path opens that photo, so "Abrir com" and drag-onto-the-exe work.
/// </summary>
public static class CommandLine
{
    /// <summary>Parses the arguments.</summary>
    /// <param name="args">As Windows passed them.</param>
    /// <returns>The options, or why they were refused.</returns>
    public static (AppOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? open = null, models = null, manifest = null;
        ProviderChoice provider = ProviderChoice.Auto;
        bool high = false;
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                open = arg;
                continue;
            }

            if (i + 1 >= args.Count)
            {
                return (null, $"{arg} precisa de um valor.");
            }

            string value = args[++i];
            switch (arg)
            {
                case "--open":
                    open = value;
                    break;
                case "--models":
                    models = value;
                    break;
                case "--manifest":
                    manifest = value;
                    break;
                case "--provider":
                    switch (value)
                    {
                        case "auto":
                            provider = ProviderChoice.Auto;
                            break;
                        case "dml":
                            provider = ProviderChoice.DirectML;
                            break;
                        case "cpu":
                            provider = ProviderChoice.Cpu;
                            break;
                        default:
                            return (null, $"--provider {value}: use auto, dml ou cpu.");
                    }

                    break;
                case "--quality":
                    if (value is not ("standard" or "high"))
                    {
                        return (null, $"--quality {value}: use standard ou high.");
                    }

                    high = value == "high";
                    break;
                default:
                    return (null, $"Opção desconhecida: {arg}.");
            }
        }

        return (new AppOptions(open, models, provider, high, manifest), null);
    }
}
