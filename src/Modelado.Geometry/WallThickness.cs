using System.Numerics;

namespace Modelado.Geometry;

/// <summary>What a thickness check found (MOD146).</summary>
/// <param name="Mesh">The model with its thin vertices coloured red, the rest as they were (light grey where it had no colour).</param>
/// <param name="ThinVertices">How many vertices are thinner than the minimum.</param>
/// <param name="Vertices">How many vertices were measured.</param>
/// <param name="ThinShare">The share of the surface around them, 0 to 1.</param>
public sealed record ThicknessReport(Mesh Mesh, int ThinVertices, int Vertices, double ThinShare);

/// <summary>
/// The thickness check (MOD146): the parts of a model too thin to print, an ear or a tail a slicer
/// drops or a print snaps. At each vertex a ray is cast inward, along the reversed vertex normal, to
/// where it leaves the model again through a face turned the same way; that distance is the wall's
/// thickness there. Only whether it is under the minimum matters, so each ray stops at the minimum,
/// and the triangles are kept in a uniform grid with cells at least that long, which a ray then
/// crosses in a few cells rather than testing every triangle. The vertices under the minimum are
/// coloured red, so the view shows where they are, and the share of the surface they hold is counted.
/// </summary>
public static class WallThickness
{
    /// <summary>The thinnest wall a print keeps unless told otherwise, in millimetres: three 0.4 mm lines.</summary>
    public const float MinimumDefault = 1.2f;

    private static readonly Vector3 Thin = new(0.9f, 0.1f, 0.1f);
    private static readonly Vector3 Plain = new(0.8f, 0.8f, 0.8f);

    /// <summary>Checks a model against a minimum thickness.</summary>
    /// <param name="mesh">A closed model, its faces pointing out.</param>
    /// <param name="minimum">The thinnest a wall may be, in the model's units.</param>
    /// <returns>The coloured model and the counts.</returns>
    /// <exception cref="ArgumentException">A minimum that is not above 0.</exception>
    public static ThicknessReport Check(Mesh mesh, float minimum)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!(minimum > 0))
        {
            throw new ArgumentException("The minimum thickness needs to be above 0.", nameof(minimum));
        }

        float[] thickness = Measure(mesh, minimum);
        bool[] thin = [.. thickness.Select(t => t < minimum)];

        // Each vertex holds a third of the area of the triangles around it.
        double[] area = new double[mesh.VertexCount];
        int[] t = mesh.Triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = mesh.Positions[t[i]], b = mesh.Positions[t[i + 1]], c = mesh.Positions[t[i + 2]];
            double third = Vector3.Cross(b - a, c - a).Length() / 6.0;
            area[t[i]] += third;
            area[t[i + 1]] += third;
            area[t[i + 2]] += third;
        }

        double total = area.Sum(), thinArea = Enumerable.Range(0, area.Length).Where(i => thin[i]).Sum(i => area[i]);
        Vector3[] colors = new Vector3[mesh.VertexCount];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = thin[i] ? Thin : mesh.Colors?[i] ?? Plain;
        }

        // Colours show only without a texture over them, so the checked copy leaves it off; Undo brings it back.
        Mesh coloured = new(mesh.Positions, mesh.Triangles, colors, null, null, mesh.DoubleSided);
        return new ThicknessReport(coloured, thin.Count(x => x), mesh.VertexCount, total > 0 ? thinArea / total : 0);
    }

    /// <summary>The wall's thickness at each vertex, up to a limit.</summary>
    /// <param name="mesh">A closed model, its faces pointing out.</param>
    /// <param name="limit">How far a ray goes; a wall thicker than this reads as infinity.</param>
    /// <returns>One thickness per vertex, in the model's units.</returns>
    public static float[] Measure(Mesh mesh, float limit)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        float diagonal = Vector3.Distance(min, max);
        float cell = MathF.Max(limit, diagonal / 128);
        float epsilon = diagonal * 1e-5f;
        int[] t = mesh.Triangles;
        Vector3[] p = mesh.Positions;

        Dictionary<(int, int, int), List<int>> grid = [];
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 low = Vector3.Min(p[t[i]], Vector3.Min(p[t[i + 1]], p[t[i + 2]]));
            Vector3 high = Vector3.Max(p[t[i]], Vector3.Max(p[t[i + 1]], p[t[i + 2]]));
            foreach ((int, int, int) key in Cells(low, high, min, cell))
            {
                if (!grid.TryGetValue(key, out List<int>? list))
                {
                    grid[key] = list = [];
                }

                list.Add(i);
            }
        }

        Vector3[] normals = mesh.ComputeNormals();
        float[] thickness = new float[p.Length];
        Parallel.For(0, p.Length, v =>
        {
            Vector3 direction = -normals[v];
            float best = float.PositiveInfinity;
            if (direction != Vector3.Zero)
            {
                Vector3 end = p[v] + (direction * limit);
                foreach ((int, int, int) key in Cells(Vector3.Min(p[v], end), Vector3.Max(p[v], end), min, cell))
                {
                    if (!grid.TryGetValue(key, out List<int>? list))
                    {
                        continue;
                    }

                    foreach (int i in list)
                    {
                        if (t[i] == v || t[i + 1] == v || t[i + 2] == v)
                        {
                            continue;
                        }

                        float hit = Exit(p[v], direction, p[t[i]], p[t[i + 1]], p[t[i + 2]]);
                        if (hit > epsilon && hit <= limit && hit < best)
                        {
                            best = hit;
                        }
                    }
                }
            }

            thickness[v] = best;
        });

        return thickness;
    }

    private static IEnumerable<(int, int, int)> Cells(Vector3 low, Vector3 high, Vector3 origin, float cell)
    {
        (int x0, int y0, int z0) = Cell(low, origin, cell);
        (int x1, int y1, int z1) = Cell(high, origin, cell);
        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    yield return (x, y, z);
                }
            }
        }
    }

    private static (int, int, int) Cell(Vector3 point, Vector3 origin, float cell) =>
        ((int)MathF.Floor((point.X - origin.X) / cell), (int)MathF.Floor((point.Y - origin.Y) / cell), (int)MathF.Floor((point.Z - origin.Z) / cell));

    // Möller–Trumbore, counting only a face the ray leaves through (turned the way the ray goes);
    // the distance along the ray, or infinity.
    private static float Exit(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a;
        if (Vector3.Dot(Vector3.Cross(ab, ac), direction) <= 0)
        {
            return float.PositiveInfinity;
        }

        Vector3 h = Vector3.Cross(direction, ac);
        float det = Vector3.Dot(ab, h);
        if (MathF.Abs(det) < 1e-12f)
        {
            return float.PositiveInfinity;
        }

        float inverse = 1 / det;
        Vector3 s = origin - a;
        float u = inverse * Vector3.Dot(s, h);
        if (u is < 0 or > 1)
        {
            return float.PositiveInfinity;
        }

        Vector3 q = Vector3.Cross(s, ab);
        float w = inverse * Vector3.Dot(direction, q);
        if (w < 0 || u + w > 1)
        {
            return float.PositiveInfinity;
        }

        return inverse * Vector3.Dot(ac, q);
    }
}
