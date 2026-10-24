using System.Numerics;

namespace Modelado.Geometry;

/// <summary>
/// Values sampled on a regular lattice of points, x slowest and z fastest (numpy's
/// meshgrid(..., indexing="ij").ravel(), which export_models.py validates with).
/// </summary>
public sealed class ScalarGrid
{
    /// <summary>Creates a grid of zeros.</summary>
    /// <param name="nx">Points along X.</param>
    /// <param name="ny">Points along Y.</param>
    /// <param name="nz">Points along Z.</param>
    /// <param name="origin">The position of point (0, 0, 0).</param>
    /// <param name="spacing">The distance between neighbouring points along each axis.</param>
    public ScalarGrid(int nx, int ny, int nz, Vector3 origin, Vector3 spacing)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nx, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(ny, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(nz, 2);
        Nx = nx;
        Ny = ny;
        Nz = nz;
        Origin = origin;
        Spacing = spacing;
        Values = new float[checked(nx * ny * nz)];
    }

    /// <summary>A cube of <paramref name="resolution"/>³ points spanning [-radius, radius] on each axis.</summary>
    /// <param name="resolution">Points per side.</param>
    /// <param name="radius">Half the cube's side.</param>
    /// <returns>The grid.</returns>
    public static ScalarGrid Cube(int resolution, float radius) =>
        new(resolution, resolution, resolution, new Vector3(-radius), new Vector3(2 * radius / (resolution - 1)));

    /// <summary>Points along X.</summary>
    public int Nx { get; }

    /// <summary>Points along Y.</summary>
    public int Ny { get; }

    /// <summary>Points along Z.</summary>
    public int Nz { get; }

    /// <summary>The position of point (0, 0, 0).</summary>
    public Vector3 Origin { get; }

    /// <summary>The distance between neighbouring points.</summary>
    public Vector3 Spacing { get; }

    /// <summary>The samples, index (x · Ny + y) · Nz + z.</summary>
    public float[] Values { get; }

    /// <summary>A point's value.</summary>
    /// <param name="x">Index along X.</param>
    /// <param name="y">Index along Y.</param>
    /// <param name="z">Index along Z.</param>
    public float this[int x, int y, int z]
    {
        get => Values[Index(x, y, z)];
        set => Values[Index(x, y, z)] = value;
    }

    /// <summary>A point's index into <see cref="Values"/>.</summary>
    /// <param name="x">Index along X.</param>
    /// <param name="y">Index along Y.</param>
    /// <param name="z">Index along Z.</param>
    /// <returns>The index.</returns>
    public int Index(int x, int y, int z) => (((x * Ny) + y) * Nz) + z;

    /// <summary>A point's position.</summary>
    /// <param name="x">Index along X.</param>
    /// <param name="y">Index along Y.</param>
    /// <param name="z">Index along Z.</param>
    /// <returns>Where it is.</returns>
    public Vector3 PointAt(int x, int y, int z) => Origin + (new Vector3(x, y, z) * Spacing);

    /// <summary>Every point's position, in <see cref="Values"/> order, as x, y, z floats.</summary>
    /// <returns>3 · Nx · Ny · Nz floats.</returns>
    public float[] Points()
    {
        float[] points = new float[Values.Length * 3];
        int i = 0;
        for (int x = 0; x < Nx; x++)
        {
            for (int y = 0; y < Ny; y++)
            {
                for (int z = 0; z < Nz; z++)
                {
                    Vector3 p = PointAt(x, y, z);
                    points[i++] = p.X;
                    points[i++] = p.Y;
                    points[i++] = p.Z;
                }
            }
        }

        return points;
    }

    /// <summary>The value at a position, trilinearly interpolated, with edges clamped.</summary>
    /// <param name="position">Where.</param>
    /// <returns>The value.</returns>
    public float Sample(Vector3 position)
    {
        Vector3 f = (position - Origin) / Spacing;
        float fx = Math.Clamp(f.X, 0, Nx - 1), fy = Math.Clamp(f.Y, 0, Ny - 1), fz = Math.Clamp(f.Z, 0, Nz - 1);
        int x0 = Math.Min((int)fx, Nx - 2), y0 = Math.Min((int)fy, Ny - 2), z0 = Math.Min((int)fz, Nz - 2);
        float tx = fx - x0, ty = fy - y0, tz = fz - z0;
        float c00 = Lerp(this[x0, y0, z0], this[x0 + 1, y0, z0], tx);
        float c01 = Lerp(this[x0, y0, z0 + 1], this[x0 + 1, y0, z0 + 1], tx);
        float c10 = Lerp(this[x0, y0 + 1, z0], this[x0 + 1, y0 + 1, z0], tx);
        float c11 = Lerp(this[x0, y0 + 1, z0 + 1], this[x0 + 1, y0 + 1, z0 + 1], tx);
        return Lerp(Lerp(c00, c10, ty), Lerp(c01, c11, ty), tz);
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
