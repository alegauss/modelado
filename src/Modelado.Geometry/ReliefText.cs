using System.Globalization;
using System.Numerics;
using System.Text;

namespace Modelado.Geometry;

/// <summary>Where text goes on a relief (MOD106).</summary>
public enum TextPlacement
{
    /// <summary>On a straight line along the bottom, centred: under a plaque or on a tag.</summary>
    Line,

    /// <summary>Along the bottom of a circle, each letter upright toward the centre: around a medallion.</summary>
    Arc,
}

/// <summary>Text raised on a relief sized in millimetres (MOD106).</summary>
/// <param name="Text">The words: letters, digits, space and - . / '; accents are dropped, case is ignored.</param>
/// <param name="Height">A capital's height in millimetres.</param>
/// <param name="Raise">How far the text stands above the relief's highest point, in millimetres.</param>
/// <param name="Placement">A straight line or an arc.</param>
/// <param name="Font">
/// MOD142: a TrueType font file to draw the text in, filled, in its own case and characters; null
/// keeps the built-in stroke font of capitals and digits.
/// </param>
/// <param name="Engrave">
/// MOD143: cut the letters this many millimetres into the surface instead of raising them, or null
/// to raise them by <paramref name="Raise"/>.
/// </param>
public sealed record ReliefText(string Text, float Height = 6, float Raise = 1, TextPlacement Placement = TextPlacement.Line, string? Font = null, float? Engrave = null)
{
    /// <summary>MOD143: the material an engraving always leaves above the relief's back, in millimetres.</summary>
    public const float EngraveFloor = 0.6f;

    /// <summary>
    /// MOD143: where an engraved letter's floor sits on a relief whose surface is at <paramref name="surface"/>:
    /// the cut's depth below it, but never closer than <see cref="EngraveFloor"/> to the back at
    /// minus <paramref name="baseThickness"/>.
    /// </summary>
    /// <param name="surface">The relief's height there, in millimetres.</param>
    /// <param name="baseThickness">The solid base under the relief, in millimetres.</param>
    /// <returns>The floor's height.</returns>
    public float Engraved(float surface, float baseThickness) =>
        MathF.Max(surface - (Engrave ?? 0), EngraveFloor - baseThickness);
}

/// <summary>
/// Text stamped into a relief's height grid (MOD106). The letters are a built-in stroke font, each
/// glyph a few polylines on a 4 × 6 grid, drawn as strokes about a seventh of the letter's height,
/// so a name or a date prints legibly from a few millimetres up. Stamping heights rather than
/// merging a second mesh keeps the relief one closed solid, as the border (MOD105) does. The text
/// sits inside the outline, clear of the border: on a line along the bottom, or along the bottom of
/// the circle a medallion is cut to. MOD142: with <see cref="ReliefText.Font"/>, the letters are a
/// TrueType font's own outlines (<see cref="TrueTypeFont"/>), filled by a nonzero-winding test at each
/// grid vertex they cover, sized so a capital H is the height asked and bent along the arc the same
/// way the strokes are.
/// </summary>
public static class ReliefTextStamp
{
    private const float GlyphHeight = 6, Advance = 6, Margin = 1.5f;

    // Fonts read once per path: a relief may be rebuilt many times with the same one.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TrueTypeFont> Fonts = new(StringComparer.OrdinalIgnoreCase);

    private static TrueTypeFont FontAt(string path) => Fonts.GetOrAdd(Path.GetFullPath(path), TrueTypeFont.Load);

