namespace Modelado.Geometry;

/// <summary>The outline a keychain tag is cut to (MOD104).</summary>
public enum KeychainShape
{
    /// <summary>A disc as wide as the photo's shorter side, centred.</summary>
    Circle,

    /// <summary>The photo's rectangle with rounded corners.</summary>
    Tag,

    /// <summary>MOD141: the cut-out object's own outline (Object only), the hole inside it near its top.</summary>
    CutOut,
}

/// <summary>A relief made into a keychain tag (MOD104): its outline and the hole it hangs by.</summary>
/// <param name="Shape">The outline.</param>
/// <param name="HoleDiameter">The hole's diameter in millimetres.</param>
/// <param name="HoleX">Where the hole's centre sits across the tag, 0 at the left edge and 1 at the right.</param>
/// <param name="HoleY">
/// MOD141: where it sits down the photo, 0 at the top and 1 at the bottom, when it was dragged into
/// place; null keeps it near the top edge. Either way it stays a wall away from the outline.
/// </param>
public sealed record KeychainOptions(KeychainShape Shape = KeychainShape.Circle, float HoleDiameter = Keychain.HoleDiameterDefault, float HoleX = 0.5f, float? HoleY = null);

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

        // MOD141: dragged into place, kept a wall inside the tag's edges or the disc's rim.
        if (options.HoleY is { } down)
        {
            if (options.Shape == KeychainShape.Circle)
            {
                float room = (outlineWidth / 2) - wall - holeRadius;
                (float ox, float oy) = (options.HoleX - cx, (down * photoAspect) - cy);
                float far = MathF.Sqrt((ox * ox) + (oy * oy));
                float pull = far > room && far > 0 ? room / far : 1;
                (holeX, holeY) = (cx + (ox * pull), cy + (oy * pull));
            }
            else
            {
                holeY = Math.Clamp(down * photoAspect, wall + holeRadius, photoAspect - wall - holeRadius);
            }
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

    /// <summary>
    /// MOD141: a keychain shaped like the cut-out object: its own mask, less a hole placed inside it
    /// near its top, where a disc of the hole plus its wall fits wholly within the object. The
    /// distance from each pixel to the outside is a two-pass chamfer transform in the photo's own
    /// proportions (the depth map may be stretched from it), so the hole is round on the print; the
    /// topmost pixel far enough from every edge takes the hole, the one nearest the object's middle
    /// where a row offers several.
    /// </summary>
    /// <param name="objectMask">The cut-out's mask over the depth map, 0 to 1.</param>
    /// <param name="width">The depth map's width.</param>
    /// <param name="height">The depth map's height.</param>
    /// <param name="photoAspect">The photo's height over its width.</param>
    /// <param name="printWidth">The object's printed width in millimetres, which sizes the hole and its wall.</param>
    /// <param name="holeDiameter">The hole's diameter in millimetres.</param>
    /// <param name="threshold">The mask value at or above which a pixel is the object.</param>
    /// <returns>The mask with the hole cut.</returns>
    /// <exception cref="ArgumentException">The object has no room anywhere for the hole and its wall.</exception>
    public static float[] CutOutMask(float[] objectMask, int width, int height, float photoAspect, float printWidth, float holeDiameter, float threshold = 0.5f)
    {
        ArgumentNullException.ThrowIfNull(objectMask);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(printWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(holeDiameter);
        float dx = 1f / (width - 1), dy = photoAspect / (height - 1);

        // The object's width in the photo's units is what printWidth millimetres becomes.
        (int minX, int maxX) = (width, -1);
        for (int i = 0; i < objectMask.Length; i++)
        {
            if (objectMask[i] >= threshold)
            {
                minX = Math.Min(minX, i % width);
                maxX = Math.Max(maxX, i % width);
            }
        }

        if (maxX < 0)
        {
            throw new ArgumentException("The cut-out keeps nothing to make a keychain of.", nameof(objectMask));
        }

        float mm = (maxX - minX) * dx / printWidth;
        float holeRadius = holeDiameter / 2 * mm;
        float reach = holeRadius + (MathF.Max(MinimumWall, holeDiameter / 2) * mm);

        // Distance to the nearest pixel outside the object (or past the map's edge).
        float diagonal = MathF.Sqrt((dx * dx) + (dy * dy));
        float[] distance = new float[objectMask.Length];
        for (int i = 0; i < distance.Length; i++)
        {
            distance[i] = objectMask[i] >= threshold ? float.MaxValue : 0;
        }

        float At(int x, int y) => x < 0 || y < 0 || x >= width || y >= height ? 0 : distance[(y * width) + x];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int k = (y * width) + x;
                distance[k] = MathF.Min(distance[k], MathF.Min(MathF.Min(At(x - 1, y) + dx, At(x, y - 1) + dy), MathF.Min(At(x - 1, y - 1), At(x + 1, y - 1)) + diagonal));
            }
        }

        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                int k = (y * width) + x;
                distance[k] = MathF.Min(distance[k], MathF.Min(MathF.Min(At(x + 1, y) + dx, At(x, y + 1) + dy), MathF.Min(At(x + 1, y + 1), At(x - 1, y + 1)) + diagonal));
            }
        }

        // The topmost row with room, and in it the place nearest the object's middle.
        float middle = (minX + maxX) / 2f;
        for (int y = 0; y < height; y++)
        {
            int best = -1;
            for (int x = 0; x < width; x++)
            {
                if (distance[(y * width) + x] >= reach && (best < 0 || MathF.Abs(x - middle) < MathF.Abs(best - middle)))
                {
                    best = x;
                }
            }

            if (best >= 0)
            {
                float[] mask = (float[])objectMask.Clone();
                float holeX = best * dx, holeY = y * dy;
                for (int j = 0; j < height; j++)
                {
                    for (int i = 0; i < width; i++)
                    {
                        if (Outline.Square((i * dx) - holeX) + Outline.Square((j * dy) - holeY) <= Outline.Square(holeRadius))
                        {
                            mask[(j * width) + i] = 0;
                        }
                    }
                }

                return mask;
            }
        }

        throw new ArgumentException($"A {holeDiameter:0.#} mm hole and its wall fit nowhere inside this {printWidth:0.#} mm cut-out: make it wider or the hole smaller.", nameof(holeDiameter));
    }
}
