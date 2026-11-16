using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

/// <summary>
/// MOD114: a relief cut along an object's irregular outline (Object only, keychains, medallions) is
/// still one closed two-manifold solid: every edge used by two triangles, and every vertex's
/// triangles one fan around it, never two pieces meeting at a point.
/// </summary>
public sealed class IrregularMaskTests
{
    private const int Side = 48;

    private static float[] Depth()
    {
        float[] depth = new float[Side * Side];
        for (int i = 0; i < depth.Length; i++)
        {
            depth[i] = ((i % Side) + (i / Side)) / (2f * Side);
        }

        return depth;
    }

    private static float[] Mask(Func<int, int, bool> inside)
    {
        float[] mask = new float[Side * Side];
        for (int y = 0; y < Side; y++)
        {
            for (int x = 0; x < Side; x++)
            {
                mask[(y * Side) + x] = inside(x, y) ? 1 : 0;
            }
        }

        return mask;
    }

    // Shapes a cut-out's outline has: corners touching diagonally, a thread one pixel wide, a hole,
    // a ragged edge, and speckle.
    public static TheoryData<string> Shapes => ["diagonal", "thread", "hole", "ragged", "speckle", "checker"];

    private static float[] Shape(string name) => name switch
    {
        "diagonal" => Mask((x, y) => (x is >= 8 and < 24 && y is >= 8 and < 24) || (x is >= 23 and < 40 && y is >= 23 and < 40)),
        "thread" => Mask((x, y) => (x is >= 4 and < 18 && y is >= 4 and < 44) || (x is >= 30 and < 44 && y is >= 4 and < 44) || (y == 24 && x is >= 18 and < 30)),
        "hole" => Mask((x, y) => x is >= 4 and < 44 && y is >= 4 and < 44 && !(x is >= 20 and < 28 && y is >= 20 and < 28)),
        "ragged" => Mask((x, y) => ((x - 24) * (x - 24)) + ((y - 24) * (y - 24)) < (18 + (3 * MathF.Sin(x * 1.7f) * MathF.Cos(y * 2.3f))) * 18),
        "speckle" => Mask((x, y) => x is >= 4 and < 44 && y is >= 4 and < 44 && ((x * 7919) + (y * 104729)) % 11 != 0),
        "checker" => Mask((x, y) => x is >= 10 and < 38 && y is >= 10 and < 38 && ((x / 2) + (y / 2)) % 2 == 0),
        _ => throw new ArgumentException(name, nameof(name)),
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void A_relief_cut_to_an_irregular_outline_is_a_closed_two_manifold_solid(string shape)
    {
        Mesh mesh = Relief.Build(Depth(), Side, Side, new RgbaImage(Side, Side), Shape(shape),
            new ReliefOptions(GridLongSide: Side, Depth: 3, BaseThickness: 2, SmoothPasses: 0, PrintWidth: 40));

        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Assert.True(mesh.SignedVolume() > 0, "faces point outward");
        Assert.Empty(PinchedVertices(mesh));
    }

    // Vertices whose triangles fall into more than one fan: two pieces of surface meeting at a point.
    internal static List<int> PinchedVertices(Mesh mesh)
    {
        int[] t = mesh.Triangles;
        Dictionary<int, List<(int A, int B)>> around = [];
        for (int k = 0; k < t.Length; k += 3)
        {
            for (int e = 0; e < 3; e++)
            {
                int v = t[k + e], a = t[k + ((e + 1) % 3)], b = t[k + ((e + 2) % 3)];
                if (!around.TryGetValue(v, out List<(int, int)>? list))
                {
                    list = [];
                    around[v] = list;
                }

                list.Add((a, b));
            }
        }

        List<int> pinched = [];
        foreach ((int v, List<(int A, int B)> fan) in around)
        {
            // Walk the fan from its first triangle; a closed fan visits every triangle once.
            Dictionary<int, int> next = [];
            foreach ((int a, int b) in fan)
            {
                next[a] = b;
            }

            int start = fan[0].A, at = start, steps = 0;
            while (next.TryGetValue(at, out int to) && steps <= fan.Count)
            {
                at = to;
                steps++;
                if (at == start)
                {
                    break;
                }
            }

            if (steps != fan.Count || at != start || next.Count != fan.Count)
            {
                pinched.Add(v);
            }
        }

        return pinched;
    }
}
