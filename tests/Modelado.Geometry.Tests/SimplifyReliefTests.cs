using System.Diagnostics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

/// <summary>MOD62: Simplify on a relief with a base, half of which is one flat plane.</summary>
public sealed class SimplifyReliefTests
{
    // A 256-grid relief with a solid base: the shape the Studio's reliefs had. `shape` gives the
    // depth at (x, y) in [0, 1]: smooth, noisy like a photo's depth, or in steps like a building's.
    private static Mesh Relief256(Func<int, int, Random, float> shape)
    {
        const int side = 256;
        Random random = new(62);
        float[] depth = new float[side * side];
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                depth[(y * side) + x] = shape(x, y, random);
            }
        }

        return Relief.Build(depth, side, side, new RgbaImage(8, 8), default, new ReliefOptions(GridLongSide: side, BaseThickness: 0.03f));
    }

    private static float Bump(int x, int y)
    {
        float dx = (x - 128f) / 256, dy = (y - 128f) / 256;
        return MathF.Exp(-((dx * dx) + (dy * dy)) * 12);
    }

    private static Mesh BumpRelief() => Relief256((x, y, _) => Bump(x, y));

    private static Mesh NoisyRelief() => Relief256((x, y, r) => (Bump(x, y) * 0.8f) + ((float)r.NextDouble() * 0.05f));

    private static Mesh SteppedRelief() => Relief256((x, y, _) => ((x / 32) + (y / 48)) % 4 / 4f);

    private static int LargestValence(Mesh mesh)
    {
        int[] count = new int[mesh.VertexCount];
        HashSet<(int, int)> edges = [];
        for (int t = 0; t < mesh.Triangles.Length; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int a = mesh.Triangles[t + k], b = mesh.Triangles[t + ((k + 1) % 3)];
                if (edges.Add(a < b ? (a, b) : (b, a)))
                {
                    count[a]++;
                    count[b]++;
                }
            }
        }

        return count.Max();
    }

    [Theory]
    [InlineData("smooth")]
    [InlineData("noisy")]
    [InlineData("stepped")]
    public void A_relief_with_a_base_simplifies_to_a_third_without_growing_stars(string kind)
    {
        Mesh relief = kind switch { "noisy" => NoisyRelief(), "stepped" => SteppedRelief(), _ => BumpRelief() };
        Stopwatch clock = Stopwatch.StartNew();
        Mesh simpler = MeshEdit.Simplify(relief, relief.TriangleCount / 3, TestContext.Current.CancellationToken);
        clock.Stop();

        int valence = LargestValence(simpler);
        TestContext.Current.TestOutputHelper?.WriteLine($"{kind}: {relief.TriangleCount:N0} -> {simpler.TriangleCount:N0} triangles in {clock.Elapsed.TotalSeconds:0.0} s, largest valence {valence}");

        // Measured on the guest's CPU: 10 to 51 s before, with stars of up to 520 neighbours; 1.1 to 1.6 s after.
        Assert.True(simpler.TriangleCount <= relief.TriangleCount / 3, $"{kind}: reaches a third");
        Assert.True(valence <= 20, $"{kind}: no vertex keeps more than 20 neighbours, it has {valence}");
        Assert.True(clock.Elapsed.TotalSeconds < 10, $"{kind}: took {clock.Elapsed.TotalSeconds:0.0} s");
    }
}
