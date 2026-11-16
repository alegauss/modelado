using System.Numerics;
using Modelado.Imaging;

namespace Modelado.Geometry;

/// <summary>The form a lithophane takes (MOD145).</summary>
public enum LithophaneShape
{
    /// <summary>A flat panel, its back on the plate.</summary>
    Flat,

    /// <summary>The panel bent around a vertical axis through an arc, so it stands on its own.</summary>
    Curve,

    /// <summary>The panel bent all the way round, its two side edges joined: a shade over a tea light.</summary>
    Cylinder,
}

/// <summary>A lithophane's printed size (MOD108).</summary>
/// <param name="Width">The panel's width in millimetres; its height follows the photo. Bent, the length around the arc.</param>
/// <param name="MinThickness">Where the photo is brightest, in millimetres: thin enough to let light through.</param>
/// <param name="MaxThickness">Where it is darkest, in millimetres: thick enough to hold light back.</param>
/// <param name="Cell">The grid's spacing in millimetres; 0.25 is finer than a 0.4 mm nozzle draws.</param>
/// <param name="Shape">MOD145: flat, curved or a cylinder.</param>
/// <param name="Arc">MOD145: how far a curve bends, in degrees; a cylinder is always 360.</param>
/// <param name="Frame">MOD145: a rim this many millimetres wide at the thickest, around the panel; 0 for none.</param>
public sealed record LithophaneOptions(
    float Width = Lithophane.WidthDefault,
    float MinThickness = Lithophane.MinThicknessDefault,
    float MaxThickness = Lithophane.MaxThicknessDefault,
    float Cell = 0.25f,
    LithophaneShape Shape = LithophaneShape.Flat,
    float Arc = Lithophane.ArcDefault,
    float Frame = 0);

/// <summary>
/// A lithophane from a photo (MOD108): a thin panel whose thickness follows the photo's darkness, so
/// the photo appears when light shines through it from behind. No model is needed: the height is the
/// inverted brightness (Rec. 601 luma), stretched between the photo's 2nd and 98th percentiles as a
/// relief's depth is, so a flat-looking photo still uses the whole range. The relief mesher
/// (<see cref="Relief.Build"/>) does the rest: the minimum thickness is its solid base, the range
/// above it its depth, and the panel comes out one closed solid, its back flat on the plate and the
/// photo raised on its face, sized in millimetres with a grid fine enough for a 0.4 mm nozzle.
/// MOD145: a frame is the relief's raised border (MOD105) at the thickest, and a curve or a cylinder
/// is the finished flat panel bent around a vertical axis: each vertex's x becomes an angle, its
/// thickness a distance out from the axis, so the flat back becomes the inner wall and the photo's
/// top points up the plate's Z. A cylinder's two side edges are joined into one seam, their heights
/// evened out, so the shade is still one closed solid.
/// </summary>
public static class Lithophane
{
    /// <summary>How far a curve bends unless told otherwise, in degrees.</summary>
    public const float ArcDefault = 120f;

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

        float tall = options.Width * photo.Height / photo.Width;
        if (options.Frame < 0 || options.Frame * 2 >= MathF.Min(options.Width, tall))
        {
            throw new ArgumentException("The frame needs a width of 0 mm or more, and less than half the panel's shorter side.", nameof(options));
        }

        if (options.Shape == LithophaneShape.Curve && options.Arc is <= 0 or >= 360)
        {
            throw new ArgumentException("A curve bends through more than 0 and less than 360 degrees; all the way round is a cylinder.", nameof(options));
        }

        float longest = MathF.Max(options.Width, tall);
        int grid = Math.Clamp((int)MathF.Round(longest / options.Cell) + 1, 64, 1024);
        float depth = options.MaxThickness - options.MinThickness;
        Mesh flat = Relief.Build(Darkness(photo), photo.Width, photo.Height, photo, ReadOnlySpan<float>.Empty, new ReliefOptions(
            GridLongSide: grid,
            Depth: depth,
            BaseThickness: options.MinThickness,
            SmoothPasses: 0,
            PrintWidth: options.Width,
            BorderWidth: options.Frame,
            BorderHeight: depth));

        return options.Shape switch
        {
            LithophaneShape.Curve => Bend(flat, options.Arc, -options.MinThickness),
            LithophaneShape.Cylinder => Bend(Seamed(flat), 360, -options.MinThickness),
            _ => flat,
        };
    }

    // Each vertex's x is a length around a vertical axis, its height above the back a distance out
    // from it; the photo's y becomes the plate's Z. The middle of the panel faces -Y.
    private static Mesh Bend(Mesh flat, float arc, float backZ)
    {
        (Vector3 min, Vector3 max) = flat.Bounds();
        float radius = (max.X - min.X) / (arc * MathF.PI / 180);
        float middle = (min.X + max.X) / 2;
        Vector3[] bent = new Vector3[flat.VertexCount];
        for (int i = 0; i < bent.Length; i++)
        {
            Vector3 p = flat.Positions[i];
            float angle = (p.X - middle) / radius, r = radius + (p.Z - backZ);
            bent[i] = new Vector3(r * MathF.Sin(angle), -r * MathF.Cos(angle), p.Y);
        }

        return new Mesh(bent, flat.Triangles, flat.Colors, flat.TexCoords, flat.Texture, flat.DoubleSided);
    }

    // A cylinder's left and right edges become one seam: each right-edge vertex is replaced by the
    // left-edge vertex in the same row (front or back), the two edges' heights evened to the higher,
    // and the side walls between them, now inside the solid, dropped.
    private static Mesh Seamed(Mesh flat)
    {
        (Vector3 min, Vector3 max) = flat.Bounds();
        float tolerance = (max.X - min.X) * 1e-5f;
        Vector3[] positions = [.. flat.Positions];
        Dictionary<(float Y, bool Back), int> left = [];
        List<int> right = [];
        float backZ = min.Z;
        for (int i = 0; i < positions.Length; i++)
        {
            if (MathF.Abs(positions[i].X - min.X) <= tolerance)
            {
                left[(positions[i].Y, positions[i].Z == backZ)] = i;
            }
            else if (MathF.Abs(positions[i].X - max.X) <= tolerance)
            {
                right.Add(i);
            }
        }

        int[] map = [.. Enumerable.Range(0, positions.Length)];
        foreach (int i in right)
        {
            if (left.TryGetValue((positions[i].Y, positions[i].Z == backZ), out int twin))
            {
                map[i] = twin;
                float z = MathF.Max(positions[i].Z, positions[twin].Z);
                positions[twin] = positions[twin] with { Z = z };
            }
        }

        HashSet<int> seam = [.. left.Values];
        int[] t = flat.Triangles;
        List<int> kept = new(t.Length);
        for (int i = 0; i < t.Length; i += 3)
        {
            int a = map[t[i]], b = map[t[i + 1]], c = map[t[i + 2]];
            bool degenerate = a == b || b == c || a == c;
            if (!degenerate && !(seam.Contains(a) && seam.Contains(b) && seam.Contains(c)))
            {
                kept.AddRange([a, b, c]);
            }
        }

        Mesh evened = new(positions, flat.Triangles, flat.Colors, flat.TexCoords, flat.Texture, flat.DoubleSided);
        return MeshCleanup.Compact(evened, [.. kept]);
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
