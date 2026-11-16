using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

/// <summary>MOD141: a keychain shaped like the cut-out object, its hole inside the object near its top.</summary>
public sealed class KeychainCutOutTests
{
    private const int Side = 192;

    // A round body with a thin ear on top, too thin for the hole: the hole must skip it.
    private static float[] Pet()
    {
        float[] mask = new float[Side * Side];
        for (int y = 0; y < Side; y++)
        {
            for (int x = 0; x < Side; x++)
            {
                float u = x / (Side - 1f), v = y / (Side - 1f);
                bool body = ((u - 0.5f) * (u - 0.5f)) + ((v - 0.6f) * (v - 0.6f)) <= 0.3f * 0.3f;
                bool ear = MathF.Abs(u - 0.5f) < 0.02f && v > 0.1f && v <= 0.35f;
                mask[(y * Side) + x] = body || ear ? 1 : 0;
            }
        }

        return mask;
    }

    private static int Euler(Mesh mesh) => mesh.VertexCount - MarchingCubesTests.EdgeUses(mesh).Count + mesh.TriangleCount;

    [Fact]
    public void The_hole_goes_inside_the_body_near_its_top_and_the_keychain_is_one_closed_solid_with_one_hole()
    {
        float[] pet = Pet();
        float[] mask = Keychain.CutOutMask(pet, Side, Side, 1, printWidth: 40, holeDiameter: 4);

        // What the hole took: object pixels now outside.
        List<(int X, int Y)> hole = [];
        for (int i = 0; i < mask.Length; i++)
        {
            if (pet[i] >= 0.5f && mask[i] < 0.5f)
            {
                hole.Add((i % Side, i / Side));
            }
        }

        Assert.NotEmpty(hole);
        float cy = (float)hole.Average(p => p.Y) / (Side - 1), cx = (float)hole.Average(p => p.X) / (Side - 1);
        Assert.InRange(cx, 0.45f, 0.55f);
        Assert.InRange(cy, 0.30f, 0.45f); // in the body's top, below the ear (which ends at 0.35)

        float[] depth = new float[Side * Side];
        Mesh keychain = Relief.Build(depth, Side, Side, new RgbaImage(Side, Side), mask, new ReliefOptions(GridLongSide: Side, Depth: 2, BaseThickness: 2, SmoothPasses: 0, PrintWidth: 40));
        Assert.All(MarchingCubesTests.EdgeUses(keychain).Values, uses => Assert.Equal(2, uses));
        Assert.Equal(0, Euler(keychain));
    }

    [Fact]
    public void An_object_with_no_room_for_the_hole_is_refused_in_words()
    {
        ArgumentException refused = Assert.Throws<ArgumentException>(() => Keychain.CutOutMask(Pet(), Side, Side, 1, printWidth: 10, holeDiameter: 8));
        Assert.Contains("fit nowhere inside", refused.Message, StringComparison.Ordinal);
    }
}