    // Polylines separated by ';', points "x,y" on a 0..4 × 0..6 grid with y up.
    private static readonly Dictionary<char, string> Glyphs = new()
    {
        ['A'] = "0,0 2,6 4,0;1,3 3,3", ['B'] = "0,0 0,6 3,6 4,5 3,3 0,3;3,3 4,2 4,1 3,0 0,0",
        ['C'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1", ['D'] = "0,0 0,6 3,6 4,4 4,2 3,0 0,0",
        ['E'] = "4,6 0,6 0,0 4,0;0,3 3,3", ['F'] = "4,6 0,6 0,0;0,3 3,3",
        ['G'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,3 2,3", ['H'] = "0,0 0,6;4,0 4,6;0,3 4,3",
        ['I'] = "1,6 3,6;2,6 2,0;1,0 3,0", ['J'] = "4,6 4,1 3,0 1,0 0,1",
        ['K'] = "0,0 0,6;4,6 0,2;1,3 4,0", ['L'] = "0,6 0,0 4,0",
        ['M'] = "0,0 0,6 2,3 4,6 4,0", ['N'] = "0,0 0,6 4,0 4,6",
        ['O'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0", ['P'] = "0,0 0,6 3,6 4,5 4,4 3,3 0,3",
        ['Q'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0;2,2 4,0", ['R'] = "0,0 0,6 3,6 4,5 4,4 3,3 0,3;2,3 4,0",
        ['S'] = "4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1", ['T'] = "0,6 4,6;2,6 2,0",
        ['U'] = "0,6 0,1 1,0 3,0 4,1 4,6", ['V'] = "0,6 2,0 4,6",
        ['W'] = "0,6 1,0 2,3 3,0 4,6", ['X'] = "0,0 4,6;0,6 4,0",
        ['Y'] = "0,6 2,3 4,6;2,3 2,0", ['Z'] = "0,6 4,6 0,0 4,0",
        ['0'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0;0,1 4,5", ['1'] = "1,5 2,6 2,0;1,0 3,0",
        ['2'] = "0,5 1,6 3,6 4,5 4,4 0,0 4,0", ['3'] = "0,5 1,6 3,6 4,5 4,4 3,3 1,3;3,3 4,2 4,1 3,0 1,0 0,1",
        ['4'] = "3,0 3,6 0,2 4,2", ['5'] = "4,6 0,6 0,3 3,3 4,2 4,1 3,0 0,0",
        ['6'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,2 3,3 0,3", ['7'] = "0,6 4,6 1,0",
        ['8'] = "1,3 0,4 0,5 1,6 3,6 4,5 4,4 3,3 1,3 0,2 0,1 1,0 3,0 4,1 4,2 3,3",
        ['9'] = "4,3 1,3 0,4 0,5 1,6 3,6 4,5 4,1 3,0 1,0 0,1",
        ['-'] = "1,3 3,3", ['.'] = "2,0 2,0.5", ['/'] = "0,0 4,6", ['\''] = "2,6 2,4", [' '] = "",
    };

    /// <summary>The text as the font draws it: accents dropped, capitals; refused when a character has no glyph.</summary>
    /// <param name="text">The words.</param>
    /// <returns>The characters drawn.</returns>
    /// <exception cref="ArgumentException">A character the font does not have.</exception>
    public static string Drawable(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder kept = new();
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            char upper = char.ToUpperInvariant(c);
            if (!Glyphs.ContainsKey(upper))
            {
                throw new ArgumentException($"\"{c}\" cannot be written on a relief: use letters, digits, spaces and - . / '.", nameof(text));
            }

            kept.Append(upper);
        }

        return kept.ToString().Trim();
    }

    /// <summary>
    /// The text's height at each grid vertex it covers, and -1 elsewhere.
    /// </summary>
    /// <param name="mask">The kept vertices' mask over the grid, or null for all of it.</param>
    /// <param name="gw">The grid's width.</param>
    /// <param name="gh">The grid's height.</param>
    /// <param name="options">The relief, with its printed width, depth, border and text.</param>
    /// <returns>Heights, or null without text.</returns>
    /// <exception cref="ArgumentException">The text does not fit.</exception>
    internal static float[]? Heights(float[]? mask, int gw, int gh, ReliefOptions options)
    {
        if (options.Text is not { } text || options.PrintWidth is not { } printWidth || string.IsNullOrWhiteSpace(text.Text))
        {
            return null;
        }

        if (!EngravingFits(text, options.Depth, options.BaseThickness))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Letters cut {text.Engrave} mm deep do not fit a relief {options.Depth} mm deep on a {options.BaseThickness} mm base: {ReliefText.EngraveFloor} mm must stay under them."),
                nameof(options));
        }

        (int minColumn, int maxColumn, int minRow, int maxRow) = Kept(mask, gw, gh, options.MaskThreshold);
        float cellsPerMm = Math.Max(1, maxColumn - minColumn) / printWidth;
        float centreColumn = (minColumn + maxColumn) / 2f, centreRow = (minRow + maxRow) / 2f;
        (string words, Func<float, float, Vector2> place, float scale, float stroke) =
            Layout(text, printWidth, (maxRow - minRow) / cellsPerMm, options.BorderWidth);

        float top = options.Depth + text.Raise;
        float[] heights = new float[gw * gh];
        Array.Fill(heights, -1f);

        // MOD142: a font's outlines, filled, each point placed as a stroke's would be.
        if (text.Font is { } path)
        {
            TrueTypeFont font = FontAt(path);
            List<Vector2[]> polygons = [];
            float pen = 0;
            foreach (char c in words)
            {
                foreach (Vector2[] contour in font.Outline(c))
                {
                    polygons.Add([.. contour.Select(v => place((pen + v.X) * scale, v.Y * scale))]);
                }

                pen += font.Advance(c);
            }

            Fill(heights, gw, gh, polygons, top, cellsPerMm, centreColumn, centreRow);
            return heights;
        }

        for (int letter = 0; letter < words.Length; letter++)
        {
            foreach (string line in Glyphs[words[letter]].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                Vector2[] points = [.. line.Split(' ').Select(p => p.Split(',')).Select(xy => place(
                    ((letter * Advance) + float.Parse(xy[0], CultureInfo.InvariantCulture)) * scale,
                    float.Parse(xy[1], CultureInfo.InvariantCulture) * scale))];
                for (int i = 1; i < points.Length; i++)
                {
                    Stamp(heights, gw, gh, points[i - 1], points[i], stroke / 2, top, cellsPerMm, centreColumn, centreRow);
                }
            }
        }

        return heights;
    }

    /// <summary>
    /// Whether the text can be written on a relief of this printed size, before one is built: the
    /// Relief card asks so it can say why before a run, not after (MOD106).
    /// </summary>
    /// <param name="text">The text and its size.</param>
    /// <param name="width">The relief's printed width in millimetres.</param>
    /// <param name="height">Its printed height in millimetres.</param>
    /// <param name="borderWidth">The raised border the text keeps clear of, in millimetres.</param>
    /// <exception cref="ArgumentException">A character the font does not have, or text that does not fit.</exception>
    public static void Check(ReliefText text, float width, float height, float borderWidth)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!string.IsNullOrWhiteSpace(text.Text))
        {
            _ = Layout(text, width, height, borderWidth);
        }
    }

    /// <summary>
    /// MOD143: whether an engraving fits a relief this deep on a base this thick: deeper than 0 mm,
    /// and no deeper than the relief and its base less the <see cref="ReliefText.EngraveFloor"/> it
    /// always leaves, so even at the relief's highest point the cut is the depth asked.
    /// </summary>
    /// <param name="text">The text, engraved or raised.</param>
    /// <param name="depth">The relief's depth in millimetres.</param>
    /// <param name="baseThickness">Its base in millimetres.</param>
    /// <returns>True for raised text or an engraving that fits.</returns>
    public static bool EngravingFits(ReliefText text, float depth, float baseThickness)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Engrave is not { } cut || (cut > 0 && cut <= depth + baseThickness - ReliefText.EngraveFloor);
    }

