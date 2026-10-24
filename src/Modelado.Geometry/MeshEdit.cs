using System.Numerics;

namespace Modelado.Geometry;

/// <summary>An axis to turn a model about.</summary>
public enum Axis
{
    /// <summary>Left to right.</summary>
    X,

    /// <summary>Up.</summary>
    Y,

    /// <summary>Front to back.</summary>
    Z,
}

/// <summary>
/// The edits a reconstructed or imported model can take: fewer triangles, more triangles,
/// holes closed, a flat base, a turn. Each returns a new mesh; colours and texture coordinates
/// follow the vertices.
/// </summary>
public static class MeshEdit
{
    /// <summary>
    /// Garland and Heckbert's quadric edge collapse: merges the edge whose removal moves the
    /// surface least, again and again, until <paramref name="targetTriangles"/> are left. Borders
    /// are held by extra planes, and a collapse that would fold a triangle over or make the surface
    /// non-manifold is skipped, so a closed mesh stays closed.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="targetTriangles">How many triangles to keep.</param>
    /// <param name="cancellation">Checked every few thousand collapses.</param>
    /// <returns>The simplified mesh; fewer triangles may remain than asked when nothing more can go safely.</returns>
    public static Mesh Simplify(Mesh mesh, int targetTriangles, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (targetTriangles >= mesh.TriangleCount)
        {
            return mesh;
        }

        int n = mesh.VertexCount;
        Vector3[] position = (Vector3[])mesh.Positions.Clone();
        Vector3[]? colors = (Vector3[]?)mesh.Colors?.Clone();
        Vector2[]? uvs = (Vector2[]?)mesh.TexCoords?.Clone();
        int[] faces = (int[])mesh.Triangles.Clone();
        bool[] removed = new bool[mesh.TriangleCount];
        bool[] dead = new bool[n];
        int[] version = new int[n];
        List<int>[] around = new List<int>[n];
        for (int i = 0; i < n; i++)
        {
            around[i] = [];
        }

        Quadric[] q = new Quadric[n];
        for (int f = 0; f < removed.Length; f++)
        {
            int a = faces[f * 3], b = faces[(f * 3) + 1], c = faces[(f * 3) + 2];
            around[a].Add(f);
            around[b].Add(f);
            around[c].Add(f);
            Vector3 normal = Vector3.Cross(position[b] - position[a], position[c] - position[a]);
            float area = normal.Length();
            if (area <= 0)
            {
                continue;
            }

            normal /= area;
            Quadric plane = Quadric.Plane(normal, -Vector3.Dot(normal, position[a]), area);
            q[a] += plane;
            q[b] += plane;
            q[c] += plane;
        }

        // Border edges: a steep plane through each, so the outline holds its shape.
        Dictionary<(int, int), int> edgeFace = [];
        for (int f = 0; f < removed.Length; f++)
        {
            for (int k = 0; k < 3; k++)
            {
                int a = faces[(f * 3) + k], b = faces[(f * 3) + ((k + 1) % 3)];
                (int, int) key = a < b ? (a, b) : (b, a);
                edgeFace[key] = edgeFace.ContainsKey(key) ? -1 : f;
            }
        }

        PriorityQueue<(int A, int B, int VersionA, int VersionB, Vector3 Target, float Mix), double> queue = new();
        foreach (((int a, int b), int f) in edgeFace)
        {
            if (f >= 0)
            {
                Vector3 e = position[b] - position[a];
                Vector3 fn = Vector3.Cross(position[faces[(f * 3) + 1]] - position[faces[f * 3]], position[faces[(f * 3) + 2]] - position[faces[f * 3]]);
                Vector3 side = Vector3.Cross(e, fn);
                if (side.LengthSquared() > 0)
                {
                    side = Vector3.Normalize(side);
                    Quadric wall = Quadric.Plane(side, -Vector3.Dot(side, position[a]), 1000 * e.LengthSquared());
                    q[a] += wall;
                    q[b] += wall;
                }
            }
        }

        foreach ((int a, int b) in edgeFace.Keys)
        {
            Enqueue(a, b);
        }

        int alive = removed.Length;
        int collapses = 0;
        HashSet<int> ringA = [], ringB = [];
        while (alive > targetTriangles && queue.TryDequeue(out var edge, out _))
        {
            (int a, int b, int va, int vb, Vector3 target, float mix) = edge;
            if (dead[a] || dead[b] || version[a] != va || version[b] != vb)
            {
                continue;
            }

            // Link condition: the vertices both neighbour must be exactly the shared triangles' third corners.
            Ring(a, ringA);
            Ring(b, ringB);
            int shared = around[a].Count(f => !removed[f] && Has(f, b));
            ringA.IntersectWith(ringB);
            if (shared == 0 || ringA.Count != shared || Folds(a, b, target) || Folds(b, a, target))
            {
                continue;
            }

            position[a] = target;
            q[a] += q[b];
            if (colors is not null)
            {
                colors[a] = Vector3.Lerp(colors[a], colors[b], mix);
            }

            if (uvs is not null)
            {
                uvs[a] = Vector2.Lerp(uvs[a], uvs[b], mix);
            }

            foreach (int f in around[b])
            {
                if (removed[f])
                {
                    continue;
                }

                if (Has(f, a))
                {
                    removed[f] = true;
                    alive--;
                    continue;
                }

                for (int k = 0; k < 3; k++)
                {
                    if (faces[(f * 3) + k] == b)
                    {
                        faces[(f * 3) + k] = a;
                    }
                }

                around[a].Add(f);
            }

            dead[b] = true;
            around[b].Clear();
            around[a].RemoveAll(f => removed[f]);
            version[a]++;
            Ring(a, ringA);
            foreach (int neighbour in ringA)
            {
                Enqueue(a, neighbour);
            }

            if (++collapses % 4096 == 0)
            {
                cancellation.ThrowIfCancellationRequested();
            }
        }

        List<int> kept = new(alive * 3);
        for (int f = 0; f < removed.Length; f++)
        {
            if (!removed[f])
            {
                kept.AddRange([faces[f * 3], faces[(f * 3) + 1], faces[(f * 3) + 2]]);
            }
        }

        return MeshCleanup.Compact(new Mesh(position, faces, colors, uvs, mesh.Texture, mesh.DoubleSided), [.. kept]);

        bool Has(int f, int v) => faces[f * 3] == v || faces[(f * 3) + 1] == v || faces[(f * 3) + 2] == v;

        void Ring(int v, HashSet<int> into)
        {
            into.Clear();
            foreach (int f in around[v])
            {
                if (!removed[f])
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int w = faces[(f * 3) + k];
                        if (w != v)
                        {
                            into.Add(w);
                        }
                    }
                }
            }
        }

        // Whether moving `moving` to `to` turns one of its triangles (not shared with `other`) over.
        bool Folds(int moving, int other, Vector3 to)
        {
            foreach (int f in around[moving])
            {
                if (removed[f] || Has(f, other))
                {
                    continue;
                }

                Vector3 p0 = position[faces[f * 3]], p1 = position[faces[(f * 3) + 1]], p2 = position[faces[(f * 3) + 2]];
                Vector3 before = Vector3.Cross(p1 - p0, p2 - p0);
                Vector3 n0 = faces[f * 3] == moving ? to : p0, n1 = faces[(f * 3) + 1] == moving ? to : p1, n2 = faces[(f * 3) + 2] == moving ? to : p2;
                Vector3 after = Vector3.Cross(n1 - n0, n2 - n0);
                if (after.LengthSquared() < 1e-20f || Vector3.Dot(Vector3.Normalize(before), Vector3.Normalize(after)) < 0.2f)
                {
                    return true;
                }
            }

            return false;
        }

        void Enqueue(int a, int b)
        {
            Quadric sum = q[a] + q[b];
            Vector3 middle = (position[a] + position[b]) / 2;
            (Vector3 best, float mix, double cost) = (position[a], 0f, sum.Error(position[a]));
            double cb = sum.Error(position[b]), cm = sum.Error(middle);
            if (cb < cost)
            {
                (best, mix, cost) = (position[b], 1f, cb);
            }

            if (cm < cost)
            {
                (best, mix, cost) = (middle, 0.5f, cm);
            }

            queue.Enqueue((a, b, version[a], version[b], best, mix), cost);
        }
    }

    /// <summary>
    /// Splits every triangle into four at its edges' midpoints, then smooths, so the surface gets
    /// finer and rounder: what a coarse reconstruction needs before printing large.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="smoothing">Taubin passes after the split; 0 keeps the flat facets.</param>
    /// <returns>A mesh with four times the triangles.</returns>
    public static Mesh Subdivide(Mesh mesh, int smoothing = 2)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        List<Vector3> positions = [.. mesh.Positions];
        List<Vector3>? colors = mesh.Colors is null ? null : [.. mesh.Colors];
        List<Vector2>? uvs = mesh.TexCoords is null ? null : [.. mesh.TexCoords];
        Dictionary<(int, int), int> middle = [];
        int[] t = mesh.Triangles;
        int[] result = new int[t.Length * 4];
        for (int i = 0; i < t.Length; i += 3)
        {
            int a = t[i], b = t[i + 1], c = t[i + 2];
            int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
            int o = i * 4;
            ReadOnlySpan<int> split = [a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca];
            split.CopyTo(result.AsSpan(o));
        }

        Mesh fine = new([.. positions], result, colors?.ToArray(), uvs?.ToArray(), mesh.Texture, mesh.DoubleSided);
        return MeshCleanup.Smooth(fine, smoothing);

        int Middle(int a, int b)
        {
            (int, int) key = a < b ? (a, b) : (b, a);
            if (!middle.TryGetValue(key, out int m))
            {
                m = positions.Count;
                middle.Add(key, m);
                positions.Add((positions[a] + positions[b]) / 2);
                colors?.Add((colors[a] + colors[b]) / 2);
                uvs?.Add((uvs[a] + uvs[b]) / 2);
            }

            return m;
        }
    }

    /// <summary>
    /// Closes every hole: each loop of border edges gets a vertex at its centre and a fan of
    /// triangles to it, wound like its neighbours. On an open surface (a relief without a base)
    /// the outer border, the longest loop, is left open.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="maxLoop">Loops with more border edges than this are left open; 0 for no limit.</param>
    /// <returns>The mesh with its holes closed, and how many were.</returns>
    public static (Mesh Mesh, int Filled) FillHoles(Mesh mesh, int maxLoop = 0)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        int[] t = mesh.Triangles;
        HashSet<(int, int)> halfEdges = [];
        for (int i = 0; i < t.Length; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                halfEdges.Add((t[i + k], t[i + ((k + 1) % 3)]));
            }
        }

        // A border half-edge has no twin; following them from vertex to vertex walks each hole.
        Dictionary<int, Queue<int>> next = [];
        foreach ((int a, int b) in halfEdges)
        {
            if (!halfEdges.Contains((b, a)))
            {
                (next.TryGetValue(a, out Queue<int>? q) ? q : next[a] = new Queue<int>()).Enqueue(b);
            }
        }

        List<List<int>> loops = [];
        foreach (int start in next.Keys.ToList())
        {
            while (next.TryGetValue(start, out Queue<int>? outgoing) && outgoing.Count > 0)
            {
                List<int> loop = [start];
                int at = outgoing.Dequeue();
                while (at != start && loop.Count <= next.Count && next.TryGetValue(at, out Queue<int>? more) && more.Count > 0)
                {
                    loop.Add(at);
                    at = more.Dequeue();
                }

                if (at == start && loop.Count >= 3)
                {
                    loops.Add(loop);
                }
            }
        }

        if (mesh.DoubleSided && loops.Count > 0)
        {
            loops.Remove(loops.MaxBy(l => l.Count)!);
        }

        List<Vector3> positions = [.. mesh.Positions];
        List<Vector3>? colors = mesh.Colors is null ? null : [.. mesh.Colors];
        List<Vector2>? uvs = mesh.TexCoords is null ? null : [.. mesh.TexCoords];
        List<int> triangles = [.. t];
        int filled = 0;
        foreach (List<int> loop in loops.Where(l => maxLoop <= 0 || l.Count <= maxLoop))
        {
            int centre = positions.Count;
            positions.Add(Average(loop.Select(v => positions[v])));
            colors?.Add(Average(loop.Select(v => colors[v])));
            if (uvs is not null)
            {
                Vector2 sum = Vector2.Zero;
                foreach (int v in loop)
                {
                    sum += uvs[v];
                }

                uvs.Add(sum / loop.Count);
            }

            for (int i = 0; i < loop.Count; i++)
            {
                // The border runs a→b in its triangle, so the cap runs b→a.
                int a = loop[i], b = loop[(i + 1) % loop.Count];
                triangles.AddRange([b, a, centre]);
            }

            filled++;
        }

        int[] result = [.. triangles];
        return (new Mesh([.. positions], result, colors?.ToArray(), uvs?.ToArray(), mesh.Texture, !MeshImport.IsClosed(result)), filled);
    }

    /// <summary>
    /// Flattens the bottom: every vertex below the cut is raised to it, so the model stands on a
    /// flat face, which a printer's bed and a shelf need.
    /// </summary>
    /// <param name="mesh">The mesh, Y up.</param>
    /// <param name="fraction">Where the cut is, as a share of the height from the bottom.</param>
    /// <returns>The flattened mesh.</returns>
    public static Mesh FlattenBase(Mesh mesh, float fraction)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        float cut = min.Y + ((max.Y - min.Y) * Math.Clamp(fraction, 0, 0.5f));
        Vector3[] positions = [.. mesh.Positions.Select(p => p.Y < cut ? p with { Y = cut } : p)];
        return new Mesh(positions, mesh.Triangles, mesh.Colors, mesh.TexCoords, mesh.Texture, mesh.DoubleSided);
    }

    /// <summary>Turns the model about an axis through its centre.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="axis">The axis.</param>
    /// <param name="degrees">The angle, counter-clockwise looking down the axis.</param>
    /// <returns>The turned mesh.</returns>
    public static Mesh Rotate(Mesh mesh, Axis axis, float degrees)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Vector3 centre = (min + max) / 2;
        float radians = degrees * MathF.PI / 180;
        Matrix4x4 turn = axis switch
        {
            Axis.X => Matrix4x4.CreateRotationX(radians),
            Axis.Y => Matrix4x4.CreateRotationY(radians),
            _ => Matrix4x4.CreateRotationZ(radians),
        };
        return mesh.Transformed(Matrix4x4.CreateTranslation(-centre) * turn * Matrix4x4.CreateTranslation(centre));
    }

    /// <summary>Mirrors the model left to right (X), keeping its triangles facing out.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The mirrored mesh.</returns>
    public static Mesh Mirror(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        float centre = (min.X + max.X) / 2;
        return mesh.Transformed(Matrix4x4.CreateTranslation(-centre, 0, 0) * Matrix4x4.CreateScale(-1, 1, 1) * Matrix4x4.CreateTranslation(centre, 0, 0));
    }

    private static Vector3 Average(IEnumerable<Vector3> values)
    {
        Vector3 sum = Vector3.Zero;
        int count = 0;
        foreach (Vector3 v in values)
        {
            sum += v;
            count++;
        }

        return count == 0 ? sum : sum / count;
    }

    /// <summary>A symmetric 4 × 4 error quadric: the sum of squared distances to a set of planes.</summary>
    private readonly record struct Quadric(double A, double B, double C, double D, double E, double F, double G, double H, double I, double J)
    {
        public static Quadric Plane(Vector3 n, float d, float weight)
        {
            double a = n.X, b = n.Y, c = n.Z, w = weight;
            return new(w * a * a, w * a * b, w * a * c, w * a * d, w * b * b, w * b * c, w * b * d, w * c * c, w * c * d, w * (double)d * d);
        }

        public static Quadric operator +(Quadric p, Quadric q) =>
            new(p.A + q.A, p.B + q.B, p.C + q.C, p.D + q.D, p.E + q.E, p.F + q.F, p.G + q.G, p.H + q.H, p.I + q.I, p.J + q.J);

        public double Error(Vector3 v)
        {
            double x = v.X, y = v.Y, z = v.Z;
            return (A * x * x) + (2 * B * x * y) + (2 * C * x * z) + (2 * D * x) + (E * y * y) + (2 * F * y * z) + (2 * G * y) + (H * z * z) + (2 * I * z) + J;
        }
    }
}
