using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

/// <summary>MOD108: a lithophane, thin where the photo is bright and thick where it is dark, one closed panel.</summary>
public sealed class LithophaneTests
{
    // Left half white, right half black.
    private static RgbaImage Halves(int width = 80, int height = 40)
    {
        RgbaImage photo = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte v = x < width / 2 ? (byte)255 : (byte)0;
                int i = photo.OffsetOf(x, y);
                (photo.Pixels[i], photo.Pixels[i + 1], photo.Pixels[i + 2], photo.Pixels[i + 3]) = (v, v, v, 255);
            }
        }

        return photo;
    }

    [Fact]
    public void Bright_is_thin_dark_is_thick_and_the_panel_is_closed_at_its_printed_size()
    {
        Mesh panel = Lithophane.Build(Halves(), new LithophaneOptions(Width: 100, MinThickness: 0.8f, MaxThickness: 3));

        Assert.All(MarchingCubesTests.EdgeUses(panel).Values, uses => Assert.Equal(2, uses));
        (Vector3 min, Vector3 max) = panel.Bounds();
        Assert.Equal(100, max.X - min.X, 1);
        Assert.InRange(max.Y - min.Y, 49.5f, 50.5f); // within a grid cell of the photo's proportions
        Assert.Equal(3, max.Z - min.Z, 2);

        // The face over the white half stands 0.8 mm above the back, over the black half 3 mm.
        Vector3[] front = [.. panel.Positions.Where(p => p.Z > min.Z + 0.01f)];
        Assert.All(front.Where(p => p.X < min.X + 40), p => Assert.Equal(min.Z + 0.8f, p.Z, 2));
        Assert.All(front.Where(p => p.X > max.X - 40), p => Assert.Equal(min.Z + 3, p.Z, 2));
    }

    [Fact]
    public void The_grid_is_finer_than_a_nozzle_draws()
    {
        Mesh panel = Lithophane.Build(Halves(), new LithophaneOptions(Width: 100));

        float[] xs = [.. panel.Positions.Select(p => MathF.Round(p.X, 3)).Distinct().Order()];
        Assert.InRange(xs[1] - xs[0], 0.2f, 0.3f);
    }

    [Fact]
    public void Thicknesses_that_cannot_be_printed_are_refused()
    {
        Assert.Throws<ArgumentException>(() => Lithophane.Build(Halves(), new LithophaneOptions(MinThickness: 0)));
        Assert.Throws<ArgumentException>(() => Lithophane.Build(Halves(), new LithophaneOptions(MinThickness: 3, MaxThickness: 2)));
        Assert.Throws<ArgumentException>(() => Lithophane.Build(Halves(), new LithophaneOptions(Width: -1)));
    }

    private static int Euler(Mesh mesh) => mesh.VertexCount - MarchingCubesTests.EdgeUses(mesh).Count + mesh.TriangleCount;

    [Fact]
    public void A_frame_is_a_rim_at_the_thickest_around_the_panel()
    {
        // MOD145: over the white half the face is 0.8 mm thick, but not within the 4 mm rim.
        Mesh panel = Lithophane.Build(Halves(), new LithophaneOptions(Width: 100, Frame: 4));

        Assert.All(MarchingCubesTests.EdgeUses(panel).Values, uses => Assert.Equal(2, uses));
        (Vector3 min, Vector3 max) = panel.Bounds();
        Vector3[] front = [.. panel.Positions.Where(p => p.Z > min.Z + 0.01f)];
        Assert.All(front.Where(p => p.X < min.X + 3.5f || p.Y > max.Y - 3.5f), p => Assert.Equal(min.Z + 3, p.Z, 2));
        Assert.Contains(front, p => p.X < min.X + 40 && MathF.Abs(p.Z - (min.Z + 0.8f)) < 0.01f);
    }

    [Fact]
    public void A_framed_curve_is_one_closed_solid_that_stands_on_its_own()
    {
        Mesh curve = Lithophane.Build(Halves(), new LithophaneOptions(Width: 100, Shape: LithophaneShape.Curve, Arc: 120, Frame: 3));

        Assert.All(MarchingCubesTests.EdgeUses(curve).Values, uses => Assert.Equal(2, uses));
        Assert.Equal(2, Euler(curve));
        (Vector3 min, Vector3 max) = curve.Bounds();
        Assert.InRange(max.Z - min.Z, 49.5f, 50.5f); // the photo's height is now upright

        // It stands when its middle is over the footprint its bottom edge leaves on the table.
        Vector2[] footprint = [.. curve.Positions.Where(p => p.Z < min.Z + 0.01f).Select(p => new Vector2(p.X, p.Y))];
        Vector2 middle = new(curve.Positions.Average(p => p.X), curve.Positions.Average(p => p.Y));
        Assert.True(Inside(Hull(footprint), middle), $"the middle {middle} is over the footprint");
    }

    [Fact]
    public void A_cylinder_joins_its_two_edges_into_one_closed_shade_of_the_same_thickness_all_round()
    {
        Mesh shade = Lithophane.Build(Halves(), new LithophaneOptions(Width: 100, MinThickness: 0.8f, MaxThickness: 3, Shape: LithophaneShape.Cylinder, Frame: 2));

        Assert.All(MarchingCubesTests.EdgeUses(shade).Values, uses => Assert.Equal(2, uses));
        Assert.Equal(0, Euler(shade)); // a ring: one hole through it
        // The inside wall is the panel's 100 mm (within a grid cell) round; 0.8 to 3 mm out from it.
        float[] radii = [.. shade.Positions.Select(p => new Vector2(p.X, p.Y).Length())];
        Assert.Equal(100 / (2 * MathF.PI), radii.Min(), 0.1f);
        Assert.Equal(3, radii.Max() - radii.Min(), 0.01f);
    }

    [Fact]
    public void A_curve_of_no_arc_or_all_the_way_round_and_a_frame_wider_than_the_panel_are_refused()
    {
        Assert.Throws<ArgumentException>(() => Lithophane.Build(Halves(), new LithophaneOptions(Shape: LithophaneShape.Curve, Arc: 0)));
        Assert.Throws<ArgumentException>(() => Lithophane.Build(Halves(), new LithophaneOptions(Shape: LithophaneShape.Curve, Arc: 360)));
        Assert.Throws<ArgumentException>(() => Lithophane.Build(Halves(), new LithophaneOptions(Width: 100, Frame: 25)));
        Assert.Throws<ArgumentException>(() => Lithophane.Build(Halves(), new LithophaneOptions(Frame: -1)));
    }

    // Andrew's monotone chain, counter-clockwise.
    private static List<Vector2> Hull(Vector2[] points)
    {
        Vector2[] sorted = [.. points.OrderBy(p => p.X).ThenBy(p => p.Y)];
        List<Vector2> hull = [];
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            foreach (Vector2 p in sorted)
            {
                while (hull.Count >= start + 2 && Cross(hull[^2], hull[^1], p) <= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                }

                hull.Add(p);
            }

            hull.RemoveAt(hull.Count - 1);
            Array.Reverse(sorted);
        }

        return hull;
    }

    private static float Cross(Vector2 o, Vector2 a, Vector2 b) => ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));

    private static bool Inside(List<Vector2> hull, Vector2 p) =>
        hull.Count >= 3 && Enumerable.Range(0, hull.Count).All(i => Cross(hull[i], hull[(i + 1) % hull.Count], p) > 0);
}
