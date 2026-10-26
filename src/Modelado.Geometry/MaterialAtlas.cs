using System.Numerics;
using Modelado.Imaging;

namespace Modelado.Geometry;

/// <summary>One material of an imported model: its texture, or none, and the colour it is tinted by.</summary>
/// <param name="Texture">The base-colour texture, or null for a flat colour (already in the vertex colours).</param>
/// <param name="Tint">The sRGB colour the texture is multiplied by (glTF's baseColorFactor); white for none.</param>
public readonly record struct MaterialPart(RgbaImage? Texture, Vector3 Tint);

/// <summary>
/// What a single <see cref="Mesh"/>, which holds one texture, makes of a model with several materials
/// (MOD59). One texture is kept as it is, tinted where its material says. Two to
/// <see cref="MostTextures"/> are packed side by side into one atlas, each part's coordinates moved
/// into its tile, with a tile of flat cells for the parts that are a plain colour. More than that
/// are baked into the vertex colours, which a notice says, since an atlas of many tiles gives each too
/// few pixels to be worth keeping.
/// </summary>
public static class MaterialAtlas
{
    /// <summary>The most textures packed into one atlas.</summary>
    public const int MostTextures = 4;

    private const int LargestTile = 2048;

    /// <summary>Puts a model's parts into one mesh.</summary>
    /// <param name="positions">Every vertex.</param>
    /// <param name="triangles">Three indices per triangle.</param>
    /// <param name="colors">sRGB per vertex: flat parts' colours already baked in, white elsewhere.</param>
    /// <param name="anyColor">Whether any vertex carries a colour that is not white.</param>
    /// <param name="uvs">Per vertex, in its own part's texture; zero for a part with none.</param>
    /// <param name="partOf">The part each vertex belongs to.</param>
    /// <param name="parts">The parts.</param>
    /// <param name="doubleSided">Whether the surface is meant to be seen from both sides.</param>
    /// <param name="name">The file's name, for the notice.</param>
    /// <param name="notice">Why the textures were baked into colours, or null.</param>
    /// <returns>The mesh.</returns>
    public static Mesh Assemble(
        IReadOnlyList<Vector3> positions, int[] triangles, IReadOnlyList<Vector3> colors, bool anyColor, IReadOnlyList<Vector2> uvs,
        IReadOnlyList<int> partOf, IReadOnlyList<MaterialPart> parts, bool doubleSided, string name, out string? notice)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(colors);
        ArgumentNullException.ThrowIfNull(uvs);
        ArgumentNullException.ThrowIfNull(partOf);
        ArgumentNullException.ThrowIfNull(parts);
        notice = null;

        // A texture tinted differently by two materials is two tiles.
        List<MaterialPart> tiles = [.. parts.Where(p => p.Texture is not null).Distinct()];
        bool flatParts = parts.Any(p => p.Texture is null) && Enumerable.Range(0, partOf.Count).Any(v => parts[partOf[v]].Texture is null);
        Vector3[]? keptColors = anyColor ? [.. colors] : null;

        if (tiles.Count == 0)
        {
            return new Mesh([.. positions], triangles, keptColors, doubleSided: doubleSided);
        }

        if (tiles.Count == 1 && !flatParts)
        {
            return new Mesh([.. positions], triangles, keptColors, [.. uvs], Tinted(tiles[0].Texture!, tiles[0].Tint), doubleSided);
        }

        if (tiles.Count > MostTextures)
        {
            notice = $"{name} has {tiles.Count} textures, more than the {MostTextures} one model can keep, so its colours were baked into its vertices.";
            Vector3[] baked = new Vector3[positions.Count];
            for (int v = 0; v < baked.Length; v++)
            {
                MaterialPart part = parts[partOf[v]];
                if (part.Texture is null)
                {
                    baked[v] = colors[v];
                    continue;
                }

                (float r, float g, float b) = part.Texture.Sample(Wrap(uvs[v].X), Wrap(uvs[v].Y));
                baked[v] = new Vector3(r, g, b) * part.Tint * colors[v];
            }

            return new Mesh([.. positions], triangles, baked, doubleSided: doubleSided);
        }

