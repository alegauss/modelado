using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

/// <summary>MOD40: a material image made to tile and put on a model, with or without texture coordinates.</summary>
public sealed class MaterialTextureTests
{
    // Red rising left to right, blue falling, green rising top to bottom: no step inside it, and
    // its opposite edges as far apart as they can be.
    private static RgbaImage Gradient(int width = 64, int height = 48)
    {
        RgbaImage image = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = image.OffsetOf(x, y);
                image.Pixels[i] = (byte)(x * 255 / (width - 1));
                image.Pixels[i + 2] = (byte)(255 - (x * 255 / (width - 1)));
                image.Pixels[i + 1] = (byte)(y * 255 / (height - 1));
                image.Pixels[i + 3] = 255;
            }
        }

        return image;
    }

    [Fact]
    public void The_last_column_continues_into_the_first_and_the_last_row_into_the_first()
    {
        RgbaImage source = Gradient();
        int at = (source.Width - 1) * 4;
        Assert.Equal(255, Math.Abs(source.Pixels[0] - source.Pixels[at]));

        RgbaImage tile = MaterialTexture.Seamless(source);

        int edgeAcross = 0, edgeDown = 0;
        for (int y = 0; y < tile.Height; y++)
        {
            int left = tile.OffsetOf(0, y), right = tile.OffsetOf(tile.Width - 1, y);
            edgeAcross = Math.Max(edgeAcross, Enumerable.Range(0, 3).Max(c => Math.Abs(tile.Pixels[left + c] - tile.Pixels[right + c])));
        }

        for (int x = 0; x < tile.Width; x++)
        {
            int top = tile.OffsetOf(x, 0), bottom = tile.OffsetOf(x, tile.Height - 1);
            edgeDown = Math.Max(edgeDown, Enumerable.Range(0, 3).Max(c => Math.Abs(tile.Pixels[top + c] - tile.Pixels[bottom + c])));
        }

        // From 255 apart to about one pixel's step of the gradient.
        Assert.True(edgeAcross <= 40, $"left and right edges differ by {edgeAcross}");
        Assert.True(edgeDown <= 40, $"top and bottom edges differ by {edgeDown}");
    }

    [Fact]
    public void Inside_the_border_the_image_is_kept()
    {
        RgbaImage source = Gradient();
        RgbaImage tile = MaterialTexture.Seamless(source, border: 0.2f);

        int x = source.Width / 4, y = source.Height / 2;
        Assert.Equal(source.Pixels[source.OffsetOf(x, y)..(source.OffsetOf(x, y) + 4)], tile.Pixels[tile.OffsetOf(x, y)..(tile.OffsetOf(x, y) + 4)]);
    }

    [Fact]
    public void A_model_with_texture_coordinates_gets_the_tile_repeated_and_its_colours_read_again()
    {
        Mesh quad = new(
            [new(0, 0, 0), new(2, 0, 0), new(2, 1, 0), new(0, 1, 0)],
            [0, 1, 2, 0, 2, 3],
            [.. Enumerable.Repeat(new Vector3(0.5f), 4)],
            [new(0, 1), new(1, 1), new(1, 0), new(0, 0)],
            new RgbaImage(200, 100));
        RgbaImage tile = MaterialTexture.Seamless(Gradient());

        Mesh textured = MaterialTexture.Apply(quad, tile, tilesAcross: 3);

        Assert.Same(quad.Positions, textured.Positions);
        Assert.Same(quad.TexCoords, textured.TexCoords);
        Assert.Equal((tile.Width * 3, tile.Width * 3 / 2), (textured.Texture!.Width, textured.Texture.Height));
        for (int i = 0; i < 4; i++)
        {
            (float r, float g, float b) = textured.Texture.Sample(quad.TexCoords![i].X, quad.TexCoords[i].Y);
            Assert.Equal(new Vector3(r, g, b), textured.Colors![i]);
        }
    }

    [Fact]
    public void A_model_without_texture_coordinates_gets_colours_with_no_seam_between_neighbours()
    {
        Mesh sphere = MarchingCubes.Extract(MarchingCubesTests.Sphere(40, 0.6f), 0);
        // A smooth tileable pattern: any jump between neighbouring vertices is a seam the projection made.
        RgbaImage tile = new(64, 64);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                int i = tile.OffsetOf(x, y);
                tile.Pixels[i] = (byte)(127.5 + (127.5 * Math.Sin(2 * Math.PI * x / 64)));
                tile.Pixels[i + 1] = (byte)(127.5 + (127.5 * Math.Cos(2 * Math.PI * y / 64)));
                tile.Pixels[i + 2] = 128;
                tile.Pixels[i + 3] = 255;
            }
        }

        Mesh coloured = MaterialTexture.Apply(sphere, tile, tilesAcross: 1);

        Assert.Null(coloured.TexCoords);
        Assert.Same(sphere.Positions, coloured.Positions);

        // What a seam looks like: each vertex taking only the plane its normal faces most, which
        // switches from one projection to another between two neighbours.
        Vector3[] normals = sphere.ComputeNormals();
        (Vector3 min, Vector3 max) = sphere.Bounds();
        float scale = 1 / MathF.Max(max.X - min.X, MathF.Max(max.Y - min.Y, max.Z - min.Z));
        Vector3[] switched = new Vector3[sphere.VertexCount];
        for (int i = 0; i < switched.Length; i++)
        {
            Vector3 p = (sphere.Positions[i] - min) * scale, n = Vector3.Abs(normals[i]);
            (float u, float v) = n.X >= n.Y && n.X >= n.Z ? (p.Z, -p.Y) : n.Y >= n.Z ? (p.X, p.Z) : (p.X, -p.Y);
            (float r, float g, float b) = tile.Sample(u - MathF.Floor(u), v - MathF.Floor(v));
            switched[i] = new Vector3(r, g, b);
        }

        float blended = LargestStep(sphere, coloured.Colors!), seam = LargestStep(sphere, switched);
        Assert.True(blended < seam / 2, $"neighbours differ by up to {blended:0.000}; a hard switch between planes, by {seam:0.000}");
        Assert.True(coloured.Colors!.Select(c => c.X).Max() - coloured.Colors!.Select(c => c.X).Min() > 0.5f, "and the pattern shows");
    }

    private static float LargestStep(Mesh mesh, Vector3[] colours)
    {
        float largest = 0;
        for (int t = 0; t < mesh.Triangles.Length; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                largest = MathF.Max(largest, Vector3.Distance(colours[mesh.Triangles[t + k]], colours[mesh.Triangles[t + ((k + 1) % 3)]]));
            }
        }

        return largest;
    }
}
