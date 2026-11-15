using System.Numerics;
using Modelado.Geometry;

namespace Modelado.Geometry.Tests;

/// <summary>MOD111: Make printable rebuilds any model closed and two-manifold on a voxel grid.</summary>
public sealed class PrintableTests
{
    // A 20 mm cube, coloured red, with one face left out when asked: a model with a hole.
    private static Mesh Cube(bool open = false, float side = 20)
    {
        Vector3[] p =
        [
            new(0, 0, 0), new(side, 0, 0), new(side, side, 0), new(0, side, 0),
            new(0, 0, side), new(side, 0, side), new(side, side, side), new(0, side, side),
        ];
        int[] faces = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7];
        int[] t = open ? faces[6..] : faces;
        return new Mesh(p, t, [.. Enumerable.Repeat(new Vector3(1, 0, 0), 8)]);
    }

    private static void ClosedAndManifold(Mesh mesh)
    {
        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Assert.True(mesh.SignedVolume() > 0, "faces point outward");
    }

    [Fact]
    public void A_cube_with_a_face_missing_comes_back_closed_at_its_size_with_its_colour()
    {
        PrintableResult made = Printable.Make(Cube(open: true), 0.5f);

        Assert.Equal(4, made.OpenEdgesBefore);
        Assert.Equal(0, made.TangledEdgesBefore);
        ClosedAndManifold(made.Mesh);
        (Vector3 min, Vector3 max) = made.Mesh.Bounds();
        Assert.InRange(max.X - min.X, 19, 21);
        Assert.InRange(made.Mesh.SignedVolume(), 7000, 8800); // 8000 mm³, the corners rounded
        Assert.All(made.Mesh.Colors!, c => Assert.Equal(new Vector3(1, 0, 0), c));
    }

    [Fact]
    public void Two_cubes_crossing_each_other_become_one_surface_with_no_edge_shared_by_three()
    {
        Mesh a = Cube(), b = Cube().Transformed(Matrix4x4.CreateTranslation(10, 10, 10));
        Mesh both = new([.. a.Positions, .. b.Positions], [.. a.Triangles, .. b.Triangles.Select(i => i + 8)], [.. a.Colors!, .. b.Colors!]);

        PrintableResult made = Printable.Make(both, 0.5f);

        ClosedAndManifold(made.Mesh);

        // The overlap counted once: 2 × 8000 − 1000 mm³.
        Assert.InRange(made.Mesh.SignedVolume(), 13500, 15500);
    }

    [Fact]
    public void A_voxel_too_small_for_the_grid_grows_and_one_not_above_zero_is_refused()
    {
        PrintableResult made = Printable.Make(Cube(side: 200), 0.01f);

        ClosedAndManifold(made.Mesh);
        Assert.Throws<ArgumentException>(() => Printable.Make(Cube(), 0));
    }
}
