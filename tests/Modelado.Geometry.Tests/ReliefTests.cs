using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

public sealed class ReliefTests
{
    // A dome: nearest in the middle.
    private static float[] Dome(int width, int height)
    {
        float[] depth = new float[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - (width / 2f)) / width, dy = (y - (height / 2f)) / height;
                depth[(y * width) + x] = 1 - MathF.Sqrt((dx * dx) + (dy * dy));
            }
        }

        return depth;
    }

    private static RgbaImage Photo(int width, int height)
    {
        RgbaImage photo = new(width, height);
        for (int i = 0; i < photo.Pixels.Length; i += 4)
        {
            photo.Pixels[i] = 200;
            photo.Pixels[i + 3] = 255;
        }

        return photo;
    }

    [Fact]
    public void The_grid_follows_the_photos_aspect_and_faces_the_front()
    {
        Mesh mesh = Relief.Build(Dome(64, 64), 64, 64, Photo(400, 200), default, new ReliefOptions(GridLongSide: 50, SmoothPasses: 0));

        Assert.Equal(50 * 25, mesh.VertexCount);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(1f, max.X - min.X, 1e-4f);
        Assert.Equal(0.5f, max.Y - min.Y, 0.03f);
        Assert.True(mesh.DoubleSided);
        Assert.All(mesh.ComputeNormals(), n => Assert.True(n.Z > 0));
        Assert.NotNull(mesh.Texture);
    }

    [Fact]
    public void The_nearest_point_stands_out_by_the_depth()
    {
        Mesh mesh = Relief.Build(Dome(80, 80), 80, 80, Photo(80, 80), default, new ReliefOptions(GridLongSide: 40, Depth: 0.3f, SmoothPasses: 0));

        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(0f, min.Z, 1e-4f);
        Assert.Equal(0.3f, max.Z, 1e-4f);
        Vector3 centre = mesh.Positions.MinBy(p => (p.X * p.X) + (p.Y * p.Y));
        Assert.True(centre.Z > 0.28f);
    }

    [Fact]
    public void A_base_closes_it_into_a_solid()
    {
        Mesh mesh = Relief.Build(Dome(40, 30), 40, 30, Photo(40, 30), default, new ReliefOptions(GridLongSide: 30, BaseThickness: 0.05f));

        Assert.False(mesh.DoubleSided);
        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Assert.True(mesh.SignedVolume() > 0);
        Assert.Equal(-0.05f, mesh.Bounds().Min.Z, 1e-5f);
    }

    [Theory]
    [InlineData(0.004f, true)]
    [InlineData(0.0049f, true)]
    [InlineData(0.005f, false)]
    [InlineData(0.01f, false)]
    public void A_base_under_the_threshold_the_label_calls_none_builds_an_open_relief(float share, bool open)
    {
        // MOD113: the window's "none" and the mesh agree on each side of Relief.NoBaseBelow.
        float base_ = Relief.BaseShare(share);
        Mesh mesh = Relief.Build(Dome(40, 30), 40, 30, Photo(40, 30), default, new ReliefOptions(GridLongSide: 30, BaseThickness: base_));

        Assert.Equal(open, base_ == 0);
        Assert.Equal(open, mesh.DoubleSided);
        Assert.Equal(open, MarchingCubesTests.EdgeUses(mesh).Values.Any(uses => uses == 1));
        if (!open)
        {
            Assert.Equal(-share, mesh.Bounds().Min.Z, 1e-5f);
        }
    }

    [Fact]
    public void A_mask_keeps_only_the_object_and_its_solid_is_still_closed()
    {
        float[] mask = new float[60 * 60];
        for (int y = 15; y < 45; y++)
        {
            for (int x = 20; x < 40; x++)
            {
                mask[(y * 60) + x] = 1;
            }
        }

        Mesh open = Relief.Build(Dome(60, 60), 60, 60, Photo(60, 60), mask, new ReliefOptions(GridLongSide: 60, SmoothPasses: 0));
        Mesh solid = Relief.Build(Dome(60, 60), 60, 60, Photo(60, 60), mask, new ReliefOptions(GridLongSide: 60, BaseThickness: 0.02f, SmoothPasses: 0));

        (Vector3 min, Vector3 max) = open.Bounds();
        Assert.InRange(max.X - min.X, 0.3f, 0.35f);
        Assert.All(MarchingCubesTests.EdgeUses(solid).Values, uses => Assert.Equal(2, uses));
        Assert.True(solid.SignedVolume() > 0);
    }

    [Fact]
    public void A_relief_sized_in_millimetres_measures_exactly_that()
    {
        // MOD102: 60 mm wide, 3 mm of depth on a 2 mm base, on a 2:1 photo, so 30 mm tall.
        Mesh mesh = Relief.Build(Dome(80, 40), 80, 40, Photo(400, 200), default, new ReliefOptions(GridLongSide: 80, Depth: 3, BaseThickness: 2, SmoothPasses: 0, PrintWidth: 60));

        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(60f, max.X - min.X, 1e-3f);
        Assert.Equal(30f, max.Y - min.Y, 0.5f);
        Assert.Equal(3f, max.Z, 1e-4f);
        Assert.Equal(-2f, min.Z, 1e-4f);
        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));

        // The longest side the export card proposes for it (MOD102) places it without rescaling.
        (Vector3 placedMin, Vector3 placedMax) = mesh.Placed(60).Bounds();
        Assert.Equal(60f, placedMax.X - placedMin.X, 1e-3f);
        Assert.Equal(5f, placedMax.Z - placedMin.Z, 1e-3f);
    }

    [Fact]
    public void A_cut_out_sized_in_millimetres_is_that_wide_itself()
    {
        float[] mask = new float[60 * 60];
        for (int y = 15; y < 45; y++)
        {
            for (int x = 20; x < 40; x++)
            {
                mask[(y * 60) + x] = 1;
            }
        }

        Mesh mesh = Relief.Build(Dome(60, 60), 60, 60, Photo(60, 60), mask, new ReliefOptions(GridLongSide: 60, Depth: 4, BaseThickness: 1.5f, SmoothPasses: 0, PrintWidth: 40));

        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(40f, max.X - min.X, 1e-3f);
        Assert.Equal(4f, max.Z, 1e-4f);
        Assert.Equal(-1.5f, min.Z, 1e-4f);
        Assert.True(mesh.SignedVolume() > 0);
    }

    [Fact]
    public void A_mask_that_keeps_nothing_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => Relief.Build(Dome(10, 10), 10, 10, Photo(10, 10), new float[100], new ReliefOptions(GridLongSide: 10)));
    }
}
