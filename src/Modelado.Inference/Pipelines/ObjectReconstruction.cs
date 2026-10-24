using System.Diagnostics;
using System.Numerics;
using Microsoft.ML.OnnxRuntime;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Inference.Pipelines;

/// <summary>The steps a reconstruction reports, which the app names.</summary>
public enum ReconstructionStage
{
    /// <summary>U-2-Net is cutting the object out.</summary>
    RemovingBackground,

    /// <summary>TripoSR's encoder is turning the photo into a triplane.</summary>
    Encoding,

    /// <summary>The decoder is sampling the density on the grid.</summary>
    SamplingDensity,

    /// <summary>Marching cubes and clean-up.</summary>
    ExtractingSurface,

    /// <summary>The decoder is colouring the vertices.</summary>
    Colouring,

    /// <summary>Depth Anything is estimating depth.</summary>
    EstimatingDepth,

    /// <summary>The relief mesh is being built.</summary>
    BuildingRelief,
}

/// <summary>How far a reconstruction is.</summary>
/// <param name="Stage">The current step.</param>
/// <param name="Fraction">How much of the step is done, 0 to 1.</param>
public readonly record struct ReconstructionProgress(ReconstructionStage Stage, double Fraction);

/// <summary>How a photo becomes an object.</summary>
/// <param name="Resolution">Grid points per side for marching cubes: 128 is quick, 256 TripoSR's default, 384 fine.</param>
/// <param name="RemoveBackground">Cut the object out with U-2-Net; false trusts the photo's own transparency or uses it whole.</param>
/// <param name="ForegroundRatio">The share of the input the object fills, TripoSR's 0.85.</param>
/// <param name="DensityThreshold">The density the surface is drawn at; null takes meta.json's (25). Lower is fatter.</param>
/// <param name="CoarseStep">See <see cref="FieldSampler"/>: 1 evaluates every grid point, 2 about a quarter of them.</param>
/// <param name="SmoothingIterations">Taubin smoothing passes after marching cubes.</param>
/// <param name="KeepPieces">Pieces smaller than this share of the largest are dropped.</param>
public sealed record ObjectOptions(
    int Resolution = 256,
    bool RemoveBackground = true,
    double ForegroundRatio = 0.85,
    float? DensityThreshold = null,
    int CoarseStep = 2,
    int SmoothingIterations = 2,
    double KeepPieces = 0.05);

/// <summary>What a reconstruction produced.</summary>
/// <param name="Mesh">The model.</param>
/// <param name="Input">What the model was shown: the object framed on grey, or the photo the relief was built from.</param>
/// <param name="Timings">Each step's duration, for the log.</param>
public sealed record ReconstructionResult(Mesh Mesh, RgbaImage Input, IReadOnlyList<(ReconstructionStage Stage, TimeSpan Took)> Timings);

/// <summary>
/// A full 3D object from one photo with TripoSR (Stability AI and Tripo AI, MIT): the object is
/// cut out, framed and laid on grey as run.py does; the encoder turns it into a triplane; the
/// decoder's density is sampled on a grid and marching cubes draws the surface at TripoSR's
/// threshold; the decoder then colours each vertex.
/// </summary>
public static class ObjectReconstruction
{
    /// <summary>TripoSR's encoder.</summary>
    public const string Encoder = "triposr-encoder";

    /// <summary>TripoSR's decoder.</summary>
    public const string Decoder = "triposr-decoder";

    /// <summary>Every model the operation needs.</summary>
    public static readonly IReadOnlyList<string> Models = [Encoder, Decoder, BackgroundRemoval.Model];

    /// <summary>
    /// TripoSR's object space to glTF's: its up is +Z and the photo was taken from +X, so
    /// (x, y, z) becomes (y, z, x) and the side the photo shows faces +Z, the viewer's front.
    /// </summary>
    public static readonly Matrix4x4 ToYUp = new(
        0, 0, 1, 0,
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 0, 1);

