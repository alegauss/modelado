using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Modelado.Cli;
using Modelado.Geometry;
using Modelado.Imaging;
using Modelado.Inference;
using Modelado.Inference.Pipelines;

// Modelado from the command line: the same pipelines as the app, for batches, scripts and the
// machines (and tests) that have no window. Runs on Windows with DirectML and anywhere on the CPU.

const string Usage = """
    modelado-cli object <photo> -o <output.glb|obj|ply|stl> [options]
        --resolution N       grid points per side (128, 192, 256, 320, 384); default 256
        --keep-background    does not cut the object out (for a PNG already cut out)
        --threshold D        surface density (the model's default, 25)
        --step N             coarse sampling step, 1 = full; default 2
        --smooth N           smoothing passes; default 2
        --enhance            enhances a small photo with Real-ESRGAN first
        --no-project         does not lay the photo's colours back on the object's front
    modelado-cli relief <photo> -o <output> [options]
        --grid N             vertices on the longest side; default 256
        --depth F            relief height, as a share of the width; default 0.25
        --base F             solid base thickness, as a share of the width; 0 = open surface
        --remove-background  the object only, without the background
        --depth-model NAME   another depth model, from --source; default depth-anything-v2-small
    modelado-cli edit <model.glb|obj|ply|stl> -o <output> [edits, applied in this order]
        --rotate x:90        turns about x, y or z (degrees)
        --mirror             mirrors left to right
        --remove-pieces F    removes pieces smaller than F of the largest (e.g. 0.05)
        --fill-holes         fills the holes
        --simplify N|F       N triangles, or the share F (e.g. 0.25)
        --subdivide          4× the triangles, rounded off
        --smooth N           smoothing passes (Taubin)
        --flatten F          flat base at F of the height (e.g. 0.03)
        --texture IMAGE      puts a material image on the surface, made to tile first
        --tiles N            how many times it repeats across the longest side; default 2
        --upscale-texture    upscales the texture 4× with Real-ESRGAN
    modelado-cli download [object|relief|upscale|all]   downloads the missing models
        --dry-run            says what would be downloaded, and fetches nothing
        --remove-background  relief: U-2-Net too, which a relief of the object alone needs
    modelado-cli models                         lists the installed models
    modelado-cli cloud image-to-3d <photo> -o <output.glb> [options]   with the key stored in About
        --service ID         meshy (the default)
        --estimate           says what would be sent, to whom, for how much, and sends nothing
        --no-texture         the bare shape, cheaper
        --pbr                PBR maps with the texture
        --quads              quad topology
        --cloud-api ID=URL   another API root, e.g. a stand-in
    modelado-cli cloud texture "<material>" -o <tile.png>   a seamless texture drawn by Ideogram
    modelado-cli cloud remesh <model> -o <output.glb>       Meshy AI's clean topology
        --polycount N        100 to 300000 triangles; default 30000
    modelado-cli cloud retexture <model> --prompt "<text>" -o <output.glb>   Meshy AI's new texture
        --keep-uv            keeps the model's own UVs
    (each cloud command takes --service, --estimate and --cloud-api as image-to-3d does)
    modelado-cli cloud key <service>            stores the key given on standard input

    Common: --provider auto|dml|cpu   --high (FP32 on the GPU too)   --models <folder>
            --manifest <url>          --size <units> (longest side when saving)
            --source <owner/name|url> a model repository read after the built-in one
            --preview <image.png>     saves a rendered view, on a transparent background
            --compact                 a GLB's geometry quantized, about half the size (the web, Blender)
            --json                    prints the result as one JSON object on stdout, the rest on stderr
    """;

// MOD49: English output in one culture, as the window's, whatever the machine is set to.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    return 0;
}

// MOD48: refused here, before anything loads, as ERROR and an exit code a script can read.
(CliArguments? parsed, string? refusal, int refusalCode) = CliArguments.Parse(args);
if (parsed is null)
{
    await Console.Error.WriteLineAsync($"ERROR {refusal}{(CliArguments.Commands.Contains(args[0]) ? "" : $"\n{Usage}")}").ConfigureAwait(false);
    return refusalCode;
}

IReadOnlyDictionary<string, string?> options = parsed.Options;
List<string> positional = [.. parsed.Positional];

// MOD43: with --json every line a person reads goes to stderr and stdout carries one JSON object
// (what was written, the counts, each edit and its time, warnings, or the error), which the
// Blender add-on reads instead of parsing text.
bool json = options.ContainsKey("--json");
TextWriter stdout = Console.Out;
JsonObject report = new() { ["command"] = args[0] };
if (json)
{
    Console.SetOut(Console.Error);
}

