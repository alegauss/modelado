using Microsoft.ML.OnnxRuntime;
using Modelado.Imaging;

namespace Modelado.Inference.Pipelines;

/// <summary>
/// Real-ESRGAN x4plus (BSD-3-Clause), the upscaler PhotoBoost uses: a small or soft photo before
/// reconstruction, and a mesh's texture afterwards. The image is run in overlapping tiles, each
/// keeping only its middle, so the memory needed does not grow with the image and no seam shows.
/// </summary>
public static class Upscaling
{
    /// <summary>The model's folder name.</summary>
    public const string Model = "realesrgan-x4plus";

    /// <summary>The largest side a result is kept at; a larger one is scaled down to it.</summary>
    public const int MaxSide = 4096;

    // Context each tile reads beyond what it keeps, on every side, in input pixels.
    private const int Overlap = 12;

    /// <summary>Upscales an image, up to <paramref name="maxSide"/> on its longer side.</summary>
    /// <param name="runner">The sessions.</param>
    /// <param name="image">The image; its alpha is scaled along, not by the model.</param>
    /// <param name="maxSide">The longest side of the result.</param>
    /// <param name="progress">Told the share of the tiles done.</param>
    /// <param name="cancellation">Checked between tiles.</param>
    /// <returns>The image 4× larger, or scaled to <paramref name="maxSide"/>; the image itself when it is already that large.</returns>
    public static RgbaImage Upscale(ModelRunner runner, RgbaImage image, int maxSide = MaxSide, IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(image);
        int longest = Math.Max(image.Width, image.Height);
        if (longest >= maxSide)
        {
            return image;
        }

        ModelMeta meta = runner.Library.Meta(Model);
        int scale = (int)meta.Param("scale");
        int tile = meta.Params.TryGetValue("tile", out double t) ? (int)t : 256;

        // Only as much input as the result needs: a 2048 px texture is not taken to 8192 to be
        // scaled back to 4096 when its 1024 px reduction gives the same.
        RgbaImage source = longest * scale > maxSide * 2 ? ImageOps.Fit(image, (maxSide + scale - 1) / scale) : image;
        int width = source.Width, height = source.Height;
        RgbaImage result = ImageOps.Resize(source, width * scale, height * scale); // alpha, and a fallback for nothing
        int tilesX = (width + tile - 1) / tile, tilesY = (height + tile - 1) / tile;
        string input = meta.Inputs[0].Name, output = meta.Outputs[0].Name;

        for (int ty = 0; ty < tilesY; ty++)
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                cancellation.ThrowIfCancellationRequested();
                int keepX = tx * tile, keepY = ty * tile;
                int keepW = Math.Min(tile, width - keepX), keepH = Math.Min(tile, height - keepY);
                int readX = Math.Max(0, keepX - Overlap), readY = Math.Max(0, keepY - Overlap);
                int readW = Math.Min(width, keepX + keepW + Overlap) - readX, readH = Math.Min(height, keepY + keepH + Overlap) - readY;

                float[] tensor = Planar(source, readX, readY, readW, readH);
                using OrtValue value = OrtValue.CreateTensorValueFromMemory(tensor, [1, 3, readH, readW]);
                using IDisposableReadOnlyCollection<OrtValue> outputs = runner.Run(Model, new Dictionary<string, OrtValue> { [input] = value }, [output]);
                ReadOnlySpan<float> up = outputs[0].GetTensorDataAsSpan<float>();
                int upW = readW * scale, plane = upW * readH * scale;
                for (int y = 0; y < keepH * scale; y++)
                {
                    int sy = ((keepY - readY) * scale) + y;
                    for (int x = 0; x < keepW * scale; x++)
                    {
                        int sx = ((keepX - readX) * scale) + x;
                        int o = result.OffsetOf((keepX * scale) + x, (keepY * scale) + y);
                        int s = (sy * upW) + sx;
                        result.Pixels[o] = ToByte(up[s]);
                        result.Pixels[o + 1] = ToByte(up[plane + s]);
                        result.Pixels[o + 2] = ToByte(up[(2 * plane) + s]);
                    }
                }

                progress?.Report((double)((ty * tilesX) + tx + 1) / (tilesX * tilesY));
            }
        }

        return Math.Max(result.Width, result.Height) > maxSide ? ImageOps.Fit(result, maxSide) : result;
    }

    /// <summary>A textured mesh with its texture upscaled; the texture coordinates stay as they are.</summary>
    /// <param name="runner">The sessions.</param>
    /// <param name="mesh">A mesh with a texture.</param>
    /// <param name="progress">Told the share of the tiles done.</param>
    /// <param name="cancellation">Checked between tiles.</param>
    /// <returns>The mesh with the sharper texture.</returns>
    /// <exception cref="InvalidOperationException">The mesh has no texture.</exception>
    public static Geometry.Mesh UpscaleTexture(ModelRunner runner, Geometry.Mesh mesh, IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        RgbaImage texture = mesh.Texture ?? throw new InvalidOperationException("This model has no texture to improve; its colours are per vertex.");
        RgbaImage sharper = Upscale(runner, texture, MaxSide, progress, cancellation);
        return new Geometry.Mesh(mesh.Positions, mesh.Triangles, mesh.Colors, mesh.TexCoords, sharper, mesh.DoubleSided);
    }

    private static float[] Planar(RgbaImage image, int left, int top, int width, int height)
    {
        int plane = width * height;
        float[] tensor = new float[plane * 3];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = image.OffsetOf(left + x, top + y), p = (y * width) + x;
                tensor[p] = image.Pixels[i] / 255f;
                tensor[plane + p] = image.Pixels[i + 1] / 255f;
                tensor[(2 * plane) + p] = image.Pixels[i + 2] / 255f;
            }
        }

        return tensor;
    }

    private static byte ToByte(float v) => (byte)Math.Clamp(MathF.Round(v * 255f), 0, 255);
}
