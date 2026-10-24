using System.Numerics;
using Modelado.Imaging;

namespace Modelado.Geometry;

/// <summary>How the viewer shades a mesh.</summary>
public enum RenderStyle
{
    /// <summary>The mesh's own colours, lit.</summary>
    Color,

    /// <summary>Plain grey, lit: the shape alone, as a print will come out.</summary>
    Clay,

    /// <summary>Clay with the triangle edges drawn over it.</summary>
    Wireframe,
}

/// <summary>A camera orbiting the mesh's centre.</summary>
/// <param name="Yaw">Rotation about the vertical axis, in degrees; 0 looks at the front (from +Z).</param>
/// <param name="Pitch">Elevation, in degrees; positive looks down from above.</param>
/// <param name="Zoom">1 fits the mesh; larger comes closer.</param>
/// <param name="FieldOfView">The vertical field of view, in degrees.</param>
public readonly record struct OrbitCamera(float Yaw = 0, float Pitch = 15, float Zoom = 1, float FieldOfView = 35)
{
    /// <summary>The front view, slightly from above: what a struct's zeros would not be.</summary>
    public OrbitCamera()
        : this(0, 15, 1, 35)
    {
    }

    /// <summary>The camera turned by a mouse drag.</summary>
    /// <param name="dx">Horizontal pixels.</param>
    /// <param name="dy">Vertical pixels.</param>
    /// <returns>The turned camera.</returns>
    public OrbitCamera Dragged(double dx, double dy) =>
        this with { Yaw = (float)(Yaw - (dx * 0.4)), Pitch = Math.Clamp((float)(Pitch + (dy * 0.4)), -89, 89) };

    /// <summary>The camera moved by a mouse wheel.</summary>
    /// <param name="notches">Wheel notches, positive away from the user.</param>
    /// <returns>The moved camera.</returns>
    public OrbitCamera Zoomed(double notches) => this with { Zoom = Math.Clamp((float)(Zoom * Math.Pow(1.15, notches)), 0.2f, 20f) };
}

/// <summary>
/// Draws a mesh into a BGRA pixel buffer on the CPU: perspective, a depth buffer, Gouraud
/// shading from a light over the camera's shoulder, and the image split into bands drawn in
/// parallel. Fast enough to orbit a 300 000 triangle TripoSR mesh, and no GPU or UI framework
/// is involved, so the same picture comes out in the app, the command line and the tests.
/// </summary>
public sealed class MeshRenderer
{
    private const int Band = 32;
    private static readonly Vector3 Clay = new(Brand.Clay.R / 255f, Brand.Clay.G / 255f, Brand.Clay.B / 255f);
    private static readonly uint Edge = Argb(Brand.Edge);
    private readonly Mesh mesh;
    private readonly Vector3[] normals;
    private readonly Vector3[] albedo;
    private readonly Vector3 centre;
    private readonly float radius;

    /// <summary>Prepares a mesh for drawing: its normals, colours and bounding sphere.</summary>
    /// <param name="mesh">The mesh.</param>
    public MeshRenderer(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        this.mesh = mesh;
        normals = mesh.ComputeNormals();
        albedo = MeshExport.VertexColors(mesh) ?? [.. Enumerable.Repeat(Clay, mesh.VertexCount)];
        (Vector3 min, Vector3 max) = mesh.Bounds();
        centre = (min + max) / 2;
        radius = MathF.Max(1e-6f, (max - min).Length() / 2);
    }

    /// <summary>The background, top and bottom of a vertical gradient, as 0xAARRGGBB.</summary>
    public (uint Top, uint Bottom) Background { get; init; } = (Argb(Brand.ViewportTop), Argb(Brand.ViewportBottom));