    /// <summary>
    /// MOD142: the first character of the text its font does not draw, or null when it draws them all
    /// (or the text is in the built-in font). The Relief card names it in its own language.
    /// </summary>
    /// <param name="text">The text and its font.</param>
    /// <returns>The character, or null.</returns>
    public static char? Lacking(ReliefText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Font is not { } path)
        {
            return null;
        }

        TrueTypeFont font = FontAt(path);
        char missing = text.Text.Normalize(NormalizationForm.FormC).FirstOrDefault(c => !char.IsWhiteSpace(c) && !font.Has(c));
        return missing == default ? null : missing;
    }

    // The characters drawn and where each glyph point lands, in millimetres around the relief's
    // centre with y up; refused when the text does not fit.
    private static (string Words, Func<float, float, Vector2> Place, float Scale, float Stroke) Layout(ReliefText text, float width, float height, float borderWidth)
    {
        if (text.Height <= 0 || text.Raise <= 0)
        {
            throw new ArgumentException("Letters need a height and a raise above 0 mm.", nameof(text));
        }

        string words;
        float scale, length;
        if (text.Font is { } path)
        {
            // MOD142: the text as typed; a character the font lacks is refused in words.
            TrueTypeFont font = FontAt(path);
            words = text.Text.Normalize(NormalizationForm.FormC).Trim();
            if (Lacking(text) is { } missing)
            {
                throw new ArgumentException($"\"{missing}\" is not in {Path.GetFileName(path)}: choose another font or leave it out.", nameof(text));
            }

            float capital = font.Outline('H').SelectMany(p => p).Select(v => v.Y).DefaultIfEmpty(font.UnitsPerEm * 0.7f).Max();
            scale = text.Height / capital;
            length = words.Sum(c => (float)font.Advance(c)) * scale;
        }
        else
        {
            words = Drawable(text.Text);
            scale = text.Height / GlyphHeight;
            length = ((words.Length * Advance) - (Advance - 4)) * scale;
        }
        float inset = borderWidth + Margin;
        float stroke = MathF.Max(text.Height / 7, 0.4f);
        if (text.Placement == TextPlacement.Arc)
        {
            float baseline = (MathF.Min(width, height) / 2) - inset - (stroke / 2);
            float radius = baseline - (text.Height / 2);
            if (radius <= 0 || length > MathF.PI * radius)
            {
                throw new ArgumentException($"\"{words}\" does not fit around a {width:0.#} mm medallion at {text.Height:0.#} mm.", nameof(text));
            }

            // Left to right along the bottom; a letter's up points to the centre.
            return (words, (x, y) =>
            {
                float angle = (-MathF.PI / 2) + ((x - (length / 2)) / radius);
                float r = baseline - y;
                return new Vector2(r * MathF.Cos(angle), r * MathF.Sin(angle));
            }, scale, stroke);
        }

        if (length > width - (2 * inset) || text.Height + stroke > height - (2 * inset))
        {
            throw new ArgumentException($"\"{words}\" does not fit across {width:0.#} mm at {text.Height:0.#} mm.", nameof(text));
        }

        float bottom = (-height / 2) + inset + (stroke / 2);
        return (words, (x, y) => new Vector2(x - (length / 2), bottom + y), scale, stroke);
    }

    // MOD142: every grid vertex inside the polygons by the nonzero rule, as TrueType fills its outlines.
    private static void Fill(float[] heights, int gw, int gh, List<Vector2[]> polygons, float top, float cellsPerMm, float centreColumn, float centreRow)
    {
        if (polygons.Count == 0)
        {
            return;
        }

        Vector2 min = new(polygons.SelectMany(p => p).Min(v => v.X), polygons.SelectMany(p => p).Min(v => v.Y));
        Vector2 max = new(polygons.SelectMany(p => p).Max(v => v.X), polygons.SelectMany(p => p).Max(v => v.Y));
        int x0 = Math.Max(0, (int)MathF.Floor(centreColumn + (min.X * cellsPerMm)));
        int x1 = Math.Min(gw - 1, (int)MathF.Ceiling(centreColumn + (max.X * cellsPerMm)));
        int y0 = Math.Max(0, (int)MathF.Floor(centreRow - (max.Y * cellsPerMm)));
        int y1 = Math.Min(gh - 1, (int)MathF.Ceiling(centreRow - (min.Y * cellsPerMm)));
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Vector2 p = new((x - centreColumn) / cellsPerMm, (centreRow - y) / cellsPerMm);
                int winding = 0;
                foreach (Vector2[] polygon in polygons)
                {
                    for (int i = 0; i < polygon.Length; i++)
                    {
                        Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                        float side = ((b.X - a.X) * (p.Y - a.Y)) - ((p.X - a.X) * (b.Y - a.Y));
                        if (a.Y <= p.Y && b.Y > p.Y && side > 0)
                        {
                            winding++;
                        }
                        else if (a.Y > p.Y && b.Y <= p.Y && side < 0)
                        {
                            winding--;
                        }
                    }
                }

                if (winding != 0)
                {
                    heights[(y * gw) + x] = top;
                }
            }
        }
    }

    // Every grid vertex within half a stroke of the segment a–b (millimetres around the centre, y up).
    private static void Stamp(float[] heights, int gw, int gh, Vector2 a, Vector2 b, float half, float top, float cellsPerMm, float centreColumn, float centreRow)
    {
        int x0 = Math.Max(0, (int)MathF.Floor(centreColumn + ((MathF.Min(a.X, b.X) - half) * cellsPerMm)));
        int x1 = Math.Min(gw - 1, (int)MathF.Ceiling(centreColumn + ((MathF.Max(a.X, b.X) + half) * cellsPerMm)));
        int y0 = Math.Max(0, (int)MathF.Floor(centreRow - ((MathF.Max(a.Y, b.Y) + half) * cellsPerMm)));
        int y1 = Math.Min(gh - 1, (int)MathF.Ceiling(centreRow - ((MathF.Min(a.Y, b.Y) - half) * cellsPerMm)));
        Vector2 ab = b - a;
        float lengthSquared = MathF.Max(ab.LengthSquared(), 1e-9f);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                Vector2 p = new((x - centreColumn) / cellsPerMm, (centreRow - y) / cellsPerMm);
                float t = Math.Clamp(Vector2.Dot(p - a, ab) / lengthSquared, 0, 1);
                if (Vector2.DistanceSquared(p, a + (t * ab)) <= half * half)
                {
                    heights[(y * gw) + x] = top;
                }
            }
        }
    }

    private static (int MinColumn, int MaxColumn, int MinRow, int MaxRow) Kept(float[]? mask, int gw, int gh, float threshold)
    {
        if (mask is null)
        {
            return (0, gw - 1, 0, gh - 1);
        }

        (int minColumn, int maxColumn, int minRow, int maxRow) = (gw, -1, gh, -1);
        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                if (mask[(y * gw) + x] >= threshold)
                {
                    (minColumn, maxColumn) = (Math.Min(minColumn, x), Math.Max(maxColumn, x));
                    (minRow, maxRow) = (Math.Min(minRow, y), Math.Max(maxRow, y));
                }
            }
        }

        return maxColumn < 0 ? (0, gw - 1, 0, gh - 1) : (minColumn, maxColumn, minRow, maxRow);
    }
}
