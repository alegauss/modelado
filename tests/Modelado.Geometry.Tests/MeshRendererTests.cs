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
    public void A_saved_view_is_clear_around_the_model_and_drawn_at_the_size_asked()
    {
        // MOD19: the window's Save view as image and the CLI's --preview both go through SaveView.
        MeshRenderer renderer = new(RedSphere());
        string path = Path.Combine(Path.GetTempPath(), $"modelado-view-{Guid.NewGuid():N}.png");
        try
        {
            renderer.SaveView(path, 300, 200, new OrbitCamera(Pitch: 0));
            RgbaImage saved = ImageCodec.Load(path);

            Assert.Equal((300, 200), (saved.Width, saved.Height));
            Assert.Equal(0, saved.Pixels[saved.OffsetOf(2, 2) + 3]);
            Assert.Equal(255, saved.Pixels[saved.OffsetOf(150, 100) + 3]);
            Assert.True(saved.Pixels[saved.OffsetOf(150, 100)] > 100, "the model is drawn in its colours");
            Assert.Equal(ImageCodec.EncodePng(renderer.RenderImage(300, 200, new OrbitCamera(Pitch: 0), transparent: true)), File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_viewport_keeps_its_gradient_and_is_opaque()
    {
        MeshRenderer renderer = new(RedSphere());
        RgbaImage image = renderer.RenderImage(100, 100, new OrbitCamera());

        Assert.Equal(255, image.Pixels[image.OffsetOf(2, 2) + 3]);
    }

    [Theory]
    [InlineData(2048, 900, 600, 2048, 1365)]
    [InlineData(2048, 600, 900, 1365, 2048)]
    [InlineData(1024, 500, 500, 1024, 1024)]
    public void A_view_size_keeps_the_viewport_proportions_on_the_long_side(int side, double width, double height, int expectedWidth, int expectedHeight)
    {
        // MOD19.
        Assert.Equal((expectedWidth, expectedHeight), MeshRenderer.ViewSize(side, width, height));
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
