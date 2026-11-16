using System.Buffers.Binary;
using System.Numerics;

namespace Modelado.Geometry;

/// <summary>
/// MOD142: the outlines of a TrueType font (a .ttf with a glyf table), read with no font library: the
/// character map (cmap formats 4 and 12), each glyph's contours from glyf through loca (simple
/// glyphs and composites), and the advance widths from hmtx. A contour's quadratic curves are
/// flattened into short segments, so a glyph comes back as closed polygons in font units, y up,
/// which <see cref="ReliefTextStamp"/> fills into the relief. An OpenType font with CFF outlines has
/// no glyf table and is refused in words.
/// </summary>
public sealed class TrueTypeFont
{
    private readonly byte[] data;
    private readonly Dictionary<string, (int Offset, int Length)> tables = [];
    private readonly Dictionary<int, int> map = [];
    private readonly int glyphCount;
    private readonly bool longLoca;
    private readonly int metricCount;

    private TrueTypeFont(byte[] data)
    {
        this.data = data;
        int count = U16(4);
        for (int i = 0; i < count; i++)
        {
            int record = 12 + (16 * i);
            string tag = System.Text.Encoding.ASCII.GetString(data, record, 4);
            tables[tag] = ((int)U32(record + 8), (int)U32(record + 12));
        }

        foreach (string needed in new[] { "head", "maxp", "cmap", "hhea", "hmtx" })
        {
            if (!tables.ContainsKey(needed))
            {
                throw new InvalidDataException($"The font has no {needed} table.");
            }
        }

        if (!tables.ContainsKey("glyf") || !tables.ContainsKey("loca"))
        {
            throw new InvalidDataException("The font keeps its outlines as CFF, not TrueType glyphs; choose a .ttf font.");
        }

        UnitsPerEm = U16(tables["head"].Offset + 18);
        longLoca = I16(tables["head"].Offset + 50) == 1;
        glyphCount = U16(tables["maxp"].Offset + 4);
        metricCount = U16(tables["hhea"].Offset + 34);
        ReadMap();
    }

    /// <summary>Font units per em.</summary>
    public int UnitsPerEm { get; }

    /// <summary>Reads a font file.</summary>
    /// <param name="path">The .ttf file.</param>
    /// <returns>The font.</returns>
    /// <exception cref="InvalidDataException">It is not a TrueType font this reads.</exception>
    public static TrueTypeFont Load(string path)
    {
        try
        {
            return new TrueTypeFont(File.ReadAllBytes(path));
        }
        catch (Exception error) when (error is IndexOutOfRangeException or ArgumentOutOfRangeException or ArgumentException)
        {
            throw new InvalidDataException($"{Path.GetFileName(path)} is not a TrueType font this can read.", error);
        }
    }

    /// <summary>Whether the font draws a character.</summary>
    /// <param name="c">The character.</param>
    /// <returns>True when it maps to a glyph.</returns>
    public bool Has(int c) => map.TryGetValue(c, out int glyph) && glyph > 0;

    /// <summary>How far the pen moves after a character, in font units.</summary>
    /// <param name="c">The character.</param>
    /// <returns>The advance width.</returns>
    public int Advance(int c)
    {
        int glyph = map.GetValueOrDefault(c);
        int index = Math.Min(glyph, metricCount - 1);
        return U16(tables["hmtx"].Offset + (4 * index));
    }

    /// <summary>A character's outline as closed polygons in font units, y up.</summary>
    /// <param name="c">The character.</param>
    /// <returns>The contours; none for a blank such as a space.</returns>
    public IReadOnlyList<Vector2[]> Outline(int c)
    {
        List<Vector2[]> contours = [];
        Glyph(map.GetValueOrDefault(c), Matrix3x2.Identity, contours, 0);
        return contours;
    }

