using System.Numerics;
using Modelado.Geometry;

namespace Modelado.Geometry.Tests;

/// <summary>MOD146: the parts of a model too thin to print, found and coloured red.</summary>
public sealed class WallThicknessTests
{
    // A closed box, faces out, eight corners.
    internal static (Vector3[] Positions, int[] Triangles) Box(Vector3 low, Vector3 high, int offset = 0)
    {
        Vector3[] p =
        [
            new(low.X, low.Y, low.Z), new(high.X, low.Y, low.Z), new(high.X, high.Y, low.Z), new(low.X, high.Y, low.Z),
            new(low.X, low.Y, high.Z), new(high.X, low.Y, high.Z), new(high.X, high.Y, high.Z), new(low.X, high.Y, high.Z),
        ];
        int[] faces = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7];
        return (p, [.. faces.Select(i => i + offset)]);
    }

    // A 20 × 20 × 5 mm block with a fin 10 mm long, 0.5 mm thin and 10 mm tall standing on it.
    internal static Mesh BlockWithFin()
    {
        (Vector3[] block, int[] blockFaces) = Box(new Vector3(0, 0, 0), new Vector3(20, 20, 5));
        (Vector3[] fin, int[] finFaces) = Box(new Vector3(5, 9.75f, 5), new Vector3(15, 10.25f, 15), offset: 8);
        return new Mesh([.. block, .. fin], [.. blockFaces, .. finFaces]);
    }

    [Fact]
    public void A_half_millimetre_fin_is_red_and_nothing_else_at_one_point_two_millimetres()
    {
        ThicknessReport report = WallThickness.Check(BlockWithFin(), WallThickness.MinimumDefault);

        Assert.Equal(16, report.Vertices);
        Assert.Equal(8, report.ThinVertices);
        Assert.All(Enumerable.Range(8, 8), v => Assert.Equal(new Vector3(0.9f, 0.1f, 0.1f), report.Mesh.Colors![v]));
        Assert.All(Enumerable.Range(0, 8), v => Assert.Equal(new Vector3(0.8f, 0.8f, 0.8f), report.Mesh.Colors![v]));

        // The fin's 2 × 10 × 10 + 2 × 10 × 0.5 + 2 × 0.5 × 10 = 220 mm², beside the block's 1,200 mm².
        Assert.Equal(220.0 / 1420, report.ThinShare, 3);
    }

    [Fact]
    public void A_wall_is_measured_to_where_the_ray_leaves_it()
    {
        float[] thickness = WallThickness.Measure(BlockWithFin(), 100);

        Assert.All(thickness[8..], t => Assert.InRange(t, 0.5f, 0.6f));
        Assert.All(thickness[..8], t => Assert.InRange(t, 5, 6));
    }

    [Fact]
    public void A_thin_fin_thickened_to_the_minimum_passes_the_check_and_the_block_keeps_its_size()
    {
        // MOD150: the fin fixture at its own 20 mm, a 0.4 mm voxel, thickened to 1.2 mm.
        Mesh model = BlockWithFin();
        ThickenResult thick = Printable.Thicken(model, 1.2f, 0.4f);

        Assert.Equal(8, thick.ThinVerticesBefore);
        Assert.All(MarchingCubesTests.EdgeUses(thick.Mesh).Values, uses => Assert.Equal(2, uses));
        Assert.Equal(0, WallThickness.Check(thick.Mesh, 1.2f).ThinVertices);

        // The fin is a slab at least 1.2 mm thick now, around where it stood.
        float[] finY = [.. thick.Mesh.Positions.Where(p => p.Z > 8 && p.Z < 12 && p.X > 8 && p.X < 12).Select(p => p.Y)];
        Assert.InRange(finY.Max() - finY.Min(), 1.2f, 2.4f);
        Assert.InRange((finY.Max() + finY.Min()) / 2, 9.8f, 10.2f);

        // The block keeps its 20 × 20 × 5 within a voxel, as Make printable leaves it.
        (Vector3 low, Vector3 high) = thick.Mesh.Positions.Where(p => p.Z < 4).Aggregate((Low: new Vector3(float.MaxValue), High: new Vector3(float.MinValue)), (box, p) => (Vector3.Min(box.Low, p), Vector3.Max(box.High, p)));
        Assert.InRange(high.X - low.X, 19.6f, 20.4f);
        Assert.InRange(high.Y - low.Y, 19.6f, 20.4f);
    }

    [Fact]
    public void A_model_with_no_thin_wall_is_left_as_it_is()
    {
        (Vector3[] p, int[] t) = Box(Vector3.Zero, new Vector3(20, 20, 20));
        Mesh block = new(p, t);

        ThickenResult thick = Printable.Thicken(block, 1.2f, 0.4f);

        Assert.Same(block, thick.Mesh);
        Assert.Equal(0, thick.ThinVerticesBefore);
        Assert.Throws<ArgumentException>(() => Printable.Thicken(block, 0, 0.4f));
    }

    [Fact]
    public void A_solid_block_has_nothing_too_thin_and_keeps_its_colour_but_not_its_texture()
    {
        (Vector3[] p, int[] t) = Box(Vector3.Zero, new Vector3(20, 20, 20));
        Mesh block = new(p, t, [.. Enumerable.Repeat(new Vector3(0, 0, 1), 8)], [.. Enumerable.Repeat(Vector2.Zero, 8)], new Imaging.RgbaImage(2, 2));

        ThicknessReport report = WallThickness.Check(block, 1.2f);

        Assert.Equal(0, report.ThinVertices);
        Assert.Equal(0, report.ThinShare);
        Assert.All(report.Mesh.Colors!, c => Assert.Equal(new Vector3(0, 0, 1), c));
        Assert.Null(report.Mesh.Texture);
        Assert.Throws<ArgumentException>(() => WallThickness.Check(block, 0));
    }
}
