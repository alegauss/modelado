using System.Numerics;

namespace Modelado.Geometry;

/// <summary>
/// Lay flat (MOD112): the model turned so its largest flat face rests on the plate, which a slicer
/// prints without supports under it, instead of a face chosen by hand in every slicer. Triangles are
/// gathered by the plane they lie in (their normal to about a degree, their offset to a thousandth of
/// the model's size); a plane is a face the model can rest on only when no part of the model pokes
/// through it, so the largest such plane by area wins. The model is turned, never cut, so its face
/// points down the -Z axis, the plate of a slicer's Z-up frame, which STL and 3MF are written in
/// (<see cref="Mesh.OnPlate"/>, MOD109). A model with no flat face to rest on comes back unchanged.
/// </summary>
public static class LayFlat
{
    private const int Candidates = 64;

    /// <summary>The model resting on its largest flat face.</summary>
    /// <param name="mesh">The model.</param>
    /// <returns>The turned model and the face's area, or the model and 0 when it has no flat face.</returns>
    public static (Mesh Mesh, float Area) Apply(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.TriangleCount == 0)
        {
            return (mesh, 0);
        }

        (Vector3 min, Vector3 max) = mesh.Bounds();
        float tolerance = MathF.Max((max - min).Length() * 1e-3f, 1e-6f);
        Vector3[] p = mesh.Positions;
        int[] t = mesh.Triangles;

        // Area per plane: the normal rounded to about a degree, the offset to the tolerance.
        Dictionary<(int, int, int, long), (Vector3 Normal, float Offset, float Area)> planes = [];
        for (int k = 0; k < t.Length; k += 3)
        {
            Vector3 cross = Vector3.Cross(p[t[k + 1]] - p[t[k]], p[t[k + 2]] - p[t[k]]);
            float doubled = cross.Length();
            if (doubled < 1e-12f)
            {
                continue;
            }

            Vector3 n = cross / doubled;
            float offset = Vector3.Dot(p[t[k]], n);
            (int, int, int, long) key = ((int)MathF.Round(n.X * 60), (int)MathF.Round(n.Y * 60), (int)MathF.Round(n.Z * 60), (long)MathF.Round(offset / tolerance));
            (Vector3 Normal, float Offset, float Area) plane = planes.GetValueOrDefault(key, (n, offset, 0));
            planes[key] = (plane.Normal, plane.Offset, plane.Area + (doubled / 2));
        }

        // The largest plane nothing crosses: every vertex on its inner side.
        foreach ((Vector3 normal, float offset, float area) in planes.Values.OrderByDescending(plane => plane.Area).Take(Candidates))
        {
            if (p.All(v => Vector3.Dot(v, normal) <= offset + tolerance))
            {
                return (mesh.Transformed(Matrix4x4.CreateFromQuaternion(Turning(normal, -Vector3.UnitZ))), area);
            }
        }

        return (mesh, 0);
    }

    // The shortest turn taking one unit direction onto another.
    private static Quaternion Turning(Vector3 from, Vector3 to)
    {
        float cos = Vector3.Dot(from, to);
        if (cos > 1 - 1e-6f)
        {
            return Quaternion.Identity;
        }

        if (cos < -1 + 1e-6f)
        {
            // Opposite: half a turn about any axis square to both.
            Vector3 axis = Vector3.Cross(from, Vector3.UnitX);
            axis = axis.LengthSquared() < 1e-6f ? Vector3.Cross(from, Vector3.UnitY) : axis;
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }

        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(from, to)), MathF.Acos(cos));
    }
}
