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
public sealed record ReliefText(string Text, float Height = 6, float Raise = 1, TextPlacement Placement = TextPlacement.Line);

/// <summary>
/// Text stamped into a relief's height grid (MOD106). The letters are a built-in stroke font, each
/// glyph a few polylines on a 4 × 6 grid, drawn as strokes about a seventh of the letter's height,
/// so a name or a date prints legibly from a few millimetres up. Stamping heights rather than
/// merging a second mesh keeps the relief one closed solid, as the border (MOD105) does. The text
/// sits inside the outline, clear of the border: on a line along the bottom, or along the bottom of
/// the circle a medallion is cut to. Outlines from a font file, a font the user picks, and engraved
/// rather than raised text are later work.
/// </summary>
public static class ReliefTextStamp
{
    private const float GlyphHeight = 6, Advance = 6, Margin = 1.5f;

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

        string words = Drawable(text.Text);
        (int minColumn, int maxColumn, int minRow, int maxRow) = Kept(mask, gw, gh, options.MaskThreshold);
        float cellsPerMm = Math.Max(1, maxColumn - minColumn) / printWidth;
        float centreColumn = (minColumn + maxColumn) / 2f, centreRow = (minRow + maxRow) / 2f;
        float width = printWidth, height = (maxRow - minRow) / cellsPerMm;
        float scale = text.Height / GlyphHeight;
        float length = ((words.Length * Advance) - (Advance - 4)) * scale;
        float inset = options.BorderWidth + Margin;
        float stroke = MathF.Max(text.Height / 7, 0.4f);

        // Each glyph point in millimetres around the relief's centre, y up.
        Func<float, float, Vector2> place;
        if (text.Placement == TextPlacement.Arc)
        {
            float baseline = (MathF.Min(width, height) / 2) - inset - (stroke / 2);
            float radius = baseline - (text.Height / 2);
            if (radius <= 0 || length > MathF.PI * radius)
            {
                throw new ArgumentException($"\"{words}\" does not fit around a {printWidth:0.#} mm medallion at {text.Height:0.#} mm.", nameof(options));
            }

            // Left to right along the bottom; a letter's up points to the centre.
            place = (x, y) =>
            {
                float angle = (-MathF.PI / 2) + ((x - (length / 2)) / radius);
                float r = baseline - y;
                return new Vector2(r * MathF.Cos(angle), r * MathF.Sin(angle));
            };
        }
        else
        {
            if (length > width - (2 * inset))
            {
                throw new ArgumentException($"\"{words}\" does not fit across {printWidth:0.#} mm at {text.Height:0.#} mm.", nameof(options));
            }

            float bottom = (-height / 2) + inset + (stroke / 2);
            place = (x, y) => new Vector2(x - (length / 2), bottom + y);
        }

        float top = options.Depth + text.Raise;
        float[] heights = new float[gw * gh];
        Array.Fill(heights, -1f);
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
