using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Modelado.Cloud;
using Modelado.Geometry;
using Modelado.Imaging;
using Modelado.Inference;

namespace Modelado.Cli;

/// <summary>
/// modelado-cli cloud (MOD65): the window's cloud door without the window, for the Blender add-on
/// and scripts. It reads the same DPAPI key store (MOD36), the same budgets in settings.json and
/// the same ledger (MOD41), so a job sent from here is priced, refused, recorded and shown in the
/// window's About exactly as one sent from there.
/// <list type="bullet">
/// <item>cloud image-to-3d &lt;photo&gt; -o &lt;out.glb&gt; [--service meshy] [--no-texture] [--pbr] [--quads]:
/// sends the photo and follows the job to the end; Ctrl+C asks the service to stop it.</item>
/// <item>MOD45: cloud texture "&lt;material&gt;" -o &lt;tile.png&gt; draws a seamless texture with Ideogram, as
/// the window's New texture asks; cloud remesh &lt;model&gt; -o &lt;out.glb&gt; [--polycount N] [--quads] and
/// cloud retexture &lt;model&gt; --prompt "…" -o &lt;out.glb&gt; [--keep-uv] send the model to Meshy AI.</item>
/// <item>with --estimate: says what would be sent, to whom, for how much and what the budget
/// leaves, the window's consent line word for word, and sends nothing.</item>
/// <item>cloud key &lt;service&gt;: stores the key read from standard input, once the service accepts it,
/// so it never sits in a shell's history.</item>
/// </list>
/// </summary>
internal static class CloudCommand
{
    /// <summary>Runs one cloud command.</summary>
    /// <param name="positional">The words after "cloud": the action, then its photo or service.</param>
    /// <param name="options">The options given.</param>
    /// <param name="report">The --json report, filled in.</param>
    /// <param name="save">Writes a mesh where -o says, as the local commands do.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> positional, IReadOnlyDictionary<string, string?> options, JsonObject report, Action<Mesh, string, Stopwatch> save)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("Cloud keys are kept with Windows DPAPI, so cloud commands run on Windows only.");
        }

        IReadOnlyList<ICloudProvider> services = CloudProviders.Build(Apis(options));
        CloudKeys keys = new(CloudKeys.DefaultFolder);
        string? At(int index) => index < positional.Count ? positional[index] : null;
        string action = At(0) ?? "";
        if (action == "key")
        {
            return await StoreKeyAsync(Service(services, At(1) ?? throw new ArgumentException("cloud key <service>: which service?")), keys, report).ConfigureAwait(false);
        }

        string subject = At(1) ?? throw new ArgumentException(action switch
        {
            "image-to-3d" => "cloud image-to-3d: which photo?",
            "texture" => "cloud texture: which material, e.g. \"weathered oak planks\"?",
            "remesh" or "retexture" => $"cloud {action}: which model?",
            _ => "cloud: use image-to-3d, texture, remesh, retexture or key.",
        });
        bool estimateOnly = options.ContainsKey("--estimate");
        string? output = options.GetValueOrDefault("--out");
        if (!estimateOnly && output is null)
        {
            throw new ArgumentException($"-o <output{(action == "texture" ? ".png" : ".glb")}> is required.");
        }

        // What is sent, and the words the window's consent line begins with for it.
        (CloudJobRequest request, string what) = action switch
        {
            "image-to-3d" => Photo(subject, options),
            "texture" => (CloudRequests.Texture(subject, 1), "This description"),
            "remesh" or "retexture" => Model(action, subject, options),
            _ => throw new ArgumentException("cloud: use image-to-3d, texture, remesh, retexture or key."),
        };
        if (!estimateOnly && action == "texture" && !string.Equals(Path.GetExtension(output), ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{Path.GetFileName(output)}: a texture is saved as .png.");
        }
        else if (!estimateOnly && action != "texture")
        {
            MeshExport.FormatOf(output!);
        }

        ICloudProvider service = Service(services, options.GetValueOrDefault("--service") ?? (action == "texture" ? "ideogram" : "meshy"));
        if ((service.Capabilities & request.Action) != request.Action)
        {
            throw new ArgumentException($"--service {service.Id}: {service.DisplayName} cannot {action}.");
        }

        // The window's consent line, from the same price table, budgets and ledger.
        CloudSpending spending = new(new CloudLedger(CloudLedger.DefaultPath), CloudBudget.FromSettings(SettingsFile.ReadObject(SettingsFile.DefaultPath, CloudBudget.SettingsKey)));
        CloudCost? estimate = service.EstimateCost(request);
        (string note, bool blocked) = spending.Note(service, request);
        string consent = $"{what} goes to {service.DisplayName}{(estimate is null ? "" : $" for about {estimate}")}.{note}";
        report["service"] = service.Id;
        report["serviceName"] = service.DisplayName;
        report["keyStored"] = keys.Has(service.Id);
        report["consent"] = consent;
        report["blocked"] = blocked;
        if (estimate is not null)
        {
            report["estimate"] = new JsonObject { ["amount"] = estimate.Amount, ["unit"] = estimate.Unit, ["text"] = estimate.ToString() };
        }

        Console.WriteLine(consent);
        if (estimateOnly)
        {
            return 0;
        }

        if (keys.Load(service.Id) is not { } key)
        {
            throw new InvalidOperationException($"No {service.DisplayName} key is stored: add one in Modelado's About, or with modelado-cli cloud key {service.Id}.");
        }

        if (blocked)
        {
            throw new BudgetExceededException(note.Trim());
        }

        // Ctrl+C is the window's Cancel: the service is asked to stop, so the job stops spending.
        using CancellationTokenSource cancel = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancel.Cancel();
        };
        Stopwatch clock = Stopwatch.StartNew();
        CloudJobState? said = null;
        CloudResult result = await CloudJobs.RunAsync(service, key, request, new Reporter(status =>
        {
            if (status.State != said || status.State == CloudJobState.Running)
            {
                said = status.State;
                Console.WriteLine(status.State switch
                {
                    CloudJobState.Queued => $"  waiting at {service.DisplayName}",
                    CloudJobState.Running => string.Create(CultureInfo.InvariantCulture, $"  {status.Message ?? "working"} {status.Progress,6:P0}"),
                    _ => "  fetching the result",
                });
            }
        }), cancel.Token, spending: spending).ConfigureAwait(false);

        if (result.IsModel)
        {
            save(CloudJobs.ToMesh(result), output!, clock);
        }
        else
        {
            RgbaImage image = CloudJobs.ToImage(result);
            ImageCodec.SavePng(image, output!);
            Console.WriteLine($"{image.Width} × {image.Height} -> {output}");
            report["written"] = new JsonArray(JsonValue.Create(Path.GetFullPath(output!)));
            report["seconds"] = Math.Round(clock.Elapsed.TotalSeconds, 3);
        }

        CloudCost? spent = result.Actual ?? estimate;
        report["spent"] = spent?.ToString();
        Console.WriteLine($"{CloudJobs.Label(service, request.Action)}{(spent is null ? "" : $" · {spent}")}");
        return 0;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static async Task<int> StoreKeyAsync(ICloudProvider service, CloudKeys keys, JsonObject report)
    {
        string key = (await Console.In.ReadLineAsync().ConfigureAwait(false))?.Trim() ?? "";
        if (key.Length == 0)
        {
            throw new ArgumentException($"cloud key {service.Id}: give the key on standard input.");
        }

        KeyCheck check = await service.ValidateKeyAsync(key, CancellationToken.None).ConfigureAwait(false);
        report["service"] = service.Id;
        report["valid"] = check.Valid;
        Console.WriteLine(check.Message);
        if (!check.Valid)
        {
            throw new InvalidOperationException(check.Message);
        }

        keys.Save(service.Id, key);
        return 0;
    }

    private static (CloudJobRequest, string) Photo(string path, IReadOnlyDictionary<string, string?> options)
    {
        byte[] png = ImageCodec.EncodePng(ImageCodec.Load(path));
        bool textured = !options.ContainsKey("--no-texture");
        return (new CloudJobRequest(
            CloudCapability.ImageTo3D,
            Image: png,
            Options: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["texture"] = textured ? "true" : "false",
                ["pbr"] = options.ContainsKey("--pbr") && textured ? "true" : "false",
                ["topology"] = options.ContainsKey("--quads") ? "quad" : "triangle",
            }), $"This photo, {Megabytes(png.Length)},");
    }

    // The model as the window sends it (MOD38): a GLB, said with its triangles and size.
    private static (CloudJobRequest, string) Model(string action, string path, IReadOnlyDictionary<string, string?> options)
    {
        Mesh mesh = MeshImport.Load(path);
        using MemoryStream glb = new();
        MeshExport.WriteGlb(mesh, glb);
        CloudJobRequest request;
        if (action == "remesh")
        {
            string given = options.GetValueOrDefault("--polycount") ?? "30000";
            request = int.TryParse(given, NumberStyles.None, CultureInfo.InvariantCulture, out int polycount) && polycount is >= 100 and <= 300_000
                ? CloudRequests.Remesh(glb.ToArray(), polycount, options.ContainsKey("--quads"))
                : throw new ArgumentException($"--polycount {given}: use 100 to 300000.");
        }
        else
        {
            string prompt = options.GetValueOrDefault("--prompt") ?? throw new ArgumentException("cloud retexture: --prompt \"what the surface looks like\" is required.");
            request = CloudRequests.Retexture(glb.ToArray(), prompt, options.ContainsKey("--keep-uv") && mesh.TexCoords is not null);
        }

        return (request, string.Create(CultureInfo.InvariantCulture, $"This model, {mesh.TriangleCount} triangles, {Megabytes(glb.Length)} as a GLB,"));
    }

    private static ICloudProvider Service(IReadOnlyList<ICloudProvider> services, string id) =>
        services.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException($"--service {id}: use {string.Join(" or ", services.Select(p => p.Id))}.");

    // --cloud-api meshy=http://127.0.0.1:47631/meshy/, a stand-in as the window's own flag takes.
    private static Dictionary<string, Uri>? Apis(IReadOnlyDictionary<string, string?> options)
    {
        if (options.GetValueOrDefault("--cloud-api") is not { } given)
        {
            return null;
        }

        string[] parts = given.Split('=', 2);
        return parts.Length == 2 && Uri.TryCreate(parts[1], UriKind.Absolute, out Uri? api)
            ? new Dictionary<string, Uri>(StringComparer.Ordinal) { [parts[0]] = api }
            : throw new ArgumentException($"--cloud-api {given}: use id=url, e.g. meshy=https://api.meshy.ai/.");
    }

    // As the window's DownloadText.Size says it, in whole megabytes.
    private static string Megabytes(long bytes) => string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, (bytes + (1 << 19)) >> 20)} MB");

    private sealed class Reporter(Action<CloudJobStatus> report) : IProgress<CloudJobStatus>
    {
        public void Report(CloudJobStatus value) => report(value);
    }
}
