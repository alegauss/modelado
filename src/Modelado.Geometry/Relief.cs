using System.Numerics;
using Modelado.Imaging;

namespace Modelado.Geometry;

/// <summary>How a depth map becomes a relief.</summary>
/// <param name="GridLongSide">Vertices along the image's longer side; the shorter side follows its aspect.</param>
/// <param name="Depth">How far the nearest point stands out, as a share of the relief's width.</param>
/// <param name="BaseThickness">
/// The solid backing behind the deepest point, as a share of the width; 0 leaves an open surface
/// (seen from both sides), anything larger closes it into a solid a printer accepts.
/// </param>
/// <param name="SmoothPasses">Box-blur passes over the depth before it becomes geometry, against speckle.</param>
/// <param name="MaskThreshold">With a mask, the share of opacity a pixel needs to be part of the relief.</param>
public sealed record ReliefOptions(
    int GridLongSide = 256,
    float Depth = 0.25f,
    float BaseThickness = 0f,
    int SmoothPasses = 1,
    float MaskThreshold = 0.5f);

/// <summary>
/// A relief (2.5D) model from a photo and its depth: a grid over the image whose height is the
/// depth, textured with the photo. With a mask only the object stands; with a base it is closed
/// into a solid, its back flat.
/// </summary>
public static class Relief
{
    /// <summary>Builds the relief.</summary>
    /// <param name="depth">Relative inverse depth (larger is nearer), <paramref name="width"/> × <paramref name="height"/>.</param>
    /// <param name="width">The depth map's width.</param>
    /// <param name="height">The depth map's height.</param>
    /// <param name="photo">The photo: the texture, the vertex colours and the relief's aspect.</param>
    /// <param name="mask">Opacity in [0, 1] per depth pixel, or null to keep the whole image.</param>
    /// <param name="options">How.</param>
    /// <returns>The relief, 1 unit wide, centred on X and Y, the nearest point at the front (+Z).</returns>
    /// <exception cref="InvalidOperationException">The mask keeps nothing.</exception>
    public static Mesh Build(ReadOnlySpan<float> depth, int width, int height, RgbaImage photo, ReadOnlySpan<float> mask, ReliefOptions options)
    {
        ArgumentNullException.ThrowIfNull(photo);
        ArgumentNullException.ThrowIfNull(options);
        if (depth.Length != width * height)
        {
            throw new ArgumentException("The depth map is not width × height.", nameof(depth));
        }

        bool masked = !mask.IsEmpty;
        if (masked && mask.Length != depth.Length)
        {
            throw new ArgumentException("The mask is not one value per depth pixel.", nameof(mask));
        }

        // The grid takes the photo's aspect: the depth may come from a square the photo was stretched to.
        int pw = photo.Width, ph = photo.Height;
        int gw = pw >= ph ? options.GridLongSide : Math.Max(2, (int)Math.Round(options.GridLongSide * (double)pw / ph));
        int gh = ph > pw ? options.GridLongSide : Math.Max(2, (int)Math.Round(options.GridLongSide * (double)ph / pw));
        float[] grid = ImageOps.ResizeMask(depth, width, height, gw, gh);
        float[]? gridMask = masked ? ImageOps.ResizeMask(mask, width, height, gw, gh) : null;
        for (int pass = 0; pass < options.SmoothPasses; pass++)
        {
            grid = BoxBlur(grid, gw, gh);
        }

        Normalise(grid, gridMask, options.MaskThreshold);

        float aspect = (float)gh / gw;
        float zScale = options.Depth;
        Vector3[] positions = new Vector3[gw * gh];
        Vector2[] uvs = new Vector2[gw * gh];
        Vector3[] colors = new Vector3[gw * gh];
        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                int i = (y * gw) + x;
                float u = (float)x / (gw - 1), v = (float)y / (gh - 1);
                positions[i] = new Vector3(u - 0.5f, (0.5f - v) * aspect, grid[i] * zScale);
                uvs[i] = new Vector2(u, v);
                (float r, float g, float b) = photo.Sample(u, v);
                colors[i] = new Vector3(r, g, b);
            }
        }

        List<int> triangles = new((gw - 1) * (gh - 1) * 6);
        for (int y = 0; y < gh - 1; y++)
        {
            for (int x = 0; x < gw - 1; x++)
            {
                int a = (y * gw) + x, b = a + 1, c = a + gw, d = c + 1;
                // Counter-clockwise seen from +Z: image rows run down, Y runs up.
                AddIfInside(triangles, gridMask, options.MaskThreshold, a, c, b);
                AddIfInside(triangles, gridMask, options.MaskThreshold, b, c, d);
            }
        }

        if (triangles.Count == 0)
        {
            throw new InvalidOperationException("The mask leaves nothing of the image to build a relief from.");
        }

        Mesh surface = MeshCleanup.Compact(new Mesh(positions, [.. triangles], colors, uvs, photo, doubleSided: true), [.. triangles]);
        return options.BaseThickness > 0 ? Solidify(surface, -options.BaseThickness) : surface;
    }

    /// <summary>
    /// Closes an open surface into a solid: a flat back at <paramref name="backZ"/> and a wall
    /// along every border edge, wound so every face points outward.
    /// </summary>
    /// <param name="surface">An open surface facing +Z.</param>
    /// <param name="backZ">Where the back lies, behind every vertex.</param>
    /// <returns>The solid, single-sided.</returns>
    public static Mesh Solidify(Mesh surface, float backZ)
    {
        ArgumentNullException.ThrowIfNull(surface);
        int n = surface.VertexCount;
        int[] t = surface.Triangles;

        // A border edge is used by one triangle only; directed as that triangle winds it.
        Dictionary<(int, int), int> uses = [];
        for (int i = 0; i < t.Length; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int a = t[i + k], b = t[i + ((k + 1) % 3)];
                (int, int) key = a < b ? (a, b) : (b, a);
                uses[key] = uses.GetValueOrDefault(key) + 1;
            }
        }

        List<int> triangles = new(t.Length * 2);
        triangles.AddRange(t);
        for (int i = 0; i < t.Length; i += 3)
        {
            // The back is the front mirrored onto the plane, wound the other way.
            triangles.Add(t[i] + n);
            triangles.Add(t[i + 2] + n);
            triangles.Add(t[i + 1] + n);
            for (int k = 0; k < 3; k++)
            {
                int a = t[i + k], b = t[i + ((k + 1) % 3)];
                if (uses[a < b ? (a, b) : (b, a)] == 1)
                {
                    // The wall runs b to a along the front, as a neighbouring triangle would.
                    triangles.AddRange([b, a, a + n, b, a + n, b + n]);
                }
            }
        }

        Vector3[] positions = new Vector3[n * 2];
        surface.Positions.CopyTo(positions, 0);
        for (int i = 0; i < n; i++)
        {
            positions[n + i] = surface.Positions[i] with { Z = backZ };
        }

        Vector3[]? colors = Doubled(surface.Colors);
        Vector2[]? uvs = Doubled(surface.TexCoords);
        return new Mesh(positions, [.. triangles], colors, uvs, surface.Texture, doubleSided: false);
    }

    private static T[]? Doubled<T>(T[]? values)
    {
        if (values is null)
        {
            return null;
        }

        T[] result = new T[values.Length * 2];
        values.CopyTo(result, 0);
        values.CopyTo(result, values.Length);
        return result;
    }

    private static void AddIfInside(List<int> triangles, float[]? mask, float threshold, int a, int b, int c)
    {
        if (mask is not null && (mask[a] < threshold || mask[b] < threshold || mask[c] < threshold))
        {
            return;
        }

        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
    }

    // Scales the depth to [0, 1] between its 2nd and 98th percentiles (inside the mask), so a
    // stray pixel does not flatten everything else.
    private static void Normalise(float[] depth, float[]? mask, float threshold)
    {
        List<float> inside = new(depth.Length);
        for (int i = 0; i < depth.Length; i++)
        {
            if (mask is null || mask[i] >= threshold)
            {
                inside.Add(depth[i]);
            }
        }

        if (inside.Count == 0)
        {
            return;
        }

        inside.Sort();
        float low = inside[(int)((inside.Count - 1) * 0.02)];
        float high = inside[(int)((inside.Count - 1) * 0.98)];
        float range = MathF.Max(high - low, 1e-6f);
        for (int i = 0; i < depth.Length; i++)
        {
            depth[i] = Math.Clamp((depth[i] - low) / range, 0f, 1f);
        }
    }

    private static float[] BoxBlur(float[] values, int width, int height)
    {
        float[] result = new float[values.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float sum = 0;
                int count = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = y + dy;
                    if (yy < 0 || yy >= height)
                    {
                        continue;
                    }

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx;
                        if (xx < 0 || xx >= width)
                        {
                            continue;
                        }

                        sum += values[(yy * width) + xx];
                        count++;
                    }
                }

                result[(y * width) + x] = sum / count;
            }
        }

        return result;
    }
}
