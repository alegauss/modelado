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
