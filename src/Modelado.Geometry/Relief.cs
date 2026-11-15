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
/// <param name="PrintWidth">
/// MOD102: the relief's width in millimetres, for printing. Given, the relief is built that wide (the
/// part a mask keeps, where there is one), and <paramref name="Depth"/> and
/// <paramref name="BaseThickness"/> are millimetres too, so a relief made 60 mm wide with 3 mm of
/// depth on a 2 mm base measures exactly that. Null keeps the shares of a 1-unit width.
/// </param>
/// <param name="BorderWidth">
/// MOD105: with <paramref name="PrintWidth"/>, a raised border this many millimetres wide along the
/// relief's outline (the photo's edge, or the crop's), a frame for a plaque or a medallion; 0 for none.
/// </param>
/// <param name="BorderHeight">MOD105: the border's top, in millimetres above the relief's lowest point.</param>
public sealed record ReliefOptions(
    int GridLongSide = 256,
    float Depth = 0.25f,
    float BaseThickness = 0f,
    int SmoothPasses = 1,
    float MaskThreshold = 0.5f,
    float? PrintWidth = null,
    float BorderWidth = 0f,
    float BorderHeight = 0f);

/// <summary>
/// A relief (2.5D) model from a photo and its depth: a grid over the image whose height is the
/// depth, textured with the photo. With a mask only the object stands; with a base it is closed
/// into a solid, its back flat.
/// </summary>
public static class Relief
{
    /// <summary>MOD102: the printed width a relief sized in millimetres starts from, in the window and the CLI.</summary>
    public const float PrintWidthDefault = 60f;

    /// <summary>MOD102: its depth in millimetres, unless one is given.</summary>
    public const float PrintDepthDefault = 3f;

    /// <summary>MOD102: its base in millimetres, unless one is given; a printed relief stands on one.</summary>
    public const float PrintBaseDefault = 2f;

    /// <summary>Builds the relief.</summary>
    /// <param name="depth">Relative inverse depth (larger is nearer), <paramref name="width"/> × <paramref name="height"/>.</param>
    /// <param name="width">The depth map's width.</param>
    /// <param name="height">The depth map's height.</param>
    /// <param name="photo">The photo: the texture, the vertex colours and the relief's aspect.</param>
    /// <param name="mask">Opacity in [0, 1] per depth pixel, or null to keep the whole image.</param>
    /// <param name="options">How.</param>
    /// <returns>
    /// The relief, 1 unit wide (or <see cref="ReliefOptions.PrintWidth"/> millimetres), centred on X and
    /// Y, the nearest point at the front (+Z).
    /// </returns>
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
        float[]? border = Border(gridMask, gw, gh, options);
        Vector3[] positions = new Vector3[gw * gh];
        Vector2[] uvs = new Vector2[gw * gh];
        Vector3[] colors = new Vector3[gw * gh];
        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                int i = (y * gw) + x;
                float u = (float)x / (gw - 1), v = (float)y / (gh - 1);
                float z = border is not null && border[i] >= 0 ? border[i] : grid[i] * zScale;
                positions[i] = new Vector3(u - 0.5f, (0.5f - v) * aspect, z);
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
        surface = Widened(surface, options.PrintWidth);
        return options.BaseThickness > 0 ? Solidify(surface, -options.BaseThickness) : surface;
    }

    /// <summary>
    /// MOD105: the raised border's height at each grid vertex within the border's width of the
    /// outline, and -1 elsewhere. The distance to the outline is a two-pass chamfer transform (1 along
    /// a row, √2 on a diagonal) over the grid padded with a ring that counts as outside, so the
    /// photo's own edge is outline as much as a crop's. Millimetres per cell come from the kept
    /// width, which <see cref="ReliefOptions.PrintWidth"/> becomes.
    /// </summary>
    private static float[]? Border(float[]? mask, int gw, int gh, ReliefOptions options)
    {
        if (options.PrintWidth is not { } printWidth || options.BorderWidth <= 0)
        {
            return null;
        }

        int pw = gw + 2, ph = gh + 2;
        float[] distance = new float[pw * ph];
        int minColumn = gw, maxColumn = -1;
        for (int y = 0; y < ph; y++)
        {
            for (int x = 0; x < pw; x++)
            {
                bool kept = x > 0 && y > 0 && x <= gw && y <= gh
                    && (mask is null || mask[((y - 1) * gw) + x - 1] >= options.MaskThreshold);
                distance[(y * pw) + x] = kept ? float.MaxValue : 0;
                if (kept)
                {
                    (minColumn, maxColumn) = (Math.Min(minColumn, x - 1), Math.Max(maxColumn, x - 1));
                }
            }
        }

        Chamfer(distance, pw, ph);
        float cellsPerMm = Math.Max(1, maxColumn - minColumn) / printWidth;
        float reach = options.BorderWidth * cellsPerMm;
        float[] border = new float[gw * gh];
        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                // A kept vertex next to the outline is 1 cell from the first outside one.
                float d = distance[((y + 1) * pw) + x + 1];
                border[(y * gw) + x] = d < float.MaxValue && d - 1 <= reach ? options.BorderHeight : -1;
            }
        }

        return border;
    }

    // Forward then backward over the grid, each cell taking the least of its neighbours' distances
    // plus the step to them.
    private static void Chamfer(float[] d, int w, int h)
    {
        const float diagonal = 1.41421356f;
        for (int y = 1; y < h; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int i = (y * w) + x;
                d[i] = MathF.Min(d[i], MathF.Min(MathF.Min(d[i - 1] + 1, d[i - w] + 1), MathF.Min(d[i - w - 1] + diagonal, d[i - w + 1] + diagonal)));
            }
        }

        for (int y = h - 2; y >= 0; y--)
        {
            for (int x = w - 2; x >= 1; x--)
            {
                int i = (y * w) + x;
                d[i] = MathF.Min(d[i], MathF.Min(MathF.Min(d[i + 1] + 1, d[i + w] + 1), MathF.Min(d[i + w + 1] + diagonal, d[i + w - 1] + diagonal)));
            }
        }
    }

    // MOD102: the depth is already in millimetres (zScale); the width is what the mask kept, made the
    // width asked for, X and Y alike so the photo keeps its aspect.
    private static Mesh Widened(Mesh surface, float? printWidth)
    {
        if (printWidth is not { } width)
        {
            return surface;
        }

        (Vector3 min, Vector3 max) = surface.Bounds();
        float kept = max.X - min.X;
        return kept > 0 ? surface.Transformed(Matrix4x4.CreateScale(width / kept, width / kept, 1)) : surface;
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
