namespace Modelado.Geometry;

/// <summary>
/// Fills a <see cref="ScalarGrid"/> from an expensive field (TripoSR's decoder) without
/// evaluating every point: first every <c>step</c>-th point, then the full resolution only in
/// the coarse cells the surface crosses and their neighbours. Elsewhere the coarse values are
/// interpolated, which keeps every point on the side of the level the coarse grid saw, so the
/// surface marching cubes extracts is the one a full evaluation gives wherever the coarse grid
/// saw it. A part thinner than a coarse cell can be missed; <c>step</c> 1 evaluates everything.
/// </summary>
public static class FieldSampler
{
    /// <summary>Evaluates the field at a batch of points.</summary>
    /// <param name="points">x, y, z per point.</param>
    /// <param name="values">One value per point, to fill.</param>
    public delegate void Evaluate(ReadOnlySpan<float> points, Span<float> values);

    /// <summary>Samples the field into the grid.</summary>
    /// <param name="grid">The grid to fill.</param>
    /// <param name="field">The field, called with batches of at most <paramref name="batch"/> points.</param>
    /// <param name="level">The level whose crossing is refined.</param>
    /// <param name="step">The coarse spacing in grid points; 1 evaluates every point.</param>
    /// <param name="batch">The most points per call.</param>
    /// <param name="progress">Told the share of the evaluations done, 0 to 1.</param>
    /// <param name="cancellation">Checked between batches.</param>
    /// <returns>How many points were evaluated.</returns>
    public static long Sample(ScalarGrid grid, Evaluate field, float level, int step, int batch, IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentOutOfRangeException.ThrowIfLessThan(step, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(batch, 1);

        int[] xs = CoarseAxis(grid.Nx, step), ys = CoarseAxis(grid.Ny, step), zs = CoarseAxis(grid.Nz, step);
        bool[] known = new bool[grid.Values.Length];
        List<int> coarse = new(xs.Length * ys.Length * zs.Length);
        foreach (int x in xs)
        {
            foreach (int y in ys)
            {
                foreach (int z in zs)
                {
                    coarse.Add(grid.Index(x, y, z));
                }
            }
        }

        if (step == 1)
        {
            Run(grid, field, coarse, known, batch, progress, 0, 1, cancellation);
            return coarse.Count;
        }

        // The coarse pass is about a step³-th of the work, the refinement the rest; the bar is
        // split by an estimate, so it moves smoothly rather than exactly.
        Run(grid, field, coarse, known, batch, progress, 0, 0.3, cancellation);

        // Coarse cells the level crosses, grown by one cell on every side.
        int cx = xs.Length - 1, cy = ys.Length - 1, cz = zs.Length - 1;
        bool[] crossed = new bool[cx * cy * cz];
        for (int i = 0; i < cx; i++)
        {
            for (int j = 0; j < cy; j++)
            {
                for (int k = 0; k < cz; k++)
                {
                    float min = float.MaxValue, max = float.MinValue;
                    for (int c = 0; c < 8; c++)
                    {
                        float v = grid[xs[i + (c & 1)], ys[j + ((c >> 1) & 1)], zs[k + (c >> 2)]];
                        min = MathF.Min(min, v);
                        max = MathF.Max(max, v);
                    }

                    crossed[(((i * cy) + j) * cz) + k] = min <= level && max > level;
                }
            }
        }

        bool[] refine = new bool[crossed.Length];
        for (int i = 0; i < cx; i++)
        {
            for (int j = 0; j < cy; j++)
            {
                for (int k = 0; k < cz; k++)
                {
                    if (!crossed[(((i * cy) + j) * cz) + k])
                    {
                        continue;
                    }

                    for (int di = Math.Max(0, i - 1); di <= Math.Min(cx - 1, i + 1); di++)
                    {
                        for (int dj = Math.Max(0, j - 1); dj <= Math.Min(cy - 1, j + 1); dj++)
                        {
                            for (int dk = Math.Max(0, k - 1); dk <= Math.Min(cz - 1, k + 1); dk++)
                            {
                                refine[(((di * cy) + dj) * cz) + dk] = true;
                            }
                        }
                    }
                }
            }
        }

        // Every fine point of a cell to refine, each once.
        List<int> fine = [];
        bool[] queued = known.ToArray();
        for (int i = 0; i < cx; i++)
        {
            for (int j = 0; j < cy; j++)
            {
                for (int k = 0; k < cz; k++)
                {
                    if (!refine[(((i * cy) + j) * cz) + k])
                    {
                        continue;
                    }

                    for (int x = xs[i]; x <= xs[i + 1]; x++)
                    {
                        for (int y = ys[j]; y <= ys[j + 1]; y++)
                        {
                            for (int z = zs[k]; z <= zs[k + 1]; z++)
                            {
                                int index = grid.Index(x, y, z);
                                if (!queued[index])
                                {
                                    queued[index] = true;
                                    fine.Add(index);
                                }
                            }
                        }
                    }
                }
            }
        }

        Run(grid, field, fine, known, batch, progress, 0.3, 0.7, cancellation);
        FillFromCoarse(grid, xs, ys, zs, known);
        progress?.Report(1);
        return coarse.Count + fine.Count;
    }

    // 0, step, 2·step, ..., and always the last point, so the coarse grid spans the fine one.
    private static int[] CoarseAxis(int count, int step)
    {
        List<int> axis = [];
        for (int i = 0; i < count - 1; i += step)
        {
            axis.Add(i);
        }

        axis.Add(count - 1);
        return [.. axis];
    }

    private static void Run(ScalarGrid grid, Evaluate field, List<int> indices, bool[] known, int batch, IProgress<double>? progress, double from, double span, CancellationToken cancellation)
    {
        float[] points = new float[Math.Min(batch, Math.Max(1, indices.Count)) * 3];
        float[] values = new float[points.Length / 3];
        int nyz = grid.Ny * grid.Nz;
        for (int start = 0; start < indices.Count; start += batch)
        {
            cancellation.ThrowIfCancellationRequested();
            int count = Math.Min(batch, indices.Count - start);
            for (int i = 0; i < count; i++)
            {
                int index = indices[start + i];
                int x = index / nyz, y = index / grid.Nz % grid.Ny, z = index % grid.Nz;
                System.Numerics.Vector3 p = grid.PointAt(x, y, z);
                points[i * 3] = p.X;
                points[(i * 3) + 1] = p.Y;
                points[(i * 3) + 2] = p.Z;
            }

            field(points.AsSpan(0, count * 3), values.AsSpan(0, count));
            for (int i = 0; i < count; i++)
            {
                grid.Values[indices[start + i]] = values[i];
                known[indices[start + i]] = true;
            }

            progress?.Report(from + (span * (start + count) / indices.Count));
        }
    }

    // Trilinear interpolation of the coarse samples into every point not evaluated.
    private static void FillFromCoarse(ScalarGrid grid, int[] xs, int[] ys, int[] zs, bool[] known)
    {
        int[] cellX = CellOf(xs, grid.Nx), cellY = CellOf(ys, grid.Ny), cellZ = CellOf(zs, grid.Nz);
        for (int x = 0; x < grid.Nx; x++)
        {
            int i = cellX[x];
            float tx = (float)(x - xs[i]) / (xs[i + 1] - xs[i]);
            for (int y = 0; y < grid.Ny; y++)
            {
                int j = cellY[y];
                float ty = (float)(y - ys[j]) / (ys[j + 1] - ys[j]);
                for (int z = 0; z < grid.Nz; z++)
                {
                    int index = grid.Index(x, y, z);
                    if (known[index])
                    {
                        continue;
                    }

                    int k = cellZ[z];
                    float tz = (float)(z - zs[k]) / (zs[k + 1] - zs[k]);
                    float c00 = Lerp(grid[xs[i], ys[j], zs[k]], grid[xs[i + 1], ys[j], zs[k]], tx);
                    float c10 = Lerp(grid[xs[i], ys[j + 1], zs[k]], grid[xs[i + 1], ys[j + 1], zs[k]], tx);
                    float c01 = Lerp(grid[xs[i], ys[j], zs[k + 1]], grid[xs[i + 1], ys[j], zs[k + 1]], tx);
                    float c11 = Lerp(grid[xs[i], ys[j + 1], zs[k + 1]], grid[xs[i + 1], ys[j + 1], zs[k + 1]], tx);
                    grid.Values[index] = Lerp(Lerp(c00, c10, ty), Lerp(c01, c11, ty), tz);
                }
            }
        }
    }

    // For each fine index, the coarse cell (left end) holding it.
    private static int[] CellOf(int[] axis, int count)
    {
        int[] cell = new int[count];
        int c = 0;
        for (int i = 0; i < count; i++)
        {
            while (c < axis.Length - 2 && i >= axis[c + 1])
            {
                c++;
            }

            cell[i] = c;
        }

        return cell;
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
