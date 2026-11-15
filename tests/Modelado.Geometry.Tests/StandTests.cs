using System.Numerics;
using Modelado.Geometry;

namespace Modelado.Geometry.Tests;

/// <summary>MOD107: a stand a printed relief slots into, one closed solid sized from the relief.</summary>
public sealed class StandTests
{
    // A 60 × 45 mm plaque, 5 mm thick, as a box.
    private static Mesh Plaque(float width = 60, float height = 45, float thickness = 5)
    {
        Vector3[] p =
        [
            new(0, 0, 0), new(width, 0, 0), new(width, height, 0), new(0, height, 0),
            new(0, 0, thickness), new(width, 0, thickness), new(width, height, thickness), new(0, height, thickness),
        ];
        int[] t = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7];
        return new Mesh(p, t);
    }

    [Fact]
    public void The_stand_is_one_closed_solid_resting_flat_on_the_plate()
    {
        Mesh stand = Stand.For(Plaque());

        Assert.All(MarchingCubesTests.EdgeUses(stand).Values, uses => Assert.Equal(2, uses));
        Assert.True(stand.SignedVolume() > 0, "faces point outward");
        (Vector3 min, Vector3 max) = stand.Bounds();
        Assert.Equal(0, min.Z, 3);
        Assert.Equal(12, max.Z, 3);
        Assert.Equal(36, max.X - min.X, 3); // 0.6 of a 60 mm plaque
    }

    [Fact]
    public void The_slot_takes_the_relief_with_play_and_leans_back()
    {
        Mesh stand = Stand.For(Plaque(thickness: 5), new StandOptions(Tilt: 15, Clearance: 0.4f));

        // The top face's two edges either side of the slot, along y at z = 12.
        float[] top = [.. stand.Positions.Where(p => MathF.Abs(p.Z - 12) < 1e-4f).Select(p => p.Y).Distinct().Order()];
        Assert.Equal(4, top.Length);
        float across = top[2] - top[1];
        Assert.Equal(5.4f / MathF.Cos(15 * MathF.PI / 180), across, 3);

        // The slot's floor sits forward of its mouth: leaning back, the relief's top is behind its foot.
        float[] floor = [.. stand.Positions.Where(p => MathF.Abs(p.Z - 4) < 1e-4f).Select(p => p.Y).Distinct().Order()];
        Assert.True(floor[0] < top[1], "the slot leans back");
    }

    [Fact]
    public void A_tall_relief_gets_a_deeper_foot_so_its_weight_stays_over_it()
    {
        Mesh low = Stand.For(Plaque(height: 40));
        Mesh tall = Stand.For(Plaque(height: 200));

        static float Depth(Mesh m) => m.Bounds().Max.Y - m.Bounds().Min.Y;
        Assert.True(Depth(tall) > Depth(low));

        // The tall plaque's centre, half its height up the lean, lands inside the foot behind the slot.
        float[] top = [.. tall.Positions.Where(p => MathF.Abs(p.Z - 12) < 1e-4f).Select(p => p.Y).Distinct().Order()];
        float centre = ((top[1] + top[2]) / 2) + (100 * MathF.Sin(15 * MathF.PI / 180));
        Assert.True(centre < tall.Bounds().Max.Y, "the centre of mass is over the foot");
    }

    [Fact]
    public void A_tilt_outside_0_to_30_degrees_is_refused_and_the_file_goes_beside_the_relief()
    {
        Assert.Throws<ArgumentException>(() => Stand.For(Plaque(), new StandOptions(Tilt: 45)));
        Assert.Equal(Path.Combine("out", "photo-stand.stl"), Stand.PathBeside(Path.Combine("out", "photo.stl")));
    }
}
