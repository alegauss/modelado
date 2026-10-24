using System.Numerics;
using Modelado.Imaging;

namespace Modelado.Geometry;

/// <summary>
/// An indexed triangle mesh in glTF's convention: Y up, the front facing +Z, counter-clockwise
/// triangles seen from outside. Colour comes from <see cref="Colors"/> per vertex, from
/// <see cref="Texture"/> through <see cref="TexCoords"/>, or both (the relief keeps both, so a
/// format without textures still gets colour).
/// </summary>
public sealed class Mesh
{
    /// <summary>Creates a mesh.</summary>
    /// <param name="positions">The vertices.</param>
    /// <param name="triangles">Three vertex indices per triangle.</param>
    /// <param name="colors">RGB in [0, 1] per vertex, sRGB-encoded as images hold them, or null.</param>
    /// <param name="texCoords">(u, v) per vertex, v = 0 at the image's top, or null.</param>
    /// <param name="texture">The image <paramref name="texCoords"/> point into, or null.</param>
    /// <param name="doubleSided">True for an open surface whose back may be seen, such as a relief without a base.</param>
    public Mesh(Vector3[] positions, int[] triangles, Vector3[]? colors = null, Vector2[]? texCoords = null, RgbaImage? texture = null, bool doubleSided = false)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(triangles);
        if (triangles.Length % 3 != 0)
        {
            throw new ArgumentException("The triangle list is not a multiple of three.", nameof(triangles));
        }

        if (colors is not null && colors.Length != positions.Length)
        {
            throw new ArgumentException("There is not one colour per vertex.", nameof(colors));
        }

        if (texCoords is not null && texCoords.Length != positions.Length)
        {
            throw new ArgumentException("There is not one texture coordinate per vertex.", nameof(texCoords));
        }

        if ((texCoords is null) != (texture is null))
        {
            throw new ArgumentException("Texture coordinates and a texture come together.", nameof(texture));
        }

        Positions = positions;
        Triangles = triangles;
        Colors = colors;
        TexCoords = texCoords;
        Texture = texture;
        DoubleSided = doubleSided;
    }

    /// <summary>The vertices.</summary>
    public Vector3[] Positions { get; }

    /// <summary>Three vertex indices per triangle.</summary>
    public int[] Triangles { get; }

    /// <summary>RGB in [0, 1] per vertex, or null.</summary>
    public Vector3[]? Colors { get; }

    /// <summary>(u, v) per vertex, or null.</summary>
    public Vector2[]? TexCoords { get; }

    /// <summary>The texture, or null.</summary>
    public RgbaImage? Texture { get; }

    /// <summary>Whether both faces of each triangle are meant to be seen.</summary>
    public bool DoubleSided { get; }

    /// <summary>How many vertices.</summary>
    public int VertexCount => Positions.Length;

    /// <summary>How many triangles.</summary>
    public int TriangleCount => Triangles.Length / 3;

    /// <summary>The axis-aligned box around every vertex.</summary>
    /// <returns>Its corners; zero for an empty mesh.</returns>
    public (Vector3 Min, Vector3 Max) Bounds()
    {
        if (Positions.Length == 0)
        {
            return (Vector3.Zero, Vector3.Zero);
        }

        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        foreach (Vector3 p in Positions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return (min, max);
    }

    /// <summary>Per-vertex normals, each the area-weighted sum of its triangles' normals.</summary>
    /// <returns>One unit normal per vertex (zero for a vertex no triangle uses).</returns>
    public Vector3[] ComputeNormals()
    {
        Vector3[] normals = new Vector3[Positions.Length];
        for (int t = 0; t < Triangles.Length; t += 3)
        {
            int a = Triangles[t], b = Triangles[t + 1], c = Triangles[t + 2];
            Vector3 n = Vector3.Cross(Positions[b] - Positions[a], Positions[c] - Positions[a]);
            normals[a] += n;
            normals[b] += n;
            normals[c] += n;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            float length = normals[i].Length();
            normals[i] = length > 0 ? normals[i] / length : Vector3.Zero;
        }

        return normals;
    }

    /// <summary>The volume a closed mesh encloses; negative when its triangles face inward.</summary>
    /// <returns>The signed volume.</returns>
    public double SignedVolume()
    {
        double volume = 0;
        for (int t = 0; t < Triangles.Length; t += 3)
        {
            Vector3 a = Positions[Triangles[t]], b = Positions[Triangles[t + 1]], c = Positions[Triangles[t + 2]];
            volume += Vector3.Dot(a, Vector3.Cross(b, c));
        }

        return volume / 6.0;
    }

    /// <summary>The surface area.</summary>
    /// <returns>The sum of the triangles' areas.</returns>
    public double Area()
    {
        double area = 0;
        for (int t = 0; t < Triangles.Length; t += 3)
        {
            Vector3 a = Positions[Triangles[t]], b = Positions[Triangles[t + 1]], c = Positions[Triangles[t + 2]];
            area += Vector3.Cross(b - a, c - a).Length() / 2;
        }

        return area;
    }

    /// <summary>A copy with every vertex moved by a matrix; triangles flip if it mirrors.</summary>
    /// <param name="matrix">The transform.</param>
    /// <returns>The moved mesh.</returns>
    public Mesh Transformed(Matrix4x4 matrix)
    {
        Vector3[] moved = new Vector3[Positions.Length];
        for (int i = 0; i < moved.Length; i++)
        {
            moved[i] = Vector3.Transform(Positions[i], matrix);
        }

        int[] triangles = Triangles;
        if (matrix.GetDeterminant() < 0)
        {
            triangles = (int[])Triangles.Clone();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
            }
        }

        return new Mesh(moved, triangles, Colors, TexCoords, Texture, DoubleSided);
    }

    /// <summary>
    /// A copy scaled so its largest side is <paramref name="size"/>, centred on X and Z, and
    /// standing on Y = 0: how a printer or a scene expects to receive it.
    /// </summary>
    /// <param name="size">The largest side, in the file's units.</param>
    /// <returns>The placed mesh.</returns>
    public Mesh Placed(float size)
    {
        (Vector3 min, Vector3 max) = Bounds();
        Vector3 extent = max - min;
        float largest = MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z));
        if (largest <= 0)
        {
            return this;
        }

        float scale = size / largest;
        Vector3 offset = new(-(min.X + max.X) / 2, -min.Y, -(min.Z + max.Z) / 2);
        return Transformed(Matrix4x4.CreateTranslation(offset) * Matrix4x4.CreateScale(scale));
    }
}