    private void Glyph(int glyph, Matrix3x2 transform, List<Vector2[]> into, int depth)
    {
        if (glyph <= 0 || glyph >= glyphCount || depth > 8)
        {
            return;
        }

        int loca = tables["loca"].Offset;
        int start = longLoca ? (int)U32(loca + (4 * glyph)) : U16(loca + (2 * glyph)) * 2;
        int end = longLoca ? (int)U32(loca + (4 * (glyph + 1))) : U16(loca + (2 * (glyph + 1))) * 2;
        if (end <= start)
        {
            return; // An empty glyph: a space.
        }

        int at = tables["glyf"].Offset + start;
        int contourCount = I16(at);
        if (contourCount >= 0)
        {
            Simple(at, contourCount, transform, into);
        }
        else
        {
            Composite(at + 10, transform, into, depth);
        }
    }

    private void Simple(int at, int contourCount, Matrix3x2 transform, List<Vector2[]> into)
    {
        int p = at + 10;
        int[] ends = new int[contourCount];
        for (int i = 0; i < contourCount; i++)
        {
            ends[i] = U16(p + (2 * i));
        }

        int points = contourCount == 0 ? 0 : ends[^1] + 1;
        p += (2 * contourCount) + 2 + U16(p + (2 * contourCount));
        byte[] flags = new byte[points];
        for (int i = 0; i < points;)
        {
            byte flag = data[p++];
            flags[i++] = flag;
            if ((flag & 8) != 0)
            {
                for (int repeat = data[p++]; repeat > 0 && i < points; repeat--)
                {
                    flags[i++] = flag;
                }
            }
        }

        int[] xs = Coordinates(flags, ref p, 2, 16);
        int[] ys = Coordinates(flags, ref p, 4, 32);
        int first = 0;
        foreach (int last in ends)
        {
            List<(Vector2 Point, bool On)> contour = [];
            for (int i = first; i <= last; i++)
            {
                contour.Add((new Vector2(xs[i], ys[i]), (flags[i] & 1) != 0));
            }

            if (contour.Count > 1)
            {
                into.Add([.. Flatten(contour).Select(v => Vector2.Transform(v, transform))]);
            }

            first = last + 1;
        }
    }

    private int[] Coordinates(byte[] flags, ref int p, byte isShort, byte sameOrPositive)
    {
        int[] values = new int[flags.Length];
        int value = 0;
        for (int i = 0; i < flags.Length; i++)
        {
            byte flag = flags[i];
            if ((flag & isShort) != 0)
            {
                int delta = data[p++];
                value += (flag & sameOrPositive) != 0 ? delta : -delta;
            }
            else if ((flag & sameOrPositive) == 0)
            {
                value += I16(p);
                p += 2;
            }

            values[i] = value;
        }

        return values;
    }

    private void Composite(int p, Matrix3x2 transform, List<Vector2[]> into, int depth)
    {
        const int ArgsAreWords = 1, ArgsAreXy = 2, HaveScale = 8, MoreComponents = 32, HaveXyScale = 64, HaveTwoByTwo = 128;
        int flags;
        do
        {
            flags = U16(p);
            int glyph = U16(p + 2);
            p += 4;
            float dx, dy;
            if ((flags & ArgsAreWords) != 0)
            {
                (dx, dy) = (I16(p), I16(p + 2));
                p += 4;
            }
            else
            {
                (dx, dy) = ((sbyte)data[p], (sbyte)data[p + 1]);
                p += 2;
            }

            if ((flags & ArgsAreXy) == 0)
            {
                (dx, dy) = (0, 0); // Point-matched placement is rare in Latin fonts; placed at the origin.
            }

            Matrix3x2 part = Matrix3x2.Identity;
            if ((flags & HaveScale) != 0)
            {
                float s = F2Dot14(p);
                part = Matrix3x2.CreateScale(s);
                p += 2;
            }
            else if ((flags & HaveXyScale) != 0)
            {
                part = Matrix3x2.CreateScale(F2Dot14(p), F2Dot14(p + 2));
                p += 4;
            }
            else if ((flags & HaveTwoByTwo) != 0)
            {
                part = new Matrix3x2(F2Dot14(p), F2Dot14(p + 2), F2Dot14(p + 4), F2Dot14(p + 6), 0, 0);
                p += 8;
            }

            part.Translation = new Vector2(dx, dy);
            Glyph(glyph, part * transform, into, depth + 1);
        }
        while ((flags & MoreComponents) != 0);
    }

