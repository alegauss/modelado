using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Inference.Pipelines;

/// <summary>How a photo becomes a relief.</summary>
/// <param name="Relief">The mesh's shape: grid, depth, base.</param>
/// <param name="RemoveBackground">Keep only the object U-2-Net finds, instead of the whole picture.</param>
/// <param name="TextureSide">The longest side of the texture the mesh carries.</param>
public sealed record ReliefRunOptions(ReliefOptions Relief, bool RemoveBackground = false, int TextureSide = 2048);

/// <summary>
/// A relief from one photo with Depth Anything V2 Small (Apache-2.0): the depth of every pixel,
/// relative, becomes the height of a grid over the photo, which textures it. Works on any
/// picture (a face, a landscape, a building); the back is flat or open.
/// </summary>
public static class ReliefReconstruction
{
    /// <summary>The depth model.</summary>
    public const string Depth = "depth-anything-v2-small";

    /// <summary>The models the operation needs; U-2-Net only to cut the object out.</summary>
    /// <param name="removeBackground">Whether the background is removed.</param>
    /// <returns>The models' folder names.</returns>
    public static IReadOnlyList<string> Models(bool removeBackground) => removeBackground ? [Depth, BackgroundRemoval.Model] : [Depth];

    /// <summary>The relative inverse depth of a photo (larger is nearer), at the model's resolution.</summary>
    /// <param name="runner">The sessions.</param>
    /// <param name="photo">The photo, stretched to the model's square; the relief stretches the depth back.</param>
    /// <returns>The depth and its size.</returns>
    public static (float[] Depth, int Width, int Height) EstimateDepth(ModelRunner runner, RgbaImage photo)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(photo);
        ModelMeta meta = runner.Library.Meta(Depth);
        int side = meta.Preprocess.Size ?? 518;
        RgbaImage input = ImageOps.Resize(photo, side, side);
        using OrtValue tensor = OrtValue.CreateTensorValueFromMemory(ImageOps.ToPlanarRgb(input), [1, 3, side, side]);
        using IDisposableReadOnlyCollection<OrtValue> results = runner.Run(Depth, new Dictionary<string, OrtValue> { [meta.Inputs[0].Name] = tensor }, [meta.Outputs[0].Name]);
        return (results[0].GetTensorDataAsSpan<float>()[..(side * side)].ToArray(), side, side);
    }

    /// <summary>Builds the relief.</summary>
    /// <param name="runner">The sessions.</param>
    /// <param name="photo">The photo.</param>
    /// <param name="options">How.</param>
    /// <param name="progress">Told each step's progress.</param>
    /// <param name="cancellation">Checked between steps.</param>
    /// <returns>The relief, textured with the photo.</returns>
    public static ReconstructionResult Run(ModelRunner runner, RgbaImage photo, ReliefRunOptions options, IProgress<ReconstructionProgress>? progress = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(photo);
        ArgumentNullException.ThrowIfNull(options);
        List<(ReconstructionStage, TimeSpan)> timings = [];
        Stopwatch clock = Stopwatch.StartNew();
        RgbaImage texture = ImageOps.Fit(photo, options.TextureSide);

        float[]? mask = null;
        progress?.Report(new(ReconstructionStage.EstimatingDepth, 0));
        (float[] depth, int width, int height) = EstimateDepth(runner, photo);
        Mark(ReconstructionStage.EstimatingDepth);
        cancellation.ThrowIfCancellationRequested();

        if (options.RemoveBackground)
        {
            progress?.Report(new(ReconstructionStage.RemovingBackground, 0));
            float[] full = photo.HasTransparency ? AlphaOf(photo) : BackgroundRemoval.Mask(runner, photo);
            mask = ImageOps.ResizeMask(full, photo.Width, photo.Height, width, height);
            Mark(ReconstructionStage.RemovingBackground);
            cancellation.ThrowIfCancellationRequested();
        }

        progress?.Report(new(ReconstructionStage.BuildingRelief, 0));
        Mesh mesh = Relief.Build(depth, width, height, texture, mask, options.Relief);
        Mark(ReconstructionStage.BuildingRelief);
        return new ReconstructionResult(mesh, texture, timings);

        void Mark(ReconstructionStage stage)
        {
            timings.Add((stage, clock.Elapsed));
            progress?.Report(new(stage, 1));
            clock.Restart();
        }
    }

    private static float[] AlphaOf(RgbaImage image)
    {
        float[] alpha = new float[image.Width * image.Height];
        for (int i = 0; i < alpha.Length; i++)
        {
            alpha[i] = image.Pixels[(i * 4) + 3] / 255f;
        }

        return alpha;
    }
}