try
{
    // MOD65: nothing runs here, so no execution provider is chosen or named.
    if (args[0] == "cloud")
    {
        return Done(await CloudCommand.RunAsync(positional, options, report, Save).ConfigureAwait(false));
    }

    ModelLibrary library = options.TryGetValue("--models", out string? folder) && folder is not null
        ? new ModelLibrary([folder])
        : ModelLibrary.Default();
    ProviderChoice choice = Get("--provider", "auto") switch
    {
        "cpu" => ProviderChoice.Cpu,
        "dml" => ProviderChoice.DirectML,
        "auto" => ProviderChoice.Auto,
        var other => throw new ArgumentException($"--provider {other}: use auto, dml or cpu."),
    };
    (ExecutionProvider provider, string? reason) = ExecutionProviders.Resolve(choice);
    using ModelRunner runner = new(library, provider, options.ContainsKey("--high"));
    // MOD52: the words the window's provider chip shows, the GPU named.
    string where = ExecutionProviders.Label(provider, provider == ExecutionProvider.DirectML ? GpuAdapter.ForDevice() : null);
    Console.WriteLine($"Running on {where}{(reason is null ? "" : $": {reason}")}");

    switch (args[0])
    {
        case "models":
            foreach (InstalledModel installed in library.Installed())
            {
                Console.WriteLine($"{installed.Name,-26} {string.Join(", ", installed.Variants),-11} {installed.Meta.Upstream.License}  ({installed.Root})");
            }

            return 0;

        case "download":
            string which = positional.FirstOrDefault() ?? "all";
            IEnumerable<string> names = which switch
            {
                "object" => ObjectReconstruction.Models,
                // What `relief` itself fetches: U-2-Net only with --remove-background, as there (MOD44).
                "relief" => ReliefReconstruction.Models(options.ContainsKey("--remove-background")),
                "upscale" => new[] { Upscaling.Model },
                "all" => ObjectReconstruction.Models.Concat(ReliefReconstruction.Models(true)).Append(Upscaling.Model).Distinct(),
                _ => throw new ArgumentException($"download {which}: use object, relief, upscale or all."),
            };
            return Done(await Download(runner, names.ToList()).ConfigureAwait(false));

        case "object" or "relief":
            string photoPath = positional.FirstOrDefault() ?? throw new ArgumentException("Which photo? " + Usage);
            string output = Get("--out", null) ?? throw new ArgumentException("-o <output.glb> is required.");
            MeshExport.FormatOf(output);
            string depthModel = Get("--depth-model", ReliefReconstruction.Depth)!;
            IReadOnlyList<string> needed = args[0] == "object"
                ? [.. ObjectReconstruction.Models, .. options.ContainsKey("--enhance") ? [Upscaling.Model] : Array.Empty<string>()]
                : ReliefReconstruction.Models(options.ContainsKey("--remove-background"), depthModel);
            if (runner.Missing(needed).Count > 0 && await Download(runner, needed).ConfigureAwait(false) != 0)
            {
                return 1;
            }

            RgbaImage photo = ImageCodec.Load(photoPath);
            Progress progress = new();
            Stopwatch total = Stopwatch.StartNew();
            ReconstructionResult result = args[0] == "object"
                ? ObjectReconstruction.Run(runner, photo, new ObjectOptions(
                    Resolution: Int("--resolution", 256),
                    RemoveBackground: !options.ContainsKey("--keep-background"),
                    DensityThreshold: options.ContainsKey("--threshold") ? (float)Number("--threshold", 25) : null,
                    CoarseStep: Int("--step", 2),
                    SmoothingIterations: Int("--smooth", 2),
                    EnhancePhoto: options.ContainsKey("--enhance"),
                    ProjectPhoto: !options.ContainsKey("--no-project")), progress)
                : ReliefReconstruction.Run(runner, photo, new ReliefRunOptions(
                    new ReliefOptions(GridLongSide: Int("--grid", 256), Depth: (float)Number("--depth", 0.25), BaseThickness: (float)Number("--base", 0)),
                    RemoveBackground: options.ContainsKey("--remove-background"),
                    DepthModel: depthModel), progress);
            Console.WriteLine();
            foreach ((ReconstructionStage stage, TimeSpan took) in result.Timings)
            {
                Console.WriteLine($"  {stage,-20} {took.TotalSeconds,6:0.00} s");
            }

            Save(result.Mesh, output, total);
            if (runner.Notice is { } notice)
            {
                Console.WriteLine(notice);
                report["warnings"] = new JsonArray(notice);
            }

            return Done(0);

        case "edit":
            string modelPath = positional.FirstOrDefault() ?? throw new ArgumentException("Which model? " + Usage);
            string edited = Get("--out", null) ?? throw new ArgumentException("-o <output.glb> is required.");
            MeshExport.FormatOf(edited);
            if (options.ContainsKey("--upscale-texture") && runner.Missing([Upscaling.Model]).Count > 0 && await Download(runner, [Upscaling.Model]).ConfigureAwait(false) != 0)
            {
                return 1;
            }

            Stopwatch clock = Stopwatch.StartNew();
            Mesh model = MeshImport.Load(modelPath);
            Console.WriteLine($"{Path.GetFileName(modelPath)}: {model.VertexCount:N0} vertices, {model.TriangleCount:N0} triangles{(model.Texture is null ? "" : $", texture {model.Texture.Width} × {model.Texture.Height}")}");
            model = Edit(model, "--rotate", value =>
            {
                string[] parts = value.Split(':');
                Axis axis = parts[0].ToUpperInvariant() switch { "X" => Axis.X, "Y" => Axis.Y, "Z" => Axis.Z, _ => throw new ArgumentException($"--rotate {value}: use x:90, y:180, z:-90.") };
                return MeshEdit.Rotate(model, axis, float.Parse(parts[1], CultureInfo.InvariantCulture));
            });
            model = Edit(model, "--mirror", _ => MeshEdit.Mirror(model));
            model = Edit(model, "--remove-pieces", value => MeshCleanup.RemoveSmallPieces(model, double.Parse(value, CultureInfo.InvariantCulture)));
            model = Edit(model, "--fill-holes", _ => MeshEdit.FillHoles(model).Mesh);
            model = Edit(model, "--simplify", value =>
            {
                double amount = double.Parse(value, CultureInfo.InvariantCulture);
                return MeshEdit.Simplify(model, amount < 1 ? (int)(model.TriangleCount * amount) : (int)amount);
            });
            model = Edit(model, "--subdivide", _ => MeshEdit.Subdivide(model));
            model = Edit(model, "--smooth", value => MeshCleanup.Smooth(model, int.Parse(value, CultureInfo.InvariantCulture)));
            model = Edit(model, "--flatten", value => MeshEdit.FlattenBase(model, float.Parse(value, CultureInfo.InvariantCulture)));
            // MOD40: before --upscale-texture, so a new texture can be sharpened in the same run.
            model = Edit(model, "--texture", value => MaterialTexture.Apply(model, MaterialTexture.Seamless(ImageCodec.Load(value)), (float)Number("--tiles", 2)));
            model = Edit(model, "--upscale-texture", _ => Upscaling.UpscaleTexture(runner, model));
            Save(model, edited, clock);
            return Done(0);

        default:
            await Console.Error.WriteLineAsync($"Unknown command: {args[0]}\n{Usage}").ConfigureAwait(false);
            return 2;
    }
}
catch (Exception error) when (error is ArgumentException or InvalidDataException or InvalidOperationException or FileNotFoundException or HttpRequestException or OperationCanceledException)
{
    await Console.Error.WriteLineAsync($"ERROR {error.Message}").ConfigureAwait(false);
    report["error"] = error.Message;
    return Done(1);
}

