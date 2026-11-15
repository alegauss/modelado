using System.Numerics;

namespace Modelado.Geometry;

/// <summary>What Make printable found and made (MOD111).</summary>
/// <param name="Mesh">The rebuilt mesh: closed, every edge used by exactly two triangles.</param>
/// <param name="OpenEdgesBefore">Edges the model had with one triangle only: the rims of its holes.</param>
/// <param name="TangledEdgesBefore">Edges it had with three triangles or more, which no slicer can read as a surface.</param>
public sealed record PrintableResult(Mesh Mesh, int OpenEdgesBefore, int TangledEdgesBefore);

/// <summary>
/// Make printable (MOD111): the model rebuilt on a voxel grid, so whatever it was (open, with holes,
/// pieces crossing each other, edges shared by three faces) it comes out one closed two-manifold
/// surface a slicer reads with no empty layer, the voxel remesh users otherwise do by hand in
/// Blender. Each grid point is inside when most of three rays through it, one along each axis, cross
/// the surface an odd number of times before it; a ray slipping through a hole is outvoted by the two
/// that do not, which is what lets an open mesh fill. The inside is blurred once, so the steps of the
/// grid round off, and marching cubes (<see cref="MarchingCubes"/>) draws its surface on a grid
/// padded with outside, so the surface closes everywhere. Pieces are kept, joined where they overlap.
/// Each new vertex takes the colour of the nearest old one (or of the texture under it), since a
/// rebuilt surface has no texture coordinates.
/// </summary>
public static class Printable
{
    /// <summary>The voxel a print is rebuilt on unless told otherwise, in millimetres: a 0.4 mm nozzle's line.</summary>
    public const float VoxelDefault = 0.4f;

    /// <summary>The most points a side of the grid takes, so a small voxel on a large model stays in memory.</summary>
    public const int MaxCells = 384;

