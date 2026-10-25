using Modelado.Inference;

namespace Modelado.App;

/// <summary>What the app was started with.</summary>
/// <param name="Open">A photo to open, or null.</param>
/// <param name="Models">A models folder replacing the default ones, or null.</param>
/// <param name="Provider">Where inference runs, or null for the choice About saved (MOD18).</param>
/// <param name="HighQuality">fp32 on the GPU too.</param>
/// <param name="Manifest">A models manifest URL replacing the embedded one, or null.</param>
/// <param name="Releases">
/// MOD30: a release description URL replacing GitHub's API, "off" for no automatic check this run, or
/// null for the API.
/// </param>
/// <param name="CloudApis">MOD37: cloud services' API roots by provider id, standing in for their own.</param>
public sealed record AppOptions(string? Open, string? Models, ProviderChoice? Provider, bool HighQuality, string? Manifest, string? Releases = null, IReadOnlyDictionary<string, Uri>? CloudApis = null);

/// <summary>
/// Parses Modelado.exe's arguments:
/// <c>[--open] photo.jpg  --models folder  --provider auto|dml|cpu  --quality standard|high  --manifest url  --releases url|off  --cloud-api id=url</c>.
/// A bare path opens that photo, so "Open with" and drag-onto-the-exe work.
/// </summary>
public static class CommandLine
{
    /// <summary>Parses the arguments.</summary>
    /// <param name="args">As Windows passed them.</param>
    /// <returns>The options, or why they were refused.</returns>
    public static (AppOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? open = null, models = null, manifest = null, releases = null;
        Dictionary<string, Uri> cloudApis = new(StringComparer.Ordinal);
        ProviderChoice? provider = null;
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
                return (null, $"{arg} needs a value.");
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
                case "--releases":
                    releases = value;
                    break;
                case "--cloud-api":
                    if (value.Split('=', 2) is not [{ Length: > 0 } id, var url] || !Uri.TryCreate(url.EndsWith('/') ? url : url + "/", UriKind.Absolute, out Uri? api))
                    {
                        return (null, $"--cloud-api {value}: use id=url, e.g. meshy=http://127.0.0.1:8080/.");
                    }

                    cloudApis[id] = api;
                    break;
                case "--provider":
                    provider = ProviderSetting.Parse(value);
                    if (provider is null)
                    {
                        return (null, $"--provider {value}: use auto, dml or cpu.");
                    }

                    break;
                case "--quality":
                    if (value is not ("standard" or "high"))
                    {
                        return (null, $"--quality {value}: use standard or high.");
                    }

                    high = value == "high";
                    break;
                default:
                    return (null, $"Unknown option: {arg}.");
            }
        }

        return (new AppOptions(open, models, provider, high, manifest, releases, cloudApis), null);
    }
}
