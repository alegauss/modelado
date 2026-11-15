using System.Numerics;

namespace Modelado.Geometry;

/// <summary>A stand a printed relief slots into, to stand on a shelf (MOD107).</summary>
/// <param name="Tilt">How far the relief leans back from upright, in degrees.</param>
/// <param name="Clearance">Play between the relief and the slot's walls, in millimetres, so a print slides in.</param>
public sealed record StandOptions(float Tilt = Stand.TiltDefault, float Clearance = 0.4f);

/// <summary>
/// A stand for a relief sized in millimetres (MOD107): a block with a slot cut across it, leaning
/// back, that the plaque or medallion is pushed into. It prints on its own, flat side down, without
/// supports, and is sized from the relief it holds: the slot as wide as the relief is thick plus a
/// little play, the block as long as most of the relief's width, and deep enough behind the slot that
/// the relief's weight, leaning back, falls inside the foot and it does not tip. The block's side view
/// is one polygon extruded along the slot, so the stand is a single closed solid.
/// </summary>
public static class Stand
{
    /// <summary>How far a relief leans back unless told otherwise, in degrees.</summary>
    public const float TiltDefault = 15f;

    private const float SlotDepth = 8, Floor = 4, Front = 6, Behind = 6, MinimumLength = 30;

    /// <summary>The stand for a relief, its coordinates in millimetres with z up and the slot along x.</summary>
    /// <param name="relief">The relief, in millimetres: x across, y up, z its thickness.</param>
    /// <param name="options">The tilt and the slot's play, or the defaults.</param>
    /// <returns>A closed mesh resting on z = 0.</returns>
    /// <exception cref="ArgumentException">A tilt outside 0 to 30 degrees, or a relief with no thickness.</exception>
    public static Mesh For(Mesh relief, StandOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(relief);
        options ??= new StandOptions();
        if (options.Tilt is < 0 or > 30)
        {
            throw new ArgumentException("A stand leans a relief back between 0 and 30 degrees.", nameof(options));
        }

        if (options.Clearance < 0)
        {
            throw new ArgumentException("The slot's play is 0 mm or more.", nameof(options));
        }

        (Vector3 min, Vector3 max) = relief.Bounds();
        Vector3 extent = max - min;
        if (extent.Z <= 0 || extent.X <= 0 || extent.Y <= 0)
        {
            throw new ArgumentException("A stand needs a relief with a width, a height and a thickness.", nameof(relief));
        }

        float tilt = options.Tilt * MathF.PI / 180;
        float across = (extent.Z + options.Clearance) / MathF.Cos(tilt); // the slot's width along the top
        float lean = SlotDepth * MathF.Tan(tilt);                          // how far its bottom sits forward
        float height = SlotDepth + Floor;

        // Leaning back, the relief's centre of mass sits half its height times sin(tilt) behind the slot;
        // the foot reaches past it, so the stand does not tip backwards.
        float front = Front + lean;
        float behind = MathF.Max(Behind, (extent.Y / 2 * MathF.Sin(tilt)) + Behind);
        float depth = front + across + behind;
        float length = Math.Clamp(extent.X * 0.6f, MinimumLength, MathF.Max(MinimumLength, extent.X));

        // The side view, counter-clockwise in (y, z): y runs front to back, the slot leaning to the back.
        Vector2[] profile =
        [
            new(0, 0), new(depth, 0), new(depth, height),
            new(front + across, height), new(front + across - lean, Floor),
            new(front - lean, Floor), new(front, height), new(0, height),
        ];

        return Extrude(profile, length);
    }

    /// <summary>Where a relief's stand is written beside it: photo.stl gives photo-stand.stl.</summary>
    /// <param name="output">The relief's file.</param>
    /// <returns>The stand's file.</returns>
    public static string PathBeside(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        string folder = Path.GetDirectoryName(output) ?? "";
        return Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(output)}-stand{Path.GetExtension(output)}");
    }

    // A counter-clockwise polygon in (y, z) extruded from x = -length/2 to +length/2: two capped ends
    // and a quad per edge, every edge shared by exactly two triangles.
    private static Mesh Extrude(Vector2[] profile, float length)
    {
        int n = profile.Length;
        Vector3[] positions = new Vector3[2 * n];
        for (int i = 0; i < n; i++)
        {
            positions[i] = new Vector3(-length / 2, profile[i].X, profile[i].Y);
            positions[n + i] = new Vector3(length / 2, profile[i].X, profile[i].Y);
        }

        List<int> triangles = [];
        foreach ((int a, int b, int c) in EarClip(profile))
        {
            // Seen from +x the profile is counter-clockwise, so the +x end keeps its order and the -x end flips.
            triangles.AddRange([n + a, n + b, n + c, a, c, b]);
        }

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            triangles.AddRange([i, j, n + j, i, n + j, n + i]);
        }

        return new Mesh(positions, [.. triangles]);
    }

    // Triangles covering a simple counter-clockwise polygon, by clipping one convex ear at a time.
    private static List<(int A, int B, int C)> EarClip(Vector2[] polygon)
    {
        List<int> left = [.. Enumerable.Range(0, polygon.Length)];
        List<(int, int, int)> triangles = [];
        while (left.Count > 3)
        {
            bool clipped = false;
            for (int k = 0; k < left.Count && !clipped; k++)
            {
                int a = left[(k + left.Count - 1) % left.Count], b = left[k], c = left[(k + 1) % left.Count];
                if (Cross(polygon[a], polygon[b], polygon[c]) <= 0)
                {
                    continue;
                }

                bool empty = left.Where(i => i != a && i != b && i != c).All(i => !Inside(polygon[i], polygon[a], polygon[b], polygon[c]));
                if (empty)
                {
                    triangles.Add((a, b, c));
                    left.RemoveAt(k);
                    clipped = true;
                }
            }

            if (!clipped)
            {
                throw new InvalidOperationException("The stand's outline is not a simple polygon.");
            }
        }

        triangles.Add((left[0], left[1], left[2]));
        return triangles;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c) => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
        Cross(a, b, p) >= 0 && Cross(b, c, p) >= 0 && Cross(c, a, p) >= 0;
}
