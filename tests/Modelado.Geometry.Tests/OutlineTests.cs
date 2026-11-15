using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

/// <summary>MOD105: a relief cropped to a medallion or a plaque, closed at the size asked.</summary>
public sealed class OutlineTests
{
    private static Mesh Cropped(OutlineShape shape, int photoWidth, int photoHeight, float printWidth)
    {
        const int side = 160;
        float[] depth = new float[side * side];
        for (int i = 0; i < depth.Length; i++)
        {
            depth[i] = (i / side) / (float)side; // a ramp down the photo
        }

        float[] mask = Outline.Mask(side, side, (float)photoHeight / photoWidth, shape);
        return Relief.Build(depth, side, side, new RgbaImage(photoWidth, photoHeight), mask, new ReliefOptions(GridLongSide: 160, Depth: 3, BaseThickness: 2, SmoothPasses: 0, PrintWidth: printWidth));
    }

    // V - E + F is 2 for one closed solid with no hole through it.
    private static int Euler(Mesh mesh) => mesh.VertexCount - MarchingCubesTests.EdgeUses(mesh).Count + mesh.TriangleCount;

    [Theory]
    [InlineData(OutlineShape.Rounded)]
    [InlineData(OutlineShape.Circle)]
    [InlineData(OutlineShape.Oval)]
    public void A_cropped_relief_is_one_closed_solid_at_the_width_asked(OutlineShape shape)
    {
        Mesh mesh = Cropped(shape, 200, 150, 70);

        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Assert.Equal(2, Euler(mesh));
        Assert.True(mesh.SignedVolume() > 0);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(70f, max.X - min.X, 0.01f);
        Assert.Equal(-2f, min.Z, 1e-4f);
    }

    [Fact]
    public void A_circle_is_as_tall_as_it_is_wide_and_an_oval_keeps_the_photos_proportions()
    {
        // A shape's tips are single grid rows or columns, so a span can fall short by about a cell
        // at each end before the scale to the printed width: within two cells (0.5 mm each).
        (Vector3 cMin, Vector3 cMax) = Cropped(OutlineShape.Circle, 200, 150, 60).Bounds();
        Assert.Equal(cMax.X - cMin.X, cMax.Y - cMin.Y, 1.1f);

        (Vector3 oMin, Vector3 oMax) = Cropped(OutlineShape.Oval, 200, 150, 80).Bounds();
        Assert.Equal(60f, oMax.Y - oMin.Y, 1.1f);
    }

    [Theory]
    [InlineData(OutlineShape.Rectangle)]
    [InlineData(OutlineShape.Circle)]
    public void A_raised_border_frames_the_relief_at_its_height_and_width_and_stays_closed(OutlineShape shape)
    {
        // A 60 mm plaque or medallion: 3 mm of relief, a 3 mm wide border standing 5 mm, on a 2 mm base.
        const int side = 160;
        float[] depth = new float[side * side];
        for (int i = 0; i < depth.Length; i++)
        {
            depth[i] = (i % side) / (float)side;
        }

        float[] mask = Outline.Mask(side, side, 1, shape);
        Mesh mesh = Relief.Build(depth, side, side, new RgbaImage(160, 160), mask, new ReliefOptions(GridLongSide: 160, Depth: 3, BaseThickness: 2, SmoothPasses: 0, PrintWidth: 60, BorderWidth: 3, BorderHeight: 5));

        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Assert.Equal(2, Euler(mesh));
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(5f, max.Z, 1e-4f);
        Assert.Equal(60f, max.X - min.X, 0.01f);

        // Along the middle row: the rim at 5 mm for about 3 mm in from each side, the relief below it.
        float centreY = (min.Y + max.Y) / 2;
        Vector3[] row = [.. mesh.Positions.Where(p => MathF.Abs(p.Y - centreY) < 0.2f && p.Z > -1).OrderBy(p => p.X)];
        float rimWidth = row.Where(p => p.X < min.X + 10 && MathF.Abs(p.Z - 5) < 1e-4f).Select(p => p.X - min.X).DefaultIfEmpty(0).Max();
        Assert.InRange(rimWidth, 2.6f, 3.5f);
        Assert.All(row.Where(p => p.X > min.X + 4 && p.X < max.X - 4), p => Assert.True(p.Z <= 3 + 1e-4f));
    }

    [Fact]
    public void A_medallion_drops_the_photos_corners()
    {
        float[] mask = Outline.Mask(100, 100, 1, OutlineShape.Circle);
        Assert.Equal(0, mask[0]);
        Assert.Equal(1, mask[(50 * 100) + 50]);
        Assert.All(Outline.Mask(100, 100, 1, OutlineShape.Rectangle), m => Assert.Equal(1, m));
    }
}
