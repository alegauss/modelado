using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

public sealed class ColorProjectionTests
{
    // A grey sphere and a photo of it: a red disc on a transparent square, its left half blue.
    private static (Mesh Sphere, RgbaImage Photo) Scene()
    {
        Mesh sphere = MarchingCubes.Extract(MarchingCubesTests.Sphere(40, 0.6f), 0);
        sphere = new Mesh(sphere.Positions, sphere.Triangles, [.. sphere.Positions.Select(_ => new Vector3(0.5f))]);
        RgbaImage photo = new(200, 200);
        for (int y = 0; y < 200; y++)
        {
            for (int x = 0; x < 200; x++)
            {
                if (((x - 100) * (x - 100)) + ((y - 100) * (y - 100)) <= 80 * 80)
                {
                    int i = photo.OffsetOf(x, y);
                    photo.Pixels[i] = (byte)(x < 100 ? 0 : 255);
                    photo.Pixels[i + 2] = (byte)(x < 100 ? 255 : 0);
                    photo.Pixels[i + 3] = 255;
                }
            }
        }

        return (sphere, photo);
    }

    [Fact]
    public void The_front_takes_the_photos_colours_where_it_faces_the_camera()
    {
        (Mesh sphere, RgbaImage photo) = Scene();
        (Mesh coloured, int projected) = ColorProjection.Project(sphere, photo);

        Assert.InRange(projected, sphere.VertexCount / 5, sphere.VertexCount / 2);
        int front = Array.FindIndex(coloured.Positions, p => p.Z > 0.55f && p.X > 0.05f);
        Assert.True(coloured.Colors![front].X > 0.9f, "the right of the front is red");
        int left = Array.FindIndex(coloured.Positions, p => p.Z > 0.55f && p.X < -0.05f);
        Assert.True(coloured.Colors![left].Z > 0.9f, "the left of the front is blue");
    }

    [Fact]
    public void The_back_and_the_sides_keep_their_colours()
    {
        (Mesh sphere, RgbaImage photo) = Scene();
        Mesh coloured = ColorProjection.Project(sphere, photo).Mesh;

        for (int i = 0; i < coloured.VertexCount; i++)
        {
            if (coloured.Positions[i].Z < 0.1f)
            {
                Assert.Equal(new Vector3(0.5f), coloured.Colors![i]);
            }
        }
    }

    [Fact]
    public void Strength_zero_changes_nothing()
    {
        (Mesh sphere, RgbaImage photo) = Scene();
        Assert.Equal(sphere.Colors, ColorProjection.Project(sphere, photo, 0).Mesh.Colors);
    }
}
