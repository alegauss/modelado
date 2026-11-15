using Modelado.Imaging;

namespace Modelado.Geometry;

/// <summary>A lithophane's printed size (MOD108).</summary>
/// <param name="Width">The panel's width in millimetres; its height follows the photo.</param>
/// <param name="MinThickness">Where the photo is brightest, in millimetres: thin enough to let light through.</param>
/// <param name="MaxThickness">Where it is darkest, in millimetres: thick enough to hold light back.</param>
/// <param name="Cell">The grid's spacing in millimetres; 0.25 is finer than a 0.4 mm nozzle draws.</param>
public sealed record LithophaneOptions(
    float Width = Lithophane.WidthDefault,
    float MinThickness = Lithophane.MinThicknessDefault,
    float MaxThickness = Lithophane.MaxThicknessDefault,
    float Cell = 0.25f);

/// <summary>
/// A lithophane from a photo (MOD108): a thin panel whose thickness follows the photo's darkness, so
/// the photo appears when light shines through it from behind. No model is needed: the height is the
/// inverted brightness (Rec. 601 luma), stretched between the photo's 2nd and 98th percentiles as a
/// relief's depth is, so a flat-looking photo still uses the whole range. The relief mesher
/// (<see cref="Relief.Build"/>) does the rest: the minimum thickness is its solid base, the range
/// above it its depth, and the panel comes out one closed solid, its back flat on the plate and the
/// photo raised on its face, sized in millimetres with a grid fine enough for a 0.4 mm nozzle.
/// </summary>
public static class Lithophane
{
    /// <summary>The panel's width unless one is given, in millimetres.</summary>
    public const float WidthDefault = 100f;

    /// <summary>The thinnest the panel gets unless told otherwise, in millimetres.</summary>
    public const float MinThicknessDefault = 0.8f;

    /// <summary>The thickest the panel gets unless told otherwise, in millimetres.</summary>
    public const float MaxThicknessDefault = 3f;

    /// <summary>Builds the lithophane.</summary>
    /// <param name="photo">The photo.</param>
    /// <param name="options">Its size and thicknesses, or the defaults.</param>
    /// <returns>A closed mesh in millimetres, the panel's back at the lowest z.</returns>
    /// <exception cref="ArgumentException">A width or a thickness that is not one, or a maximum not above the minimum.</exception>
    public static Mesh Build(RgbaImage photo, LithophaneOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(photo);
        options ??= new LithophaneOptions();
        if (options.Width <= 0 || options.MinThickness <= 0 || options.MaxThickness <= options.MinThickness || options.Cell <= 0)
        {
            throw new ArgumentException("A lithophane needs a width above 0 mm, a thinnest point above 0 mm and a thickest above it.", nameof(options));
        }

        float longest = options.Width * MathF.Max(1, (float)photo.Height / photo.Width);
        int grid = Math.Clamp((int)MathF.Round(longest / options.Cell) + 1, 64, 1024);
        return Relief.Build(Darkness(photo), photo.Width, photo.Height, photo, ReadOnlySpan<float>.Empty, new ReliefOptions(
            GridLongSide: grid,
            Depth: options.MaxThickness - options.MinThickness,
            BaseThickness: options.MinThickness,
            SmoothPasses: 0,
            PrintWidth: options.Width));
    }

    /// <summary>One minus each pixel's luma, 0 for white and 1 for black.</summary>
    /// <param name="photo">The photo.</param>
    /// <returns>One value per pixel, rows top to bottom.</returns>
    public static float[] Darkness(RgbaImage photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        float[] dark = new float[photo.Width * photo.Height];
        byte[] p = photo.Pixels;
        for (int i = 0; i < dark.Length; i++)
        {
            float luma = ((0.299f * p[i * 4]) + (0.587f * p[(i * 4) + 1]) + (0.114f * p[(i * 4) + 2])) / 255f;
            dark[i] = 1 - luma;
        }

        return dark;
    }
}
