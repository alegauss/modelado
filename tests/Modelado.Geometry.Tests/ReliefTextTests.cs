using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

/// <summary>MOD106: words raised on a relief, under a plaque or around a medallion, still one closed solid.</summary>
public sealed class ReliefTextTests
{
    private static Mesh Relief(OutlineShape shape, ReliefText text, float border = 0)
    {
        const int side = 240;
        float[] depth = new float[side * side];
        for (int i = 0; i < depth.Length; i++)
        {
            depth[i] = (i % side) / (float)side;
        }

        float[] mask = Outline.Mask(side, side, 1, shape);
        return Geometry.Relief.Build(depth, side, side, new RgbaImage(240, 240), mask, new ReliefOptions(GridLongSide: 240, Depth: 3, BaseThickness: 2, SmoothPasses: 0, PrintWidth: 60, BorderWidth: border, BorderHeight: 5, Text: text));
    }

    private static int Euler(Mesh mesh) => mesh.VertexCount - MarchingCubesTests.EdgeUses(mesh).Count + mesh.TriangleCount;

    [Fact]
    public void A_date_under_a_plaque_stands_at_its_height_along_the_bottom_and_the_plaque_stays_closed()
    {
        Mesh mesh = Relief(OutlineShape.Rounded, new ReliefText("12/11/2026", Height: 5, Raise: 1));

        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Assert.Equal(2, Euler(mesh));
        Vector3[] raised = [.. mesh.Positions.Where(p => MathF.Abs(p.Z - 4) < 1e-4f)];
        Assert.NotEmpty(raised);
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.All(raised, p => Assert.InRange(p.Y, min.Y + 1, min.Y + 9));
        Assert.InRange(raised.Max(p => p.X) - raised.Min(p => p.X), 40, 56);
    }

    [Fact]
    public void A_name_around_a_medallion_follows_its_bottom_arc_inside_the_border()
    {
        Mesh mesh = Relief(OutlineShape.Circle, new ReliefText("JOÃO", Height: 5, Raise: 1, Placement: TextPlacement.Arc), border: 2);

        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Vector3[] raised = [.. mesh.Positions.Where(p => MathF.Abs(p.Z - 4) < 1e-4f)];
        Assert.NotEmpty(raised);
        Assert.All(raised, p =>
        {
            float r = new Vector2(p.X, p.Y).Length();
            Assert.InRange(r, 30 - 2 - 1.5f - 6.5f, 30 - 2 - 1.2f);
            Assert.True(p.Y < 0, "below the centre");
        });
    }

    [Fact]
    public void Accents_are_dropped_and_a_character_the_font_lacks_is_refused_in_words()
    {
        Assert.Equal("JOAO E ANA", ReliefTextStamp.Drawable("João e Ana"));
        ArgumentException refused = Assert.Throws<ArgumentException>(() => ReliefTextStamp.Drawable("A ♥ B"));
        Assert.Contains("letters, digits", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_too_long_for_the_relief_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Relief(OutlineShape.Rectangle, new ReliefText("A VERY LONG NAME INDEED", Height: 8)));
    }

    [Fact]
    public void The_card_can_ask_whether_text_fits_before_a_relief_is_built()
    {
        ReliefTextStamp.Check(new ReliefText("ANA 2026", Height: 5), 60, 40, 0);
        ReliefTextStamp.Check(new ReliefText("JOAO", Height: 5, Placement: TextPlacement.Arc), 60, 60, 2);
        ReliefTextStamp.Check(new ReliefText("   ", Height: 5), 10, 10, 0);
        Assert.Throws<ArgumentException>(() => ReliefTextStamp.Check(new ReliefText("A VERY LONG NAME INDEED", Height: 8), 60, 40, 0));
        Assert.Throws<ArgumentException>(() => ReliefTextStamp.Check(new ReliefText("ANA", Height: 20), 60, 15, 0));
        Assert.Throws<ArgumentException>(() => ReliefTextStamp.Check(new ReliefText("ANA", Height: 0), 60, 40, 0));
        Assert.Throws<ArgumentException>(() => ReliefTextStamp.Check(new ReliefText("A ♥ B", Height: 5), 60, 40, 0));
    }
}
