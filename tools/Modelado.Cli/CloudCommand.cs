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
        switch (At(0))
        {
            case "key":
                return await StoreKeyAsync(Service(services, At(1) ?? throw new ArgumentException("cloud key <service>: which service?")), keys, report).ConfigureAwait(false);
            case "image-to-3d":
                break;
            default:
                throw new ArgumentException("cloud: use image-to-3d or key.");
        }

        ICloudProvider service = Service(services, options.GetValueOrDefault("--service") ?? "meshy");
        string photoPath = At(1) ?? throw new ArgumentException("cloud image-to-3d: which photo?");
        bool estimateOnly = options.ContainsKey("--estimate");
        string? output = options.GetValueOrDefault("--out");
        if (!estimateOnly)
        {
            MeshExport.FormatOf(output ?? throw new ArgumentException("-o <output.glb> is required."));
        }

        RgbaImage photo = ImageCodec.Load(photoPath);
        CloudJobRequest request = new(
            CloudCapability.ImageTo3D,
            Image: ImageCodec.EncodePng(photo),
            Options: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["texture"] = options.ContainsKey("--no-texture") ? "false" : "true",
                ["pbr"] = options.ContainsKey("--pbr") && !options.ContainsKey("--no-texture") ? "true" : "false",
                ["topology"] = options.ContainsKey("--quads") ? "quad" : "triangle",
            });
        if ((service.Capabilities & request.Action) != request.Action)
        {
            throw new ArgumentException($"{service.DisplayName} cannot turn a photo into a 3D model.");
        }

        // The window's consent line, from the same price table, budgets and ledger.
        CloudSpending spending = new(new CloudLedger(CloudLedger.DefaultPath), CloudBudget.FromSettings(SettingsFile.ReadObject(SettingsFile.DefaultPath, CloudBudget.SettingsKey)));
        CloudCost? estimate = service.EstimateCost(request);
        (string note, bool blocked) = spending.Note(service, request);
        string consent = $"This photo, {Megabytes(request.Image!.Length)}, goes to {service.DisplayName}{(estimate is null ? "" : $" for about {estimate}")}.{note}";
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
                    _ => "  fetching the model",
                });
            }
        }), cancel.Token, spending: spending).ConfigureAwait(false);

        save(CloudJobs.ToMesh(result), output!, clock);
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
