using System.Diagnostics;
using System.Globalization;
using Modelado.Geometry;
using Modelado.Imaging;
using Modelado.Inference;
using Modelado.Inference.Pipelines;

// Modelado from the command line: the same pipelines as the app, for batches, scripts and the
// machines (and tests) that have no window. Runs on Windows with DirectML and anywhere on the CPU.

const string Usage = """
    modelado-cli object <foto> -o <saida.glb|obj|ply|stl> [opções]
        --resolution N       pontos da grade por lado (128, 192, 256, 320, 384); padrão 256
        --keep-background    não recorta o objeto (use com PNG já recortado)
        --threshold D        densidade da superfície (padrão do modelo, 25)
        --step N             passo da amostragem grossa, 1 = completa; padrão 2
        --smooth N           passes de suavização; padrão 2
        --enhance            melhora uma foto pequena com Real-ESRGAN antes
        --no-project         não reprojeta as cores da foto na frente do objeto
    modelado-cli relief <foto> -o <saida> [opções]
        --grid N             vértices no lado maior; padrão 256
        --depth F            altura do relevo, fração da largura; padrão 0.25
        --base F             espessura da base sólida, fração da largura; 0 = superfície aberta
        --remove-background  só o objeto, sem o fundo
    modelado-cli edit <modelo.glb|obj|ply|stl> -o <saida> [edições, aplicadas nesta ordem]
        --rotate x:90        gira em torno de x, y ou z (graus)
        --mirror             espelha da esquerda para a direita
        --remove-pieces F    remove pedaços menores que F do maior (ex.: 0.05)
        --fill-holes         fecha os buracos
        --simplify N|F       N triângulos, ou a fração F (ex.: 0.25)
        --subdivide          4× os triângulos, arredondando
        --smooth N           passes de suavização (Taubin)
        --flatten F          base plana a F da altura (ex.: 0.03)
        --upscale-texture    aumenta a textura 4× com Real-ESRGAN
    modelado-cli download [object|relief|all]   baixa os modelos que faltam
    modelado-cli models                         lista os modelos instalados

    Comuns: --provider auto|dml|cpu   --high (FP32 também na GPU)   --models <pasta>
            --manifest <url>          --size <unidades> (maior lado ao salvar)
            --preview <imagem.png>    salva uma vista renderizada
    """;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    return 0;
}

Dictionary<string, string?> options = new(StringComparer.Ordinal);
List<string> positional = [];
for (int i = 1; i < args.Length; i++)
{
    if (args[i].StartsWith("--", StringComparison.Ordinal) || args[i] == "-o")
    {
        string key = args[i] == "-o" ? "--out" : args[i];
        bool flag = key is "--keep-background" or "--remove-background" or "--high" or "--enhance" or "--no-project"
            or "--mirror" or "--fill-holes" or "--subdivide" or "--upscale-texture";
        options[key] = flag ? "true" : i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{key} needs a value.");
    }
    else
    {
        positional.Add(args[i]);
    }
}

try
{
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
    Console.WriteLine($"Executando em {(provider == ExecutionProvider.DirectML ? "GPU (DirectML)" : "CPU")}{(reason is null ? "" : $": {reason}")}");

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
                "relief" => ReliefReconstruction.Models(true),
                "all" => ObjectReconstruction.Models.Concat(ReliefReconstruction.Models(true)).Append(Upscaling.Model).Distinct(),
                _ => throw new ArgumentException($"download {which}: use object, relief or all."),
            };
            return await Download(runner, names.ToList()).ConfigureAwait(false);

        case "object" or "relief":
            string photoPath = positional.FirstOrDefault() ?? throw new ArgumentException("Which photo? " + Usage);
            string output = Get("--out", null) ?? throw new ArgumentException("-o <saida.glb> is required.");
            MeshExport.FormatOf(output);
            IReadOnlyList<string> needed = args[0] == "object"
                ? [.. ObjectReconstruction.Models, .. options.ContainsKey("--enhance") ? [Upscaling.Model] : Array.Empty<string>()]
                : ReliefReconstruction.Models(options.ContainsKey("--remove-background"));
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
                    RemoveBackground: options.ContainsKey("--remove-background")), progress);
            Console.WriteLine();
            foreach ((ReconstructionStage stage, TimeSpan took) in result.Timings)
            {
                Console.WriteLine($"  {stage,-20} {took.TotalSeconds,6:0.00} s");
            }

            Save(result.Mesh, output, total);
            if (runner.Notice is { } notice)
            {
                Console.WriteLine(notice);
            }

            return 0;

        case "edit":
            string modelPath = positional.FirstOrDefault() ?? throw new ArgumentException("Which model? " + Usage);
            string edited = Get("--out", null) ?? throw new ArgumentException("-o <saida.glb> is required.");
            MeshExport.FormatOf(edited);
            if (options.ContainsKey("--upscale-texture") && runner.Missing([Upscaling.Model]).Count > 0 && await Download(runner, [Upscaling.Model]).ConfigureAwait(false) != 0)
            {
                return 1;
            }

            Stopwatch clock = Stopwatch.StartNew();
            Mesh model = MeshImport.Load(modelPath);
            Console.WriteLine($"{Path.GetFileName(modelPath)}: {model.VertexCount:N0} vértices, {model.TriangleCount:N0} triângulos{(model.Texture is null ? "" : $", textura {model.Texture.Width} × {model.Texture.Height}")}");
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
            model = Edit(model, "--upscale-texture", _ => Upscaling.UpscaleTexture(runner, model));
            Save(model, edited, clock);
            return 0;

        default:
            await Console.Error.WriteLineAsync($"Comando desconhecido: {args[0]}\n{Usage}").ConfigureAwait(false);
            return 2;
    }
}
catch (Exception error) when (error is ArgumentException or InvalidDataException or InvalidOperationException or FileNotFoundException or HttpRequestException)
{
    await Console.Error.WriteLineAsync($"ERRO {error.Message}").ConfigureAwait(false);
    return 1;
}

void Save(Mesh mesh, string output, Stopwatch total)
{
    mesh = options.ContainsKey("--size") ? mesh.Placed((float)Number("--size", 1)) : mesh;
    IReadOnlyList<string> written = MeshExport.Save(mesh, output);
    Console.WriteLine($"{mesh.VertexCount:N0} vértices, {mesh.TriangleCount:N0} triângulos em {total.Elapsed.TotalSeconds:0.0} s -> {string.Join(", ", written)}");
    if (Get("--preview", null) is { } preview)
    {
        MeshRenderer renderer = new(mesh);
        ImageCodec.SavePng(renderer.RenderImage(800, 800, new OrbitCamera(Yaw: 25, Pitch: 15)), preview);
        Console.WriteLine($"vista -> {preview}");
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
    Console.WriteLine($"  {key,-18} {result.TriangleCount,10:N0} triângulos  {clock.Elapsed.TotalSeconds,6:0.00} s");
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
    string variant = ExecutionProviders.VariantFor(runner.Provider, runner.HighQuality);
    ModelRequest[] requests = [.. models.Select(m => new ModelRequest(m, variant))];
    long pending = ModelDownloads.PendingBytes(manifest, runner.Library.DownloadRoot, requests, runner.Library.Roots);
    Console.WriteLine($"Baixando {pending / 1e6:0} MB para {runner.Library.DownloadRoot}");
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
    Console.WriteLine($"\n{result.Fetched} baixado(s), {result.Present} já presente(s), todos verificados por SHA-256.");
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
