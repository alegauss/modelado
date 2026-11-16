using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

/// <summary>MOD104: a relief cut into a keychain tag, closed, with the hole through it.</summary>
public sealed class KeychainTests
{
    private static Mesh Tag(KeychainOptions options, int photoWidth = 120, int photoHeight = 120, float printWidth = 50)
    {
        const int side = 192;
        float[] depth = new float[side * side];
        for (int i = 0; i < depth.Length; i++)
        {
            depth[i] = (i % side) / (float)side; // a ramp, so the face is not flat
        }

        float[] mask = Keychain.Mask(side, side, (float)photoHeight / photoWidth, printWidth, options);
        return Relief.Build(depth, side, side, new RgbaImage(photoWidth, photoHeight), mask, new ReliefOptions(GridLongSide: 192, Depth: 2, BaseThickness: 2, SmoothPasses: 0, PrintWidth: printWidth));
    }

    // V - E + F: 2 for a closed surface with no hole through it, 0 for one hole.
    private static int Euler(Mesh mesh) => mesh.VertexCount - MarchingCubesTests.EdgeUses(mesh).Count + mesh.TriangleCount;

    [Theory]
    [InlineData(KeychainShape.Circle)]
    [InlineData(KeychainShape.Tag)]
    public void A_keychain_is_one_closed_solid_with_one_hole_through_it(KeychainShape shape)
    {
        Mesh mesh = Tag(new KeychainOptions(shape));

        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Assert.True(mesh.SignedVolume() > 0);
        Assert.Equal(0, Euler(mesh));
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.Equal(50f, max.X - min.X, 0.01f);
        Assert.Equal(-2f, min.Z, 1e-4f);
    }

    [Fact]
    public void The_hole_is_the_diameter_asked_near_the_top_with_a_wall_above_it()
    {
        Mesh mesh = Tag(new KeychainOptions(KeychainShape.Circle, HoleDiameter: 5));
        (Vector3 min, Vector3 max) = mesh.Bounds();
        float centreX = (min.X + max.X) / 2;

        // Down the middle from the top: the rim, then no surface across the hole, then the tag again.
        Vector3[] middle = [.. mesh.Positions.Where(p => MathF.Abs(p.X - centreX) < 0.2f && p.Z > 0)];
        float[] gaps = [.. middle.Select(p => max.Y - p.Y).Order()];
        float widest = 0, at = 0;
        for (int i = 1; i < gaps.Length; i++)
        {
            if (gaps[i] - gaps[i - 1] > widest)
            {
                (widest, at) = (gaps[i] - gaps[i - 1], gaps[i - 1]);
            }
        }

        // A triangle goes as soon as one corner is in the hole, so the opening is as wide as asked
        // and up to a grid cell more on each side (50 mm over 192 cells), never narrower: a ring fits.
        Assert.InRange(widest, 4.9f, 5.9f);
        Assert.InRange(at, Keychain.MinimumWall, 3.5f);
    }

    [Fact]
    public void A_hole_too_large_for_the_tag_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Keychain.Mask(64, 64, 1, 8, new KeychainOptions(HoleDiameter: 6)));
    }

    [Theory]
    [InlineData(KeychainShape.Tag, 0.3f, 0.6f)]
    [InlineData(KeychainShape.Circle, 0.7f, 0.5f)]
    public void A_hole_dragged_into_place_is_cut_there_and_the_keychain_stays_closed(KeychainShape shape, float across, float down)
    {
        // MOD141: the hole where it was dropped, in the photo's 0..1 coordinates.
        const int side = 192;
        float[] mask = Keychain.Mask(side, side, 1, 50, new KeychainOptions(shape, 4, across, down));
        List<(int X, int Y)> hole = [];
        float[] outline = Outline.Mask(side, side, 1, shape == KeychainShape.Circle ? OutlineShape.Circle : OutlineShape.Rounded);
        for (int i = 0; i < mask.Length; i++)
        {
            if (outline[i] >= 0.5f && mask[i] < 0.5f)
            {
                hole.Add((i % side, i / side));
            }
        }

        Assert.NotEmpty(hole);
        Assert.Equal(across, (float)hole.Average(p => p.X) / (side - 1), 0.03f);
        Assert.Equal(down, (float)hole.Average(p => p.Y) / (side - 1), 0.03f);

        Mesh mesh = Tag(new KeychainOptions(shape, 4, across, down));
        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Assert.Equal(0, Euler(mesh));
    }

    [Fact]
    public void A_hole_dropped_past_the_rim_is_kept_a_wall_inside()
    {
        Mesh mesh = Tag(new KeychainOptions(KeychainShape.Circle, 4, 1, 1));

        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Assert.Equal(0, Euler(mesh));
    }
}
