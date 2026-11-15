using System.Numerics;
using Modelado.Geometry;

namespace Modelado.Geometry.Tests;

/// <summary>MOD112: lay flat turns a model onto its largest flat face, down the slicer's -Z.</summary>
public sealed class LayFlatTests
{
    // A box 40 × 20 × 5, so its two largest faces are 40 × 20.
    private static Mesh Slab()
    {
        Vector3[] p =
        [
            new(0, 0, 0), new(40, 0, 0), new(40, 20, 0), new(0, 20, 0),
            new(0, 0, 5), new(40, 0, 5), new(40, 20, 5), new(0, 20, 5),
        ];
        int[] t = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7];
        return new Mesh(p, t);
    }

    // A square pyramid: a 30 × 30 base and four slanted sides, each smaller than the base.
    private static Mesh Pyramid()
    {
        Vector3[] p = [new(0, 0, 0), new(30, 0, 0), new(30, 30, 0), new(0, 30, 0), new(15, 15, 10)];
        int[] t = [0, 2, 1, 0, 3, 2, 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4];
        return new Mesh(p, t);
    }

    private static Matrix4x4 Tumbled => Matrix4x4.CreateRotationX(0.7f) * Matrix4x4.CreateRotationY(-1.1f) * Matrix4x4.CreateRotationZ(0.4f);

    [Fact]
    public void A_tumbled_slab_comes_to_rest_on_a_large_face_with_its_thickness_up()
    {
        (Mesh laid, float area) = LayFlat.Apply(Slab().Transformed(Tumbled));

        Assert.Equal(800, area, 0.5f);
        (Vector3 min, Vector3 max) = laid.Bounds();
        Assert.Equal(5, max.Z - min.Z, 0.01f);
        Assert.Equal(4, laid.Positions.Count(v => MathF.Abs(v.Z - min.Z) < 1e-3f));
    }

    [Fact]
    public void A_pyramid_rests_on_its_base_not_on_a_side()
    {
        (Mesh laid, float area) = LayFlat.Apply(Pyramid().Transformed(Tumbled));

        Assert.Equal(900, area, 0.5f);
        (Vector3 min, Vector3 max) = laid.Bounds();
        Assert.Equal(10, max.Z - min.Z, 0.01f);

        // The apex is the single point at the top.
        Assert.Single(laid.Positions, v => MathF.Abs(v.Z - max.Z) < 1e-3f);
    }

    [Fact]
    public void A_model_with_no_flat_face_is_left_as_it_was()
    {
        // A tetrahedron with one face missing has faces, so take a single upturned triangle pair forming a V: no plane supports it.
        Vector3[] p = [new(0, 0, 0), new(10, 0, 5), new(10, 10, 5), new(-10, 0, 5), new(-10, 10, 5)];
        Mesh v = new(p, [0, 1, 2, 0, 4, 3]);

        (Mesh laid, float area) = LayFlat.Apply(v);

        Assert.Equal(0, area);
        Assert.Same(v, laid);
    }
}
