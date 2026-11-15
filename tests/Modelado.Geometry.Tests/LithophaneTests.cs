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
}
