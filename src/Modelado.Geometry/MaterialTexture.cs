using System.Numerics;
using Modelado.Imaging;

namespace Modelado.Geometry;

/// <summary>
/// A material image put on a model (MOD40): wood, stone or painted metal, drawn from a prompt or
/// opened from a file, replacing what the model's surface showed. The image is made to tile first,
/// so its repeats meet without a line. A model with texture coordinates (a relief, a textured GLB
/// or OBJ) gets the tile repeated into a new texture; one without (TripoSR's vertex colours) gets
/// a triplanar projection baked into its colours, which blends the three axes' projections by the
/// surface's facing and so has no seam where one axis gives way to the next.
/// </summary>
public static class MaterialTexture
{
    /// <summary>
    /// The image made to tile: inside a border it is unchanged, and towards each edge it blends into
    /// the same image offset by half its size, whose pixels at opposite edges were neighbours. The
    /// last column then continues into the first, and the last row into the first.
    /// </summary>
    /// <param name="image">The material image.</param>
    /// <param name="border">The share of each side, from the edge, that is blended.</param>
    /// <returns>A tileable copy.</returns>
    public static RgbaImage Seamless(RgbaImage image, float border = 0.2f)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(border);
        int w = image.Width, h = image.Height;
        RgbaImage result = new(w, h);
        for (int y = 0; y < h; y++)
        {
            float wy = EdgeWeight((y + 0.5f) / h, border);
            for (int x = 0; x < w; x++)
            {
                float weight = MathF.Max(EdgeWeight((x + 0.5f) / w, border), wy);
                int own = image.OffsetOf(x, y);
                int shifted = image.OffsetOf((x + (w / 2)) % w, (y + (h / 2)) % h);
                int to = result.OffsetOf(x, y);
                for (int c = 0; c < 3; c++)
                {
                    result.Pixels[to + c] = (byte)MathF.Round((image.Pixels[own + c] * (1 - weight)) + (image.Pixels[shifted + c] * weight));
                }

                result.Pixels[to + 3] = 255;
            }
        }

        return result;
    }

    /// <summary>The model with the material on it, as a new mesh; the positions and triangles are kept.</summary>
    /// <param name="mesh">The model.</param>
    /// <param name="tile">A tileable material image, as <see cref="Seamless"/> makes it.</param>
    /// <param name="tilesAcross">How many times the tile repeats across the model's longest side.</param>
    /// <returns>The retextured mesh.</returns>
    public static Mesh Apply(Mesh mesh, RgbaImage tile, float tilesAcross = 2)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tilesAcross);
        return mesh.TexCoords is null ? Triplanar(mesh, tile, tilesAcross) : Repeated(mesh, tile, tilesAcross);
    }

    // The texture becomes the tile repeated so its longest side holds tilesAcross of them, at most
    // 4096 px; the coordinates stay, and the colours are read again from the new texture.
    private static Mesh Repeated(Mesh mesh, RgbaImage tile, float tilesAcross)
    {
        RgbaImage old = mesh.Texture!;
        float aspect = (float)old.Width / old.Height;
        int across = Math.Max(1, (int)MathF.Round(tilesAcross));
        int width = Math.Min(4096, tile.Width * across);
        int height = Math.Max(1, (int)MathF.Round(width / aspect));
        float repeatX = across, repeatY = across / aspect;
        RgbaImage texture = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                (float r, float g, float b) = Wrapped(tile, (x + 0.5f) / width * repeatX, (y + 0.5f) / height * repeatY);
                Write(texture, x, y, r, g, b);
            }
        }

        Vector3[] colors = new Vector3[mesh.VertexCount];
        for (int i = 0; i < colors.Length; i++)
        {
            (float r, float g, float b) = texture.Sample(mesh.TexCoords![i].X, mesh.TexCoords[i].Y);
            colors[i] = new Vector3(r, g, b);
        }

        return new Mesh(mesh.Positions, mesh.Triangles, colors, mesh.TexCoords, texture, mesh.DoubleSided);
    }

    // Each vertex reads the tile from the three planes through it, weighted by how squarely its
    // normal faces each axis; sharpening the weights keeps a face from looking smeared.
    private static Mesh Triplanar(Mesh mesh, RgbaImage tile, float tilesAcross)
    {
        (Vector3 min, Vector3 max) = mesh.Bounds();
        float longest = MathF.Max(max.X - min.X, MathF.Max(max.Y - min.Y, max.Z - min.Z));
        float scale = longest > 0 ? tilesAcross / longest : 1;
        Vector3[] normals = mesh.ComputeNormals();
        Vector3[] colors = new Vector3[mesh.VertexCount];
        for (int i = 0; i < colors.Length; i++)
        {
            Vector3 p = (mesh.Positions[i] - min) * scale;
            Vector3 n = Vector3.Abs(normals[i]);
            Vector3 weights = n * n * n * n;
            float sum = weights.X + weights.Y + weights.Z;
            weights = sum > 0 ? weights / sum : new Vector3(1 / 3f);
            Vector3 fromX = Colour(Wrapped(tile, p.Z, -p.Y));
            Vector3 fromY = Colour(Wrapped(tile, p.X, p.Z));
            Vector3 fromZ = Colour(Wrapped(tile, p.X, -p.Y));
            colors[i] = (fromX * weights.X) + (fromY * weights.Y) + (fromZ * weights.Z);
        }

        return new Mesh(mesh.Positions, mesh.Triangles, colors, null, null, mesh.DoubleSided);
    }

    private static float EdgeWeight(float t, float border)
    {
        float fromEdge = MathF.Min(t, 1 - t);
        return fromEdge >= border ? 0 : 1 - (fromEdge / border);
    }

    // The tile sampled with repeat, as a tiling texture is: whole tiles are dropped first.
    private static (float R, float G, float B) Wrapped(RgbaImage tile, float u, float v) =>
        tile.Sample(u - MathF.Floor(u), v - MathF.Floor(v));

    private static Vector3 Colour((float R, float G, float B) c) => new(c.R, c.G, c.B);

    private static void Write(RgbaImage image, int x, int y, float r, float g, float b)
    {
        int i = image.OffsetOf(x, y);
        image.Pixels[i] = (byte)MathF.Round(Math.Clamp(r, 0, 1) * 255);
        image.Pixels[i + 1] = (byte)MathF.Round(Math.Clamp(g, 0, 1) * 255);
        image.Pixels[i + 2] = (byte)MathF.Round(Math.Clamp(b, 0, 1) * 255);
        image.Pixels[i + 3] = 255;
    }
}