    // A contour of on-curve and off-curve points as a polyline: two off-curve points in a row imply
    // an on-curve point between them, and each quadratic curve is cut into a few straight pieces.
    private static List<Vector2> Flatten(List<(Vector2 Point, bool On)> contour)
    {
        List<(Vector2 Point, bool On)> full = [];
        for (int i = 0; i < contour.Count; i++)
        {
            (Vector2 point, bool on) = contour[i];
            (Vector2 next, bool nextOn) = contour[(i + 1) % contour.Count];
            full.Add((point, on));
            if (!on && !nextOn)
            {
                full.Add(((point + next) / 2, true));
            }
        }

        int start = full.FindIndex(p => p.On);
        if (start < 0)
        {
            return [.. full.Select(p => p.Point)];
        }

        List<Vector2> line = [];
        int n = full.Count;
        for (int k = 0; k < n; k++)
        {
            (Vector2 a, _) = full[(start + k) % n];
            (Vector2 b, bool bOn) = full[(start + k + 1) % n];
            line.Add(a);
            if (!bOn)
            {
                Vector2 c = full[(start + k + 2) % n].Point;
                for (int s = 1; s < 6; s++)
                {
                    float t = s / 6f;
                    line.Add(((1 - t) * (1 - t) * a) + (2 * (1 - t) * t * b) + (t * t * c));
                }

                k++;
            }
        }

        return line;
    }

    private void ReadMap()
    {
        int cmap = tables["cmap"].Offset;
        int count = U16(cmap + 2);
        int best = -1, bestFormat = 0;
        for (int i = 0; i < count; i++)
        {
            int record = cmap + 4 + (8 * i);
            int platform = U16(record), encoding = U16(record + 2);
            int sub = cmap + (int)U32(record + 4);
            int format = U16(sub);
            bool unicode = platform == 0 || (platform == 3 && encoding is 1 or 10);
            if (unicode && (format == 12 || (format == 4 && bestFormat != 12)))
            {
                (best, bestFormat) = (sub, format);
            }
        }

        if (best < 0)
        {
            throw new InvalidDataException("The font has no Unicode character map.");
        }

        if (bestFormat == 12)
        {
            uint groups = U32(best + 12);
            for (uint g = 0; g < groups; g++)
            {
                int group = best + 16 + (int)(12 * g);
                uint from = U32(group), to = U32(group + 4), glyph = U32(group + 8);
                for (uint c = from; c <= to && c <= 0x2FFFF; c++)
                {
                    map[(int)c] = (int)(glyph + (c - from));
                }
            }

            return;
        }

        int segments = U16(best + 6) / 2;
        int ends = best + 14, starts = ends + (2 * segments) + 2, deltas = starts + (2 * segments), offsets = deltas + (2 * segments);
        for (int s = 0; s < segments; s++)
        {
            int end = U16(ends + (2 * s)), from = U16(starts + (2 * s));
            int delta = I16(deltas + (2 * s)), rangeOffset = U16(offsets + (2 * s));
            for (int c = from; c <= end && c != 0xFFFF; c++)
            {
                int glyph = rangeOffset == 0
                    ? (c + delta) & 0xFFFF
                    : U16(offsets + (2 * s) + rangeOffset + (2 * (c - from)));
                if (rangeOffset != 0 && glyph != 0)
                {
                    glyph = (glyph + delta) & 0xFFFF;
                }

                map[c] = glyph;
            }
        }
    }

    private int U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at, 2));

    private int I16(int at) => BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(at, 2));

    private uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at, 4));

    private float F2Dot14(int at) => I16(at) / 16384f;
}