    /// <summary>Reconstructs the object in a photo.</summary>
    /// <param name="runner">The sessions.</param>
    /// <param name="photo">The photo: one object, ideally whole and in the middle.</param>
    /// <param name="options">How.</param>
    /// <param name="progress">Told each step's progress.</param>
    /// <param name="cancellation">Checked between steps and decoder batches.</param>
    /// <returns>The mesh, Y up, the photo's view at the front (+Z), with vertex colours.</returns>
    public static ReconstructionResult Run(ModelRunner runner, RgbaImage photo, ObjectOptions options, IProgress<ReconstructionProgress>? progress = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(photo);
        ArgumentNullException.ThrowIfNull(options);
        List<(ReconstructionStage, TimeSpan)> timings = [];
        Stopwatch clock = Stopwatch.StartNew();

        ModelMeta encoderMeta = runner.Library.Meta(Encoder);
        ModelMeta decoderMeta = runner.Library.Meta(Decoder);
        int side = encoderMeta.Preprocess.Size ?? 512;
        float radius = (float)decoderMeta.Param("radius");
        float threshold = options.DensityThreshold ?? (float)decoderMeta.Param("density_threshold");
        int chunk = decoderMeta.Params.TryGetValue("chunk", out double c) ? (int)c : 262144;

        // 1. The object, cut out and framed as TripoSR saw its training renders.
        // A photo that already has transparency (a PNG cut out elsewhere) is trusted as it is.
        RgbaImage cutout = photo;
        if (options.RemoveBackground && !photo.HasTransparency)
        {
            progress?.Report(new(ReconstructionStage.RemovingBackground, 0));
            cutout = BackgroundRemoval.CutOut(runner, ImageOps.Fit(photo, 2048));
            Mark(ReconstructionStage.RemovingBackground);
        }

        RgbaImage framed = cutout.HasTransparency ? ImageOps.FrameObject(cutout, options.ForegroundRatio) : cutout;
        RgbaImage input = ImageOps.Resize(ImageOps.Composite(framed, encoderMeta.Preprocess.Background ?? 0.5f), side, side);
        cancellation.ThrowIfCancellationRequested();

        // 2. The triplane.
        progress?.Report(new(ReconstructionStage.Encoding, 0));
        float[] triplane;
        long[] triplaneShape = [.. decoderMeta.Inputs[0].Shape.Select(d => (long)d)];
        using (OrtValue image = OrtValue.CreateTensorValueFromMemory(ImageOps.ToPlanarRgb(input), [1, 3, side, side]))
        using (IDisposableReadOnlyCollection<OrtValue> encoded = runner.Run(Encoder, new Dictionary<string, OrtValue> { [encoderMeta.Inputs[0].Name] = image }, [encoderMeta.Outputs[0].Name]))
        {
            triplane = encoded[0].GetTensorDataAsSpan<float>().ToArray();
        }

        Mark(ReconstructionStage.Encoding);
        cancellation.ThrowIfCancellationRequested();

        using OrtValue planes = OrtValue.CreateTensorValueFromMemory(triplane, triplaneShape);
        string pointsName = decoderMeta.Inputs[1].Name, densityName = decoderMeta.Outputs[0].Name, colourName = decoderMeta.Outputs[1].Name;

        // 3. Density on the grid, refined where the surface is.
        ScalarGrid grid = ScalarGrid.Cube(options.Resolution, radius);
        IProgress<double>? sampling = progress is null ? null : new Relay(f => progress.Report(new(ReconstructionStage.SamplingDensity, f)));
        FieldSampler.Sample(grid, Density, threshold, options.CoarseStep, chunk, sampling, cancellation);
        Mark(ReconstructionStage.SamplingDensity);

        // 4. The surface.
        progress?.Report(new(ReconstructionStage.ExtractingSurface, 0));
        Mesh mesh = MarchingCubes.Extract(grid, threshold);
        if (mesh.TriangleCount == 0)
        {
            throw new InvalidOperationException("TripoSR found no object in this photo. Try a photo of a single object, whole and centred, against a plain background.");
        }

        mesh = MeshCleanup.RemoveSmallPieces(mesh, options.KeepPieces);
        mesh = MeshCleanup.Smooth(mesh, options.SmoothingIterations);
        Mark(ReconstructionStage.ExtractingSurface);
        cancellation.ThrowIfCancellationRequested();

        // 5. A colour per vertex, from the decoder at the vertex.
        progress?.Report(new(ReconstructionStage.Colouring, 0));
        Vector3[] colours = new Vector3[mesh.VertexCount];
        float[] points = new float[Math.Min(chunk, mesh.VertexCount) * 3];
        for (int start = 0; start < mesh.VertexCount; start += chunk)
        {
            int count = Math.Min(chunk, mesh.VertexCount - start);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = mesh.Positions[start + i];
                points[i * 3] = p.X;
                points[(i * 3) + 1] = p.Y;
                points[(i * 3) + 2] = p.Z;
            }

            using OrtValue batch = OrtValue.CreateTensorValueFromMemory(OrtMemoryInfo.DefaultInstance, points.AsMemory(0, count * 3), [count, 3]);
            using IDisposableReadOnlyCollection<OrtValue> decoded = runner.Run(Decoder, new Dictionary<string, OrtValue> { [decoderMeta.Inputs[0].Name] = planes, [pointsName] = batch }, [colourName]);
            ReadOnlySpan<float> rgb = decoded[0].GetTensorDataAsSpan<float>();
            for (int i = 0; i < count; i++)
            {
                colours[start + i] = new Vector3(rgb[i * 3], rgb[(i * 3) + 1], rgb[(i * 3) + 2]);
            }

            progress?.Report(new(ReconstructionStage.Colouring, (double)(start + count) / mesh.VertexCount));
        }

        mesh = new Mesh(mesh.Positions, mesh.Triangles, colours).Transformed(ToYUp);
        Mark(ReconstructionStage.Colouring);
        return new ReconstructionResult(mesh, input, timings);

        void Density(ReadOnlySpan<float> batchPoints, Span<float> values)
        {
            float[] copy = batchPoints.ToArray();
            using OrtValue batch = OrtValue.CreateTensorValueFromMemory(copy, [copy.Length / 3, 3]);
            using IDisposableReadOnlyCollection<OrtValue> decoded = runner.Run(Decoder, new Dictionary<string, OrtValue> { [decoderMeta.Inputs[0].Name] = planes, [pointsName] = batch }, [densityName]);
            decoded[0].GetTensorDataAsSpan<float>()[..values.Length].CopyTo(values);
        }

        void Mark(ReconstructionStage stage)
        {
            timings.Add((stage, clock.Elapsed));
            progress?.Report(new(stage, 1));
            clock.Restart();
        }
    }

    /// <summary>An IProgress that calls back on the reporting thread, unlike Progress&lt;T&gt;.</summary>
    internal sealed class Relay(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
