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
    public void A_mask_that_keeps_nothing_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => Relief.Build(Dome(10, 10), 10, 10, Photo(10, 10), new float[100], new ReliefOptions(GridLongSide: 10)));
    }
}
