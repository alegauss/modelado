using System.Numerics;

namespace Modelado.Geometry;

/// <summary>Repairs what a reconstruction leaves: floating specks and rough surfaces.</summary>
public static class MeshCleanup
{
    /// <summary>
    /// Drops every connected piece smaller than <paramref name="keepFraction"/> of the largest,
    /// counted in triangles: the specks of density TripoSR leaves in the air around an object.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="keepFraction">0 keeps everything, 1 keeps only the largest piece.</param>
    /// <returns>The mesh without the small pieces, its vertices renumbered.</returns>
    public static Mesh RemoveSmallPieces(Mesh mesh, double keepFraction)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.TriangleCount == 0 || keepFraction <= 0)
        {
            return mesh;
        }

        int[] parent = new int[mesh.VertexCount];
        for (int i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        int[] t = mesh.Triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            Union(parent, t[i], t[i + 1]);
            Union(parent, t[i], t[i + 2]);
        }

        Dictionary<int, int> trianglesPerPiece = [];
        for (int i = 0; i < t.Length; i += 3)
        {
            int root = Find(parent, t[i]);
            trianglesPerPiece[root] = trianglesPerPiece.GetValueOrDefault(root) + 1;
        }

        int largest = trianglesPerPiece.Values.Max();
        int floor = (int)Math.Ceiling(largest * Math.Min(keepFraction, 1.0));
        List<int> kept = new(t.Length);
        for (int i = 0; i < t.Length; i += 3)
        {
            if (trianglesPerPiece[Find(parent, t[i])] >= floor)
            {
                kept.Add(t[i]);
                kept.Add(t[i + 1]);
                kept.Add(t[i + 2]);
            }
        }

        return Compact(mesh, [.. kept]);
    }

    /// <summary>
    /// Taubin's λ|μ smoothing: alternating shrink and inflate steps that take out the staircase a
    /// grid leaves without the shrinking plain Laplacian smoothing causes.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="iterations">λ|μ pairs; 0 returns the mesh.</param>
    /// <param name="lambda">The shrink factor, 0.5 by default.</param>
    /// <param name="mu">The inflate factor, negative and slightly larger in size than λ.</param>
    /// <returns>The smoothed mesh; colours and texture coordinates are kept.</returns>
    public static Mesh Smooth(Mesh mesh, int iterations, float lambda = 0.5f, float mu = -0.53f)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (iterations <= 0 || mesh.VertexCount == 0)
        {
            return mesh;
        }

        int[][] neighbours = Neighbours(mesh);
        Vector3[] current = (Vector3[])mesh.Positions.Clone();
        Vector3[] next = new Vector3[current.Length];
        for (int pass = 0; pass < iterations * 2; pass++)
        {
            float factor = pass % 2 == 0 ? lambda : mu;
            Parallel.For(0, current.Length, i =>
            {
                int[] around = neighbours[i];
                if (around.Length == 0)
                {
                    next[i] = current[i];
                    return;
                }

                Vector3 mean = Vector3.Zero;
                foreach (int n in around)
                {
                    mean += current[n];
                }

                mean /= around.Length;
                next[i] = current[i] + (factor * (mean - current[i]));
            });
            (current, next) = (next, current);
        }

        return new Mesh(current, mesh.Triangles, mesh.Colors, mesh.TexCoords, mesh.Texture, mesh.DoubleSided);
    }

    /// <summary>Keeps only the vertices the given triangles use.</summary>
    /// <param name="mesh">The source of the vertices.</param>
    /// <param name="triangles">The triangles to keep, indexing <paramref name="mesh"/>'s vertices.</param>
    /// <returns>The compacted mesh.</returns>
    public static Mesh Compact(Mesh mesh, int[] triangles)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(triangles);
        int[] map = new int[mesh.VertexCount];
        Array.Fill(map, -1);
        int count = 0;
        int[] renumbered = new int[triangles.Length];
        for (int i = 0; i < triangles.Length; i++)
        {
            int v = triangles[i];
            if (map[v] < 0)
            {
                map[v] = count++;
            }

            renumbered[i] = map[v];
        }

        Vector3[] positions = new Vector3[count];
        Vector3[]? colors = mesh.Colors is null ? null : new Vector3[count];
        Vector2[]? uvs = mesh.TexCoords is null ? null : new Vector2[count];
        for (int v = 0; v < map.Length; v++)
        {
            int to = map[v];
            if (to < 0)
            {
                continue;
            }

            positions[to] = mesh.Positions[v];
            if (colors is not null)
            {
                colors[to] = mesh.Colors![v];
            }

            if (uvs is not null)
            {
                uvs[to] = mesh.TexCoords![v];
            }
        }

        return new Mesh(positions, renumbered, colors, uvs, mesh.Texture, mesh.DoubleSided);
    }

    /// <summary>
    /// MOD114: every vertex where pieces of surface meet at a point only (two corners of a cut-out's
    /// outline touching diagonally) split into one vertex per piece, so each vertex's triangles are
    /// one fan. Solidifying a surface pinched that way walls the point twice and leaves an edge with
    /// four faces; split, the two walls only touch, and the solid is two-manifold. Positions are not
    /// moved, colours and texture coordinates are copied.
    /// </summary>
    /// <param name="mesh">The surface.</param>
    /// <returns>The surface with its pinches split, and how many vertices were added.</returns>
    public static (Mesh Mesh, int Split) Unpinched(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        int[] t = mesh.Triangles;

        // The corners each vertex is used at, as triangle indices.
        List<int>[] around = new List<int>[mesh.VertexCount];
        for (int k = 0; k < t.Length; k++)
        {
            (around[t[k]] ??= []).Add(k / 3);
        }

        int[] triangles = (int[])t.Clone();
        List<int> copies = []; // the source vertex of each vertex added
        for (int v = 0; v < around.Length; v++)
        {
            if (around[v] is not { Count: > 1 } fan)
            {
                continue;
            }

            // Triangles around v join when they share an edge from v: union by the far end of that edge.
            int[] parent = [.. Enumerable.Range(0, fan.Count)];
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
            Dictionary<int, int> byNeighbour = [];
            for (int i = 0; i < fan.Count; i++)
            {
                int tri = fan[i] * 3;
                for (int c = 0; c < 3; c++)
                {
                    int other = t[tri + c];
                    if (other == v)
                    {
                        continue;
                    }

                    if (byNeighbour.TryGetValue(other, out int j))
                    {
                        parent[Find(i)] = Find(j);
                    }
                    else
                    {
                        byNeighbour[other] = i;
                    }
                }
            }

            // The first piece keeps v; each further piece takes a copy of it.
            Dictionary<int, int> vertexOf = [];
            for (int i = 0; i < fan.Count; i++)
            {
                int root = Find(i);
                if (!vertexOf.TryGetValue(root, out int to))
                {
                    to = vertexOf.Count == 0 ? v : mesh.VertexCount + copies.Count;
                    if (to != v)
                    {
                        copies.Add(v);
                    }

                    vertexOf[root] = to;
                }

                int tri = fan[i] * 3;
                for (int c = 0; c < 3; c++)
                {
                    if (t[tri + c] == v)
                    {
                        triangles[tri + c] = to;
                    }
                }
            }
        }

        if (copies.Count == 0)
        {
            return (mesh, 0);
        }

        Vector3[] positions = [.. mesh.Positions, .. copies.Select(v => mesh.Positions[v])];
        Vector3[]? colors = mesh.Colors is { } c0 ? [.. c0, .. copies.Select(v => c0[v])] : null;
        Vector2[]? uvs = mesh.TexCoords is { } u0 ? [.. u0, .. copies.Select(v => u0[v])] : null;
        return (new Mesh(positions, triangles, colors, uvs, mesh.Texture, mesh.DoubleSided), copies.Count);
    }

    private static int[][] Neighbours(Mesh mesh)
    {
        HashSet<int>[] sets = new HashSet<int>[mesh.VertexCount];
        int[] t = mesh.Triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int a = t[i + k], b = t[i + ((k + 1) % 3)];
                (sets[a] ??= []).Add(b);
                (sets[b] ??= []).Add(a);
            }
        }

        return [.. sets.Select(s => s is null ? [] : s.ToArray())];
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }

        return i;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int ra = Find(parent, a), rb = Find(parent, b);
        if (ra != rb)
        {
            parent[ra] = rb;
        }
    }
}
