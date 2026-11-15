namespace Modelado.Geometry;

/// <summary>The outline a keychain tag is cut to (MOD104).</summary>
public enum KeychainShape
{
    /// <summary>A disc as wide as the photo's shorter side, centred.</summary>
    Circle,

    /// <summary>The photo's rectangle with rounded corners.</summary>
    Tag,
}

/// <summary>A relief made into a keychain tag (MOD104): its outline and the hole it hangs by.</summary>
/// <param name="Shape">The outline.</param>
/// <param name="HoleDiameter">The hole's diameter in millimetres.</param>
/// <param name="HoleX">Where the hole's centre sits across the tag, 0 at the left edge and 1 at the right.</param>
public sealed record KeychainOptions(KeychainShape Shape = KeychainShape.Circle, float HoleDiameter = Keychain.HoleDiameterDefault, float HoleX = 0.5f);

/// <summary>
/// A keychain from a relief (MOD104): the tag's outline and its hole drawn as a mask over the depth
/// map, which <see cref="Relief.Build"/> keeps the inside of. A relief with a base already closes every
/// border into a wall, the hole's rim included, so the tag comes out as one closed solid with the hole
/// cut through it, and its width in millimetres (<see cref="ReliefOptions.PrintWidth"/>) is the
/// outline's. The hole sits near the top edge, a wall as thick as its own radius (at least
/// <see cref="MinimumWall"/>) between it and the rim, which is what a split ring needs.
/// </summary>
public static class Keychain
{
    /// <summary>The hole a key ring passes through, unless one is given.</summary>
    public const float HoleDiameterDefault = 4f;

    /// <summary>The thinnest wall left between the hole and the rim, in millimetres.</summary>
    public const float MinimumWall = 1.5f;

    /// <summary>
    /// The tag's mask over a depth map: 1 inside the outline and outside the hole, 0 elsewhere.
    /// </summary>
    /// <param name="width">The depth map's width.</param>
    /// <param name="height">The depth map's height.</param>
    /// <param name="photoAspect">The photo's height over its width, which the relief takes, whatever the depth map's own shape.</param>
    /// <param name="printWidth">The tag's width in millimetres, which sizes the hole and its wall.</param>
    /// <param name="options">The outline and the hole.</param>
    /// <returns>One value per depth pixel.</returns>
    /// <exception cref="ArgumentException">The hole and its wall do not fit inside the tag.</exception>
    public static float[] Mask(int width, int height, float photoAspect, float printWidth, KeychainOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 2);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(photoAspect);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(printWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.HoleDiameter);

        // Everything in the photo's own proportions: x across 0..1, y down 0..aspect. The outline's
        // width in those units is what PrintWidth millimetres becomes, so a millimetre is this long.
        OutlineShape outline = options.Shape == KeychainShape.Circle ? OutlineShape.Circle : OutlineShape.Rounded;
        float outlineWidth = Outline.Width(outline, photoAspect);
        float mm = outlineWidth / printWidth;
        float holeRadius = options.HoleDiameter / 2 * mm;
        float wall = MathF.Max(MinimumWall, options.HoleDiameter / 2) * mm;
        (float cx, float cy) = (0.5f, photoAspect / 2);
        float top = options.Shape == KeychainShape.Circle ? cy - (outlineWidth / 2) : 0;
        float holeY = top + wall + holeRadius;
        // On a circle the hole is at the top; on a tag it slides along the top edge, never nearer a
        // side than its wall.
        float holeX = options.Shape == KeychainShape.Circle ? cx : Math.Clamp(options.HoleX, wall + holeRadius, 1 - wall - holeRadius);
        if (holeY + holeRadius + wall > top + (options.Shape == KeychainShape.Circle ? outlineWidth : photoAspect))
        {
            throw new ArgumentException($"A {options.HoleDiameter:0.#} mm hole does not fit in a {printWidth:0.#} mm tag.", nameof(options));
        }

        // The outline (MOD105's shapes), less the hole.
        float[] mask = Outline.Mask(width, height, photoAspect, outline);
        for (int j = 0; j < height; j++)
        {
            float y = (float)j / (height - 1) * photoAspect;
            for (int i = 0; i < width; i++)
            {
                float x = (float)i / (width - 1);
                if (Outline.Square(x - holeX) + Outline.Square(y - holeY) <= Outline.Square(holeRadius))
                {
                    mask[(j * width) + i] = 0;
                }
            }
        }

        return mask;
    }
}
