using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

/// <summary>MOD142: a TrueType font's outlines read with no library, and filled into relief text.</summary>
public sealed class TrueTypeFontTests
{
    private static readonly string Arial = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");

    [Fact]
    public void Glyphs_come_back_as_closed_contours_with_their_holes_and_advances()
    {
        Assert.SkipUnless(File.Exists(Arial), "no arial.ttf on this machine");
        TrueTypeFont font = TrueTypeFont.Load(Arial);

        Assert.True(font.Has('a'));
        Assert.True(font.Has('é'));
        Assert.Equal(2, font.Outline('O').Count);   // the outer ring and the counter
        Assert.Single(font.Outline('l'));
        Assert.Empty(font.Outline(' '));
        Assert.True(font.Advance('m') > font.Advance('i'));
        float capital = font.Outline('H').SelectMany(p => p).Max(v => v.Y);
        Assert.InRange(capital / font.UnitsPerEm, 0.6f, 0.8f);
    }

    [Fact]
    public void A_lower_case_name_in_a_font_around_a_medallion_is_raised_and_the_relief_stays_closed()
    {
        Assert.SkipUnless(File.Exists(Arial), "no arial.ttf on this machine");
        const int side = 240;
        float[] depth = new float[side * side];
        float[] mask = Outline.Mask(side, side, 1, OutlineShape.Circle);

        Mesh mesh = Relief.Build(depth, side, side, new RgbaImage(side, side), mask, new ReliefOptions(
            GridLongSide: side, Depth: 3, BaseThickness: 2, SmoothPasses: 0, PrintWidth: 60,
            Text: new ReliefText("Ana é", Height: 6, Raise: 1, Placement: TextPlacement.Arc, Font: Arial)));

        Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));
        Vector3[] raised = [.. mesh.Positions.Where(p => MathF.Abs(p.Z - 4) < 1e-4f)];
        Assert.NotEmpty(raised);
        Assert.All(raised, p => Assert.True(p.Y < 0, "along the bottom arc"));
    }

    [Fact]
    public void A_character_the_font_lacks_is_refused_in_words_before_a_run()
    {
        Assert.SkipUnless(File.Exists(Arial), "no arial.ttf on this machine");
        ArgumentException refused = Assert.Throws<ArgumentException>(() =>
            ReliefTextStamp.Check(new ReliefText("Ana 中", Height: 6, Font: Arial), 60, 40, 0));

        Assert.Contains("is not in arial.ttf", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_that_is_not_a_font_is_refused()
    {
        string path = Path.Combine(Path.GetTempPath(), $"not-a-font-{Guid.NewGuid():N}.ttf");
        File.WriteAllBytes(path, [0, 1, 0, 0, 0, 0]);
        try
        {
            Assert.Throws<InvalidDataException>(() => TrueTypeFont.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
