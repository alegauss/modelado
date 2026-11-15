namespace Modelado.Geometry;

/// <summary>The shape a relief is cropped to (MOD105).</summary>
public enum OutlineShape
{
    /// <summary>The photo's rectangle, as a relief has always been.</summary>
    Rectangle,

    /// <summary>The rectangle with rounded corners: a plaque, or a keychain tag.</summary>
    Rounded,

    /// <summary>A disc as wide as the photo's shorter side, centred: a medallion.</summary>
    Circle,

    /// <summary>The ellipse the photo's rectangle holds: an oval portrait medallion.</summary>
    Oval,
}

/// <summary>
/// The outlines a relief is cropped to (MOD105), drawn as a mask over the depth map, which
/// <see cref="Relief.Build"/> keeps the inside of, so the crop closes into walls like any border.
/// Coordinates are the photo's own proportions, x across 0..1 and y down 0..aspect, since the relief
/// takes the photo's aspect whatever the depth map's shape. The keychain (MOD104) cuts its tag from
/// the same shapes.
/// </summary>
public static class Outline
{
    /// <summary>How wide the outline is, in the photo's proportions, which a printed width scales.</summary>
    /// <param name="shape">The outline.</param>
    /// <param name="aspect">The photo's height over its width.</param>
    /// <returns>1, or the shorter side for a circle.</returns>
    public static float Width(OutlineShape shape, float aspect) => shape == OutlineShape.Circle ? MathF.Min(1, aspect) : 1;

    /// <summary>Whether a point in the photo's proportions is inside the outline.</summary>
    /// <param name="shape">The outline.</param>
    /// <param name="x">Across, 0 to 1.</param>
    /// <param name="y">Down, 0 to the aspect.</param>
    /// <param name="aspect">The photo's height over its width.</param>
    /// <returns>True inside.</returns>
    public static bool Inside(OutlineShape shape, float x, float y, float aspect)
    {
        float cx = 0.5f, cy = aspect / 2;
        switch (shape)
        {
            case OutlineShape.Circle:
                float r = Width(shape, aspect) / 2;
                return Square(x - cx) + Square(y - cy) <= Square(r);
            case OutlineShape.Oval:
                return Square((x - cx) / 0.5f) + Square((y - cy) / cy) <= 1;
            case OutlineShape.Rounded:
                return InsideRounded(x, y, 1, aspect, 0.15f * MathF.Min(1, aspect));
            default:
                return x >= 0 && x <= 1 && y >= 0 && y <= aspect;
        }
    }

    /// <summary>The outline's mask over a depth map: 1 inside, 0 outside.</summary>
    /// <param name="width">The depth map's width.</param>
    /// <param name="height">The depth map's height.</param>
    /// <param name="aspect">The photo's height over its width.</param>
    /// <param name="shape">The outline.</param>
    /// <returns>One value per depth pixel.</returns>
    public static float[] Mask(int width, int height, float aspect, OutlineShape shape)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 2);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aspect);
        float[] mask = new float[width * height];
        for (int j = 0; j < height; j++)
        {
            float y = (float)j / (height - 1) * aspect;
            for (int i = 0; i < width; i++)
            {
                mask[(j * width) + i] = Inside(shape, (float)i / (width - 1), y, aspect) ? 1 : 0;
            }
        }

        return mask;
    }

    internal static float Square(float v) => v * v;

    // Inside a w × h rectangle at the origin whose corners are rounded by r.
    private static bool InsideRounded(float x, float y, float w, float h, float r)
    {
        float dx = MathF.Max(MathF.Max(r - x, x - (w - r)), 0);
        float dy = MathF.Max(MathF.Max(r - y, y - (h - r)), 0);
        return x >= 0 && x <= w && y >= 0 && y <= h && Square(dx) + Square(dy) <= Square(r);
    }
}