        // Up to four tiles and a palette, two across: each as large as the largest texture, at most 2048 px.
        int count = tiles.Count + (flatParts ? 1 : 0);
        int side = Math.Min(LargestTile, tiles.Max(t => Math.Max(t.Texture!.Width, t.Texture.Height)));
        int across = count == 1 ? 1 : 2, down = (count + across - 1) / across;
        RgbaImage atlas = new(side * across, side * down);
        for (int k = 0; k < tiles.Count; k++)
        {
            Blit(ImageOps.Resize(Tinted(tiles[k].Texture!, tiles[k].Tint), side, side), atlas, k % across * side, k / across * side);
        }

        // The palette: a cell per flat part, its colour the part's first vertex's.
        int paletteTile = tiles.Count;
        List<int> flatIds = [.. Enumerable.Range(0, parts.Count).Where(p => parts[p].Texture is null)];
        int cells = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(flatIds.Count)));
        int cell = side / cells;
        Dictionary<int, Vector2> cellOf = [];
        for (int i = 0; i < flatIds.Count && flatParts; i++)
        {
            int first = Enumerable.Range(0, partOf.Count).FirstOrDefault(v => partOf[v] == flatIds[i], -1);
            Vector3 colour = first >= 0 ? colors[first] : Vector3.One;
            int cx = (paletteTile % across * side) + (i % cells * cell), cy = (paletteTile / across * side) + (i / cells * cell);
            Fill(atlas, cx, cy, cell, colour);
            cellOf[flatIds[i]] = new Vector2((cx + (cell / 2f)) / atlas.Width, (cy + (cell / 2f)) / atlas.Height);
        }

        // Each vertex's coordinates moved into its tile, half a texel in from its edges so no tile bleeds into the next.
        Vector2[] moved = new Vector2[positions.Count];
        Vector2 inset = new(0.5f / side, 0.5f / side);
        for (int v = 0; v < moved.Length; v++)
        {
            MaterialPart part = parts[partOf[v]];
            if (part.Texture is null)
            {
                moved[v] = cellOf.TryGetValue(partOf[v], out Vector2 at) ? at : Vector2.Zero;
                continue;
            }

            int k = tiles.IndexOf(part);
            Vector2 inTile = inset + (new Vector2(Wrap(uvs[v].X), Wrap(uvs[v].Y)) * (Vector2.One - (2 * inset)));
            moved[v] = new Vector2(((k % across) + inTile.X) / across, ((k / across) + inTile.Y) / down);
        }

        return new Mesh([.. positions], triangles, keptColors, moved, atlas, doubleSided);
    }

    private static RgbaImage Tinted(RgbaImage texture, Vector3 tint)
    {
        if (tint == Vector3.One)
        {
            return texture;
        }

        RgbaImage tinted = texture.Clone();
        for (int i = 0; i < tinted.Pixels.Length; i += 4)
        {
            tinted.Pixels[i] = (byte)MathF.Round(tinted.Pixels[i] * tint.X);
            tinted.Pixels[i + 1] = (byte)MathF.Round(tinted.Pixels[i + 1] * tint.Y);
            tinted.Pixels[i + 2] = (byte)MathF.Round(tinted.Pixels[i + 2] * tint.Z);
        }

        return tinted;
    }

    // A coordinate a repeating texture reads the same, brought into [0, 1].
    private static float Wrap(float t) => t is >= 0 and <= 1 ? t : t - MathF.Floor(t);

    private static void Blit(RgbaImage from, RgbaImage to, int x0, int y0)
    {
        for (int y = 0; y < from.Height; y++)
        {
            Array.Copy(from.Pixels, from.OffsetOf(0, y), to.Pixels, to.OffsetOf(x0, y0 + y), from.Width * 4);
        }
    }

    private static void Fill(RgbaImage image, int x0, int y0, int size, Vector3 colour)
    {
        for (int y = y0; y < y0 + size; y++)
        {
            for (int x = x0; x < x0 + size; x++)
            {
                int i = image.OffsetOf(x, y);
                image.Pixels[i] = (byte)MathF.Round(Math.Clamp(colour.X, 0, 1) * 255);
                image.Pixels[i + 1] = (byte)MathF.Round(Math.Clamp(colour.Y, 0, 1) * 255);
                image.Pixels[i + 2] = (byte)MathF.Round(Math.Clamp(colour.Z, 0, 1) * 255);
                image.Pixels[i + 3] = 255;
            }
        }
    }
}