// The exit code, and with --json the report on the real stdout.
int Done(int code)
{
    if (json)
    {
        report["exitCode"] = code;
        stdout.WriteLine(report.ToJsonString());
        stdout.Flush();
    }

    return code;
}

void Save(Mesh mesh, string output, Stopwatch total)
{
    mesh = options.ContainsKey("--size") ? mesh.Placed((float)Number("--size", 1)) : mesh;
    IReadOnlyList<string> written = MeshExport.Save(mesh, output, new ExportOptions(GlbCompact: options.ContainsKey("--compact")));
    Console.WriteLine($"{mesh.VertexCount:N0} vertices, {mesh.TriangleCount:N0} triangles in {total.Elapsed.TotalSeconds:0.0} s -> {string.Join(", ", written)}");
    report["written"] = new JsonArray([.. written.Select(path => JsonValue.Create(Path.GetFullPath(path)))]);
    report["vertices"] = mesh.VertexCount;
    report["triangles"] = mesh.TriangleCount;
    report["seconds"] = Math.Round(total.Elapsed.TotalSeconds, 3);
    if (Get("--preview", null) is { } preview)
    {
        // The function the window's Save view as image calls too (MOD19).
        new MeshRenderer(mesh).SaveView(preview, 800, 800, new OrbitCamera(Yaw: 25, Pitch: 15));
        Console.WriteLine($"view -> {preview}");
    }
}

