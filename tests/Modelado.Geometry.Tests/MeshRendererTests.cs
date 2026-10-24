using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

public sealed class MeshRendererTests
{
    private static int Covered(RgbaImage image, MeshRenderer renderer)
    {
        // Pixels that are not the background gradient's grey-blue.
        int covered = 0;
        for (int i = 0; i < image.Pixels.Length; i += 4)
        {
            if (image.Pixels[i] > image.Pixels[i + 2] + 8)
            {
                covered++;
            }
        }

        return covered;
    }

    private static Mesh RedSphere()
    {
        Mesh sphere = MarchingCubes.Extract(MarchingCubesTests.Sphere(32, 0.5f), 0);
        return new Mesh(sphere.Positions, sphere.Triangles, [.. sphere.Positions.Select(_ => new Vector3(1, 0, 0))]);
    }

    [Fact]
    public void A_sphere_fills_a_disc_in_the_middle()
    {
        MeshRenderer renderer = new(RedSphere());
        RgbaImage image = renderer.RenderImage(200, 200, new OrbitCamera(Pitch: 0));

        int covered = Covered(image, renderer);
        // Zoom 1 fits the bounding sphere (radius √3/2 of the half-side) in the view.
        Assert.InRange(covered, 8000, 26000);
        Assert.True(image.Pixels[image.OffsetOf(100, 100)] > 100, "the centre is lit red");
        Assert.True(image.Pixels[image.OffsetOf(2, 2)] < 100, "the corner is background");
    }

    [Fact]
    public void A_single_sided_surface_is_culled_from_behind()
    {
        Mesh quad = new(
            [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)],
            [0, 1, 2, 0, 2, 3],
            [new(1, 0, 0), new(1, 0, 0), new(1, 0, 0), new(1, 0, 0)]);
        MeshRenderer renderer = new(quad);

        Assert.True(Covered(renderer.RenderImage(100, 100, new OrbitCamera(Yaw: 0, Pitch: 0)), renderer) > 1000);
        Assert.Equal(0, Covered(renderer.RenderImage(100, 100, new OrbitCamera(Yaw: 180, Pitch: 0)), renderer));
    }

    [Fact]
    public void A_double_sided_surface_shows_from_behind()
    {
        Mesh quad = new(
            [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)],
            [0, 1, 2, 0, 2, 3],
            [new(1, 0, 0), new(1, 0, 0), new(1, 0, 0), new(1, 0, 0)],
            doubleSided: true);
        MeshRenderer renderer = new(quad);

        Assert.True(Covered(renderer.RenderImage(100, 100, new OrbitCamera(Yaw: 0, Pitch: 0)), renderer) > 1000);
        Assert.True(Covered(renderer.RenderImage(100, 100, new OrbitCamera(Yaw: 180, Pitch: 0)), renderer) > 1000);
    }

    [Fact]
    public void The_camera_turns_and_zooms_within_limits()
    {
        OrbitCamera camera = new OrbitCamera().Dragged(100, 1000).Zoomed(100);

        Assert.Equal(89, camera.Pitch);
        Assert.Equal(20, camera.Zoom);
        Assert.Equal(-40, camera.Yaw, 1e-3f);
    }
}