    /// <summary>Draws into an image.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="camera">Where from.</param>
    /// <param name="style">How.</param>
    /// <returns>The picture.</returns>
    public RgbaImage RenderImage(int width, int height, OrbitCamera camera, RenderStyle style = RenderStyle.Color)
    {
        uint[] bgra = new uint[width * height];
        Render(bgra, width, height, camera, style);
        RgbaImage image = new(width, height);
        for (int i = 0; i < bgra.Length; i++)
        {
            uint p = bgra[i];
            image.Pixels[i * 4] = (byte)(p >> 16);
            image.Pixels[(i * 4) + 1] = (byte)(p >> 8);
            image.Pixels[(i * 4) + 2] = (byte)p;
            image.Pixels[(i * 4) + 3] = 255;
        }

        return image;
    }

    /// <summary>Draws into a buffer of 0xAARRGGBB pixels (BGRA bytes in memory), rows top to bottom.</summary>
    /// <param name="pixels">width × height pixels.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="camera">Where from.</param>
    /// <param name="style">How.</param>
    public void Render(uint[] pixels, int width, int height, OrbitCamera camera, RenderStyle style = RenderStyle.Color)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length < width * height)
        {
            throw new ArgumentException("The buffer is smaller than width × height.", nameof(pixels));
        }

        float[] depth = new float[width * height];
        float fov = camera.FieldOfView * MathF.PI / 180;
        float distance = radius / MathF.Sin(fov / 2) / camera.Zoom;
        float yaw = camera.Yaw * MathF.PI / 180, pitch = camera.Pitch * MathF.PI / 180;
        Vector3 eye = centre + (distance * new Vector3(MathF.Cos(pitch) * MathF.Sin(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw)));
        Matrix4x4 view = Matrix4x4.CreateLookAt(eye, centre, Vector3.UnitY);
        float near = MathF.Max(distance - (radius * 1.5f), distance * 0.01f);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(fov, (float)width / height, near, distance + (radius * 1.5f));
        Matrix4x4 viewProjection = view * projection;

        // A key light over the camera's left shoulder and a weaker fill from the right.
        Vector3 forward = Vector3.Normalize(centre - eye);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Cross(right, forward);
        Vector3 key = Vector3.Normalize(-forward + (0.6f * up) - (0.5f * right));
        Vector3 fill = Vector3.Normalize(-forward + (0.2f * right));

        int count = mesh.VertexCount;
        Vector4[] screen = new Vector4[count]; // x, y in pixels, z depth in [0, 1], w > 0 when in front
        Vector3[] shade = new Vector3[count];
        bool clay = style != RenderStyle.Color;
        bool twoSided = mesh.DoubleSided;
        Parallel.For(0, count, i =>
        {
            Vector4 clip = Vector4.Transform(new Vector4(mesh.Positions[i], 1), viewProjection);
            screen[i] = clip.W > 1e-6f
                ? new Vector4(((clip.X / clip.W) + 1) * 0.5f * width, (1 - (clip.Y / clip.W)) * 0.5f * height, clip.Z / clip.W, 1)
                : new Vector4(0, 0, 0, -1);
            Vector3 n = normals[i];
            if (twoSided && Vector3.Dot(n, eye - mesh.Positions[i]) < 0)
            {
                n = -n;
            }

            float light = 0.25f + (0.65f * MathF.Max(0, Vector3.Dot(n, key))) + (0.2f * MathF.Max(0, Vector3.Dot(n, fill)));
            shade[i] = (clay ? Clay : albedo[i]) * light;
        });

        int bands = (height + Band - 1) / Band;
        int[] t = mesh.Triangles;
        Parallel.For(0, bands, band =>
        {
            int y0 = band * Band, y1 = Math.Min(height, y0 + Band);
            FillBackground(pixels, depth, width, height, y0, y1);
            for (int i = 0; i < t.Length; i += 3)
            {
                DrawTriangle(pixels, depth, width, y0, y1, screen, shade, t[i], t[i + 1], t[i + 2], twoSided);
            }

            if (style == RenderStyle.Wireframe)
            {
                for (int i = 0; i < t.Length; i += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        DrawEdge(pixels, depth, width, y0, y1, screen[t[i + k]], screen[t[i + ((k + 1) % 3)]]);
                    }
                }
            }
        });
    }

    private void FillBackground(uint[] pixels, float[] depth, int width, int height, int y0, int y1)
    {
        for (int y = y0; y < y1; y++)
        {
            uint colour = Mix(Background.Top, Background.Bottom, (float)y / Math.Max(1, height - 1));
            pixels.AsSpan(y * width, width).Fill(colour);
            depth.AsSpan(y * width, width).Fill(float.MaxValue);
        }
    }

    private static void DrawTriangle(uint[] pixels, float[] depth, int width, int y0, int y1, Vector4[] screen, Vector3[] shade, int ia, int ib, int ic, bool twoSided)
    {
        Vector4 a = screen[ia], b = screen[ib], c = screen[ic];
        if (a.W < 0 || b.W < 0 || c.W < 0)
        {
            return;
        }

        float minY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y)), maxY = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
        if (maxY < y0 || minY >= y1)
        {
            return;
        }

        // Twice the signed area; screen Y runs down, so a front face is negative here.
        float area = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
        if (MathF.Abs(area) < 1e-9f || (!twoSided && area > 0))
        {
            return;
        }

        int left = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        int right = Math.Min(width - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        int top = Math.Max(y0, (int)MathF.Floor(minY));
        int bottom = Math.Min(y1 - 1, (int)MathF.Ceiling(maxY));
        float inverse = 1 / area;
        Vector3 sa = shade[ia], sb = shade[ib], sc = shade[ic];
        for (int y = top; y <= bottom; y++)
        {
            float py = y + 0.5f;
            for (int x = left; x <= right; x++)
            {
                float px = x + 0.5f;
                float wa = (((b.X - px) * (c.Y - py)) - ((b.Y - py) * (c.X - px))) * inverse;
                float wb = (((c.X - px) * (a.Y - py)) - ((c.Y - py) * (a.X - px))) * inverse;
                float wc = 1 - wa - wb;
                if (wa < 0 || wb < 0 || wc < 0)
                {
                    continue;
                }

                float z = (wa * a.Z) + (wb * b.Z) + (wc * c.Z);
                int index = (y * width) + x;
                if (z >= depth[index])
                {
                    continue;
                }

                depth[index] = z;
                Vector3 colour = (wa * sa) + (wb * sb) + (wc * sc);
                pixels[index] = Pack(colour);
            }
        }
    }

    private static void DrawEdge(uint[] pixels, float[] depth, int width, int y0, int y1, Vector4 a, Vector4 b)
    {
        if (a.W < 0 || b.W < 0)
        {
            return;
        }

        int steps = (int)MathF.Ceiling(MathF.Max(MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y)));
        for (int s = 0; s <= steps; s++)
        {
            float f = steps == 0 ? 0 : (float)s / steps;
            int x = (int)(a.X + ((b.X - a.X) * f)), y = (int)(a.Y + ((b.Y - a.Y) * f));
            if (x < 0 || x >= width || y < y0 || y >= y1)
            {
                continue;
            }

            int index = (y * width) + x;
            float z = a.Z + ((b.Z - a.Z) * f);
            // A small bias, so an edge on the visible surface wins against that surface.
            if (z <= depth[index] + 2e-4f)
            {
                pixels[index] = Edge;
            }
        }
    }

    private static uint Argb((byte R, byte G, byte B) colour) => 0xFF000000u | ((uint)colour.R << 16) | ((uint)colour.G << 8) | colour.B;

    private static uint Pack(Vector3 colour)
    {
        uint r = (uint)Math.Clamp(colour.X * 255f, 0, 255);
        uint g = (uint)Math.Clamp(colour.Y * 255f, 0, 255);
        uint b = (uint)Math.Clamp(colour.Z * 255f, 0, 255);
        return 0xFF000000u | (r << 16) | (g << 8) | b;
    }

    private static uint Mix(uint top, uint bottom, float t)
    {
        uint Channel(int shift)
        {
            float from = (top >> shift) & 0xFF, to = (bottom >> shift) & 0xFF;
            return (uint)Math.Clamp(from + ((to - from) * t), 0, 255);
        }

        return 0xFF000000u | (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);
    }
}