Mesh Edit(Mesh mesh, string key, Func<string, Mesh> edit)
{
    if (!options.TryGetValue(key, out string? value))
    {
        return mesh;
    }

    Stopwatch clock = Stopwatch.StartNew();
    Mesh result = edit(value ?? "");
    Console.WriteLine($"  {key,-18} {result.TriangleCount,10:N0} triangles  {clock.Elapsed.TotalSeconds,6:0.00} s");
    if (report["edits"] is not JsonArray edits)
    {
        report["edits"] = edits = [];
    }

    edits.Add(new JsonObject { ["edit"] = key, ["value"] = value, ["triangles"] = result.TriangleCount, ["seconds"] = Math.Round(clock.Elapsed.TotalSeconds, 3) });
    return result;
}

string? Get(string key, string? fallback) => options.TryGetValue(key, out string? value) ? value : fallback;

int Int(string key, int fallback) => int.Parse(Get(key, null) ?? fallback.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

double Number(string key, double fallback) => double.Parse(Get(key, null) ?? fallback.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

async Task<int> Download(ModelRunner runner, IReadOnlyCollection<string> models)
{
    using HttpClient http = new() { Timeout = Timeout.InfiniteTimeSpan };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Modelado/0.1");
    ModelManifest manifest = await ModelDownloads.LoadManifestAsync(http, Get("--manifest", null), CancellationToken.None).ConfigureAwait(false);
    if (Get("--source", null) is { } given)
    {
        // MOD27: the repository is read after the built-in models, as the window reads an added source.
        ModelSource source = await ModelSources.ResolveAsync(http, given, CancellationToken.None).ConfigureAwait(false);
        manifest = await ModelSources.WithSourcesAsync(http, manifest, [source], CancellationToken.None).ConfigureAwait(false);
    }

    string variant = ExecutionProviders.VariantFor(runner.Provider, runner.HighQuality);
    ModelRequest[] requests = [.. models.Select(m => new ModelRequest(m, variant))];
    long pending = ModelDownloads.PendingBytes(manifest, runner.Library.DownloadRoot, requests, runner.Library.Roots);
    if (options.ContainsKey("--dry-run"))
    {
        // MOD44: what a first use would fetch, so a caller can ask before it does (the Blender add-on).
        string[] missing = [.. runner.Missing(models).Select(r => r.Model)];
        Console.WriteLine(missing.Length == 0 ? "Nothing to download." : $"Would download {pending / 1e6:0} MB to {runner.Library.DownloadRoot}: {string.Join(", ", missing)}");
        report["pending"] = pending;
        report["models"] = new JsonArray([.. missing.Select(m => JsonValue.Create(m))]);
        report["licenses"] = new JsonArray([.. missing.Select(m => manifest.Model(m) is { } listed ? JsonValue.Create($"{m}: {listed.License}") : null)]);
        report["from"] = string.Join(" and ", missing.Select(m => manifest.Model(m)?.Variants.Values.First().Url).OfType<string>().Select(u => new Uri(u).Host).Distinct());
        return 0;
    }

    Console.WriteLine($"Downloading {pending / 1e6:0} MB to {runner.Library.DownloadRoot}");
    int lastPercent = -1;
    DownloadResult result = await ModelDownloads.DownloadAsync(http, manifest, runner.Library.DownloadRoot, requests, new Progress<DownloadProgress>(p =>
    {
        int percent = (int)(100 * p.Done / Math.Max(1, p.Total));
        if (percent != lastPercent)
        {
            lastPercent = percent;
            Console.Write($"\r  {percent,3}%  {p.File,-36} {p.BytesPerSecond / 1e6,6:0.0} MB/s");
        }
    }), CancellationToken.None, runner.Library.Roots).ConfigureAwait(false);
    Console.WriteLine($"\n{result.Fetched} downloaded, {result.Present} already present, all verified by SHA-256.");
    runner.Library.Refresh();
    return 0;
}

/// <summary>Prints each step's progress on one line.</summary>
internal sealed class Progress : IProgress<ReconstructionProgress>
{
    private ReconstructionStage? current;

    public void Report(ReconstructionProgress value)
    {
        if (current != value.Stage)
        {
            Console.WriteLine();
            current = value.Stage;
        }

        Console.Write($"\r  {value.Stage,-20} {value.Fraction,6:P0}");
    }
}
