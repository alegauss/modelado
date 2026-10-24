using System.Numerics;
using Modelado.Imaging;

namespace Modelado.Geometry;

/// <summary>
/// Lays the photo's own colours back onto the side of the object it shows. TripoSR's decoder
/// colours every vertex from a 64 × 64 triplane, which blurs print, eyes and edges; the photo has
/// them at full resolution. The front of the mesh (+Z, the view the photo was taken from) is
/// projected onto the photo, the mesh's outline fitted to the object's outline in it; a vertex
/// takes the photo's colour where it is seen from the front and faces it, and keeps the
/// decoder's colour where it turns away, with a soft blend between.
/// </summary>
public static class ColorProjection
{
    /// <summary>Projects the photo onto the mesh's front.</summary>
    /// <param name="mesh">A mesh with vertex colours, Y up, the photo's view at +Z.</param>
    /// <param name="reference">The object as the model saw it: square, the background transparent.</param>
    /// <param name="strength">0 keeps the mesh's colours, 1 takes the photo's fully where it faces it.</param>
    /// <returns>The recoloured mesh and how many vertices took the photo's colour.</returns>
    public static (Mesh Mesh, int Projected) Project(Mesh mesh, RgbaImage reference, float strength = 1f)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(reference);
        (int X, int Y, int Width, int Height) box = ImageOps.OpaqueBounds(reference, 127)
            ?? throw new InvalidOperationException("The reference image has no object in it.");
        (Vector3 min, Vector3 max) = mesh.Bounds();
        int w = reference.Width, h = reference.Height;
        float sx = box.Width / MathF.Max(max.X - min.X, 1e-6f), sy = box.Height / MathF.Max(max.Y - min.Y, 1e-6f);

        // Each vertex in the photo's pixels, and the frontmost surface there.
        Vector2[] pixel = new Vector2[mesh.VertexCount];
        for (int i = 0; i < pixel.Length; i++)
        {
            Vector3 p = mesh.Positions[i];
            pixel[i] = new Vector2(box.X + ((p.X - min.X) * sx), box.Y + ((max.Y - p.Y) * sy));
        }

        float[] front = new float[w * h];
        Array.Fill(front, float.MinValue);
        int[] t = mesh.Triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            Raster(pixel[t[i]], pixel[t[i + 1]], pixel[t[i + 2]], mesh.Positions[t[i]].Z, mesh.Positions[t[i + 1]].Z, mesh.Positions[t[i + 2]].Z, front, w, h);
        }

        float tolerance = (max.Z - min.Z) * 0.01f;
        Vector3[] normals = mesh.ComputeNormals();
        Vector3[] colors = (Vector3[])(mesh.Colors ?? MeshExport.VertexColors(mesh) ?? [.. Enumerable.Repeat(Vector3.One, mesh.VertexCount)]).Clone();
        int projected = 0;
        for (int i = 0; i < colors.Length; i++)
        {
            int x = (int)pixel[i].X, y = (int)pixel[i].Y;
            if (x < 1 || y < 1 || x >= w - 1 || y >= h - 1 || mesh.Positions[i].Z < front[(y * w) + x] - tolerance)
            {
                continue;
            }

            // Inside the object by a pixel on every side, so the background never bleeds in.
            float alpha = 1;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    alpha = MathF.Min(alpha, reference.Pixels[reference.OffsetOf(x + dx, y + dy) + 3] / 255f);
                }
            }

            float facing = Math.Clamp((normals[i].Z - 0.25f) / 0.45f, 0, 1);
            float weight = strength * alpha * facing * facing * (3 - (2 * facing));
            if (weight <= 0)
            {
                continue;
            }

            (float r, float g, float b) = reference.Sample(pixel[i].X / w, pixel[i].Y / h);
            colors[i] = Vector3.Lerp(colors[i], new Vector3(r, g, b), weight);
            projected++;
        }

        return (new Mesh(mesh.Positions, mesh.Triangles, colors, mesh.TexCoords, mesh.Texture, mesh.DoubleSided), projected);
    }

    // The largest Z over the triangle's pixels: the side nearest a viewer at +Z.
    private static void Raster(Vector2 a, Vector2 b, Vector2 c, float za, float zb, float zc, float[] front, int w, int h)
    {
        float area = ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
        if (MathF.Abs(area) < 1e-12f)
        {
            return;
        }

        int left = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        int right = Math.Min(w - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        int top = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        int bottom = Math.Min(h - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float wa = (((b.X - px) * (c.Y - py)) - ((b.Y - py) * (c.X - px))) / area;
                float wb = (((c.X - px) * (a.Y - py)) - ((c.Y - py) * (a.X - px))) / area;
                float wc = 1 - wa - wb;
                if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f)
                {
                    continue;
                }

                float z = (wa * za) + (wb * zb) + (wc * zc);
                int i = (y * w) + x;
                if (z > front[i])
                {
                    front[i] = z;
                }
            }
        }
    }
}