    /// <summary>Rebuilds the mesh closed and manifold.</summary>
    /// <param name="mesh">The model.</param>
    /// <param name="voxel">The grid's spacing, in the model's own units.</param>
    /// <returns>The rebuilt mesh and what the model had wrong.</returns>
    /// <exception cref="ArgumentException">A voxel that is not above zero, or a model with no extent.</exception>
    public static PrintableResult Make(Mesh mesh, float voxel)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!(voxel > 0))
        {
            throw new ArgumentException("A voxel is above 0.", nameof(voxel));
        }

        (Vector3 min, Vector3 max) = mesh.Bounds();
        Vector3 extent = max - min;
        float longest = MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z));
        if (!(longest > 0) || mesh.TriangleCount == 0)
        {
            throw new ArgumentException("The model has no extent to rebuild.", nameof(mesh));
        }

        // Two cells of outside on every side; the voxel grows where the model would need more cells.
        float step = MathF.Max(voxel, longest / (MaxCells - 5));
        int Cells(float length) => (int)MathF.Ceiling(length / step) + 5;
        int nx = Cells(extent.X), ny = Cells(extent.Y), nz = Cells(extent.Z);
        Vector3 origin = min - new Vector3(2 * step);
        ScalarGrid grid = new(nx, ny, nz, origin, new Vector3(step));

        // Votes per point: one for each axis whose ray finds it inside.
        byte[] votes = new byte[grid.Values.Length];
        Vector3[] p = mesh.Positions;
        int[] t = mesh.Triangles;
        for (int axis = 0; axis < 3; axis++)
        {
            Cast(axis, p, t, grid, votes);
        }

        float[] inside = new float[votes.Length];
        for (int i = 0; i < votes.Length; i++)
        {
            inside[i] = votes[i] >= 2 ? 1 : 0;
        }

        Blur(inside, nx, ny, nz, grid.Values);
        Mesh rebuilt = MarchingCubes.Extract(grid, 0.5f);
        if (rebuilt.TriangleCount == 0)
        {
            throw new ArgumentException("The model encloses nothing at this voxel: try a smaller one.", nameof(voxel));
        }

        (int open, int tangled) = EdgeFaults(t);
        return new PrintableResult(Coloured(rebuilt, mesh), open, tangled);
    }

    // Rays along one axis, one per column of grid points: each triangle crossing a column toggles
    // every point beyond its crossing, and a point left toggled an odd number of times is inside.
    private static void Cast(int axis, Vector3[] p, int[] t, ScalarGrid grid, byte[] votes)
    {
        int u = (axis + 1) % 3, v = (axis + 2) % 3;
        int[] n = [grid.Nx, grid.Ny, grid.Nz];
        float step = grid.Spacing.X;
        float[] o = [grid.Origin.X, grid.Origin.Y, grid.Origin.Z];
        List<float>[] hits = new List<float>[n[u] * n[v]];

        for (int k = 0; k < t.Length; k += 3)
        {
            Vector3 a = p[t[k]], b = p[t[k + 1]], c = p[t[k + 2]];
            float au = Get(a, u), av = Get(a, v), bu = Get(b, u), bv = Get(b, v), cu = Get(c, u), cv = Get(c, v);
            float area = ((bu - au) * (cv - av)) - ((bv - av) * (cu - au));
            if (MathF.Abs(area) < 1e-12f)
            {
                continue; // Edge-on to the ray: it crosses no column.
            }

            int i0 = Math.Max(0, (int)MathF.Ceiling((MathF.Min(au, MathF.Min(bu, cu)) - o[u]) / step));
            int i1 = Math.Min(n[u] - 1, (int)MathF.Floor((MathF.Max(au, MathF.Max(bu, cu)) - o[u]) / step));
            int j0 = Math.Max(0, (int)MathF.Ceiling((MathF.Min(av, MathF.Min(bv, cv)) - o[v]) / step));
            int j1 = Math.Min(n[v] - 1, (int)MathF.Floor((MathF.Max(av, MathF.Max(bv, cv)) - o[v]) / step));
            for (int i = i0; i <= i1; i++)
            {
                // Columns sit a hair off the lattice, so a ray never runs exactly through an edge or a vertex.
                float qu = o[u] + (i * step) + (step * 1e-3f);
                for (int j = j0; j <= j1; j++)
                {
                    float qv = o[v] + (j * step) + (step * 2e-3f);
                    float w0 = (((bu - qu) * (cv - qv)) - ((bv - qv) * (cu - qu))) / area;
                    float w1 = (((cu - qu) * (av - qv)) - ((cv - qv) * (au - qu))) / area;
                    float w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0)
                    {
                        continue;
                    }

                    float along = (w0 * Get(a, axis)) + (w1 * Get(b, axis)) + (w2 * Get(c, axis));
                    (hits[(i * n[v]) + j] ??= []).Add(along);
                }
            }
        }

        for (int i = 0; i < n[u]; i++)
        {
            for (int j = 0; j < n[v]; j++)
            {
                if (hits[(i * n[v]) + j] is not { } crossings)
                {
                    continue;
                }

                crossings.Sort();
                int next = 0;
                bool within = false;
                for (int s = 0; s < n[axis]; s++)
                {
                    float at = o[axis] + (s * step);
                    while (next < crossings.Count && crossings[next] <= at)
                    {
                        within = !within;
                        next++;
                    }

                    if (within)
                    {
                        votes[axis switch
                        {
                            0 => grid.Index(s, i, j),
                            1 => grid.Index(j, s, i),
                            _ => grid.Index(i, j, s),
                        }]++;
                    }
                }
            }
        }
    }

    private static float Get(Vector3 point, int axis) => axis switch { 0 => point.X, 1 => point.Y, _ => point.Z };

    // One pass of a 3 × 3 × 3 box blur, as three passes of three along each axis; the outermost
    // layer stays outside, so the surface closes.
    private static void Blur(float[] inside, int nx, int ny, int nz, float[] into)
    {
        int sx = ny * nz, sy = nz;
        float[] a = inside, b = new float[inside.Length];
        foreach ((int stride, int count, int along) in new[] { (sx, nx, 0), (sy, ny, 1), (1, nz, 2) })
        {
            for (int at = 0; at < a.Length; at++)
            {
                int position = along switch { 0 => at / sx, 1 => at / sy % ny, _ => at % nz };
                b[at] = position == 0 || position == count - 1 ? a[at] : (a[at - stride] + a[at] + a[at + stride]) / 3;
            }

            (a, b) = (b, a == inside ? new float[inside.Length] : a);
        }

        for (int x = 0; x < nx; x++)
        {
            for (int y = 0; y < ny; y++)
            {
                for (int z = 0; z < nz; z++)
                {
                    int at = (x * sx) + (y * sy) + z;
                    bool rim = x == 0 || y == 0 || z == 0 || x == nx - 1 || y == ny - 1 || z == nz - 1;
                    into[at] = rim ? 0 : a[at];
                }
            }
        }
    }

    // Edges used once (a hole's rim) and three times or more (a surface no slicer can follow).
    private static (int Open, int Tangled) EdgeFaults(int[] t)
    {
        Dictionary<(int, int), int> uses = [];
        for (int k = 0; k < t.Length; k += 3)
        {
            for (int e = 0; e < 3; e++)
            {
                int a = t[k + e], b = t[k + ((e + 1) % 3)];
                (int, int) key = a < b ? (a, b) : (b, a);
                uses[key] = uses.GetValueOrDefault(key) + 1;
            }
        }

        return (uses.Values.Count(n => n == 1), uses.Values.Count(n => n > 2));
    }

    // Each new vertex the colour of the nearest old vertex, or of the texture there, through a hash of cells.
    private static Mesh Coloured(Mesh rebuilt, Mesh source)
    {
        Vector3[]? colors = source.Colors;
        if (colors is null && source.Texture is { } texture && source.TexCoords is { } uv)
        {
            colors = [.. uv.Select(c => { (float r, float g, float b) = texture.Sample(c.X, c.Y); return new Vector3(r, g, b); })];
        }

        if (colors is null)
        {
            return rebuilt;
        }

        // Cells about as wide as the old vertices are apart, so a few cells around a point hold its nearest.
        (Vector3 min, Vector3 max) = source.Bounds();
        float diagonal = MathF.Max((max - min).Length(), 1e-6f);
        Vector3 box = Vector3.Max(max - min, new Vector3(diagonal / 64));
        float cell = MathF.Max(MathF.Cbrt(box.X * box.Y * box.Z / source.VertexCount), diagonal / 1024);
        int farthest = (int)MathF.Ceiling(diagonal / cell) + 1;
        Dictionary<(int, int, int), List<int>> buckets = [];
        (int, int, int) Key(Vector3 q) => ((int)MathF.Floor(q.X / cell), (int)MathF.Floor(q.Y / cell), (int)MathF.Floor(q.Z / cell));
        for (int i = 0; i < source.VertexCount; i++)
        {
            (int, int, int) key = Key(source.Positions[i]);
            if (!buckets.TryGetValue(key, out List<int>? list))
            {
                list = [];
                buckets[key] = list;
            }

            list.Add(i);
        }

        Vector3[] result = new Vector3[rebuilt.VertexCount];
        for (int i = 0; i < result.Length; i++)
        {
            Vector3 q = rebuilt.Positions[i];
            (int kx, int ky, int kz) = Key(q);
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int reach = 1; best < 0 && reach <= farthest; reach++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    for (int dy = -reach; dy <= reach; dy++)
                    {
                        for (int dz = -reach; dz <= reach; dz++)
                        {
                            if (!buckets.TryGetValue((kx + dx, ky + dy, kz + dz), out List<int>? near))
                            {
                                continue;
                            }

                            foreach (int j in near)
                            {
                                float d = Vector3.DistanceSquared(q, source.Positions[j]);
                                if (d < bestDistance)
                                {
                                    (bestDistance, best) = (d, j);
                                }
                            }
                        }
                    }
                }
            }

            result[i] = best >= 0 ? colors[best] : new Vector3(0.8f);
        }

        return new Mesh(rebuilt.Positions, rebuilt.Triangles, result);
    }
}
