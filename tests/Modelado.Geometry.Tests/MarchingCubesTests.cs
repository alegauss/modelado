using System.Numerics;
using Modelado.Geometry;

namespace Modelado.Geometry.Tests;

public sealed class MarchingCubesTests
{
    internal static ScalarGrid Sphere(int resolution, float radius, Vector3 centre = default)
    {
        ScalarGrid grid = ScalarGrid.Cube(resolution, 1f);
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                for (int z = 0; z < resolution; z++)
                {
                    // Positive inside, like TripoSR's density above its threshold.
                    grid[x, y, z] = radius - Vector3.Distance(grid.PointAt(x, y, z), centre);
                }
            }
        }

        return grid;
    }

    internal static Dictionary<(int, int), int> EdgeUses(Mesh mesh)
    {
        Dictionary<(int, int), int> uses = [];
        int[] t = mesh.Triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int a = t[i + k], b = t[i + ((k + 1) % 3)];
                (int, int) key = a < b ? (a, b) : (b, a);
                uses[key] = uses.GetValueOrDefault(key) + 1;
            }
        }

        return uses;
    }

    [Fact]
    public void A_sphere_comes_out_closed_with_its_volume_and_area()
    {
        Mesh mesh = MarchingCubes.Extract(Sphere(64, 0.6f), 0);

        Assert.All(EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Assert.Equal(4 / 3.0 * Math.PI * Math.Pow(0.6, 3), mesh.SignedVolume(), 0.01);
        Assert.Equal(4 * Math.PI * 0.36, mesh.Area(), 0.05);
    }

    [Fact]
    public void Triangles_face_outward()
    {
        Mesh mesh = MarchingCubes.Extract(Sphere(32, 0.5f), 0);

        Assert.True(mesh.SignedVolume() > 0);
        Vector3[] normals = mesh.ComputeNormals();
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            Assert.True(Vector3.Dot(normals[i], mesh.Positions[i]) > 0, $"vertex {i} faces inward");
        }
    }

    [Fact]
    public void Vertices_lie_on_the_level()
    {
        Mesh mesh = MarchingCubes.Extract(Sphere(48, 0.7f), 0);

        Assert.All(mesh.Positions, p => Assert.Equal(0.7f, p.Length(), 0.01f));
    }

    [Fact]
    public void Each_crossing_edge_has_one_vertex()
    {
        Mesh mesh = MarchingCubes.Extract(Sphere(24, 0.5f), 0);

        int distinct = mesh.Positions.Select(p => (MathF.Round(p.X, 5), MathF.Round(p.Y, 5), MathF.Round(p.Z, 5))).Distinct().Count();
        Assert.Equal(mesh.VertexCount, distinct);
    }

    [Fact]
    public void A_field_entirely_outside_gives_an_empty_mesh()
    {
        Mesh mesh = MarchingCubes.Extract(Sphere(16, 0.5f), 10);

        Assert.Equal(0, mesh.TriangleCount);
    }

    [Fact]
    public void Two_blobs_are_two_pieces_and_the_small_one_can_be_dropped()
    {
        ScalarGrid grid = Sphere(48, 0.4f, new Vector3(-0.4f, 0, 0));
        ScalarGrid speck = Sphere(48, 0.1f, new Vector3(0.7f, 0.5f, 0));
        for (int i = 0; i < grid.Values.Length; i++)
        {
            grid.Values[i] = MathF.Max(grid.Values[i], speck.Values[i]);
        }

        Mesh both = MarchingCubes.Extract(grid, 0);
        Mesh cleaned = MeshCleanup.RemoveSmallPieces(both, 0.2);

        Assert.True(cleaned.TriangleCount < both.TriangleCount);
        Assert.All(cleaned.Positions, p => Assert.True(p.X < 0.1f));
        Assert.All(EdgeUses(cleaned).Values, uses => Assert.Equal(2, uses));
    }
}
