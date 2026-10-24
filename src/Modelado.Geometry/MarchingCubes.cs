using System.Numerics;

namespace Modelado.Geometry;

/// <summary>
/// Extracts the surface where a sampled field crosses a level (Lorensen and Cline's marching
/// cubes, with Bourke's tables). Each crossing edge gets one vertex, shared by every triangle
/// that touches it, so the result is an indexed mesh with no duplicate vertices.
/// </summary>
public static class MarchingCubes
{
    // Bourke's corner order, as offsets in the grid.
    private static readonly (int X, int Y, int Z)[] Corners =
    [
        (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0),
        (0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1),
    ];

    // The two corners each of the twelve edges joins.
    private static readonly (int A, int B)[] EdgeCorners =
    [
        (0, 1), (1, 2), (2, 3), (3, 0),
        (4, 5), (5, 6), (6, 7), (7, 4),
        (0, 4), (1, 5), (2, 6), (3, 7),
    ];

    /// <summary>The surface around every point whose value is above <paramref name="level"/>.</summary>
    /// <param name="grid">The field.</param>
    /// <param name="level">The iso level: above it is inside.</param>
    /// <returns>A closed mesh wherever the inside does not touch the grid's border; triangles face outward.</returns>
    public static Mesh Extract(ScalarGrid grid, float level)
    {
        ArgumentNullException.ThrowIfNull(grid);
        List<Vector3> positions = [];
        List<int> triangles = [];
        Dictionary<long, int> vertexOfEdge = [];
        Span<float> values = stackalloc float[8];
        Span<int> cellVertices = stackalloc int[12];
        float[] field = grid.Values;
        int strideX = grid.Ny * grid.Nz, strideY = grid.Nz;

        for (int x = 0; x < grid.Nx - 1; x++)
        {
            for (int y = 0; y < grid.Ny - 1; y++)
            {
                int row = (x * strideX) + (y * strideY);
                for (int z = 0; z < grid.Nz - 1; z++)
                {
                    int cube = 0;
                    for (int c = 0; c < 8; c++)
                    {
                        (int cx, int cy, int cz) = Corners[c];
                        float v = field[row + (cx * strideX) + (cy * strideY) + z + cz];
                        values[c] = v;
                        if (v <= level)
                        {
                            cube |= 1 << c; // outside
                        }
                    }

                    int edges = MarchingCubesTables.Edges[cube];
                    if (edges == 0)
                    {
                        continue;
                    }

                    for (int e = 0; e < 12; e++)
                    {
                        if ((edges & (1 << e)) != 0)
                        {
                            cellVertices[e] = VertexOn(grid, x, y, z, e, values, level, positions, vertexOfEdge);
                        }
                    }

                    int table = cube * 16;
                    for (int i = 0; MarchingCubesTables.Triangles[table + i] >= 0; i += 3)
                    {
                        // In table order Bourke's triangles wind counter-clockwise seen from the
                        // side his set bits mark, here the outside (MarchingCubesTests holds this).
                        triangles.Add(cellVertices[MarchingCubesTables.Triangles[table + i]]);
                        triangles.Add(cellVertices[MarchingCubesTables.Triangles[table + i + 1]]);
                        triangles.Add(cellVertices[MarchingCubesTables.Triangles[table + i + 2]]);
                    }
                }
            }
        }

        return new Mesh([.. positions], [.. triangles]);
    }

    private static int VertexOn(ScalarGrid grid, int x, int y, int z, int edge, ReadOnlySpan<float> values, float level, List<Vector3> positions, Dictionary<long, int> vertexOfEdge)
    {
        (int a, int b) = EdgeCorners[edge];
        (int ax, int ay, int az) = Corners[a];
        (int bx, int by, int bz) = Corners[b];
        // The edge is named by its lower end and its axis, the same from every cell that shares it.
        int lx = x + Math.Min(ax, bx), ly = y + Math.Min(ay, by), lz = z + Math.Min(az, bz);
        int axis = ax != bx ? 0 : ay != by ? 1 : 2;
        long key = ((long)grid.Index(lx, ly, lz) * 3) + axis;
        if (vertexOfEdge.TryGetValue(key, out int index))
        {
            return index;
        }

        float va = values[a], vb = values[b];
        float t = MathF.Abs(vb - va) > 1e-12f ? Math.Clamp((level - va) / (vb - va), 0f, 1f) : 0.5f;
        Vector3 pa = grid.PointAt(x + ax, y + ay, z + az);
        Vector3 pb = grid.PointAt(x + bx, y + by, z + bz);
        index = positions.Count;
        positions.Add(Vector3.Lerp(pa, pb, t));
        vertexOfEdge.Add(key, index);
        return index;
    }
}
