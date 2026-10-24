using System.Numerics;
using Modelado.Geometry;

namespace Modelado.Geometry.Tests;

public sealed class FieldSamplerTests
{
    private static float Field(Vector3 p) =>
        MathF.Max(0.45f - Vector3.Distance(p, new Vector3(-0.2f, 0, 0)), 0.3f - Vector3.Distance(p, new Vector3(0.35f, 0.2f, 0.1f)));

    private static void Evaluate(ReadOnlySpan<float> points, Span<float> values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = Field(new Vector3(points[i * 3], points[(i * 3) + 1], points[(i * 3) + 2]));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Coarse_to_fine_gives_the_surface_a_full_evaluation_gives(int step)
    {
        ScalarGrid full = ScalarGrid.Cube(65, 1f), sparse = ScalarGrid.Cube(65, 1f);
        long all = FieldSampler.Sample(full, Evaluate, 0, 1, 4096, cancellation: TestContext.Current.CancellationToken);
        long some = FieldSampler.Sample(sparse, Evaluate, 0, step, 4096, cancellation: TestContext.Current.CancellationToken);

        Mesh expected = MarchingCubes.Extract(full, 0), got = MarchingCubes.Extract(sparse, 0);

        Assert.Equal(65L * 65 * 65, all);
        Assert.True(some < all / 2, $"{some} of {all} evaluated");
        Assert.Equal(expected.Positions, got.Positions);
        Assert.Equal(expected.Triangles, got.Triangles);
    }

    [Fact]
    public void Every_point_is_filled_and_progress_reaches_the_end()
    {
        ScalarGrid grid = ScalarGrid.Cube(33, 1f);
        Array.Fill(grid.Values, float.NaN);
        List<double> reported = [];
        FieldSampler.Sample(grid, Evaluate, 0, 2, 100, new Collect(reported), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(grid.Values, float.IsNaN);
        Assert.Equal(1.0, reported[^1]);
        Assert.Equal(reported.Order(), reported);
    }

    [Fact]
    public void Batches_never_exceed_the_limit()
    {
        int largest = 0;
        FieldSampler.Sample(ScalarGrid.Cube(20, 1f), (points, values) =>
        {
            largest = Math.Max(largest, values.Length);
            Evaluate(points, values);
        }, 0, 2, 77, cancellation: TestContext.Current.CancellationToken);

        Assert.InRange(largest, 1, 77);
    }

    [Fact]
    public void Cancellation_stops_between_batches()
    {
        using CancellationTokenSource cancel = new();
        int calls = 0;
        Assert.Throws<OperationCanceledException>(() => FieldSampler.Sample(ScalarGrid.Cube(40, 1f), (points, values) =>
        {
            calls++;
            cancel.Cancel();
            Evaluate(points, values);
        }, 0, 1, 1000, cancellation: cancel.Token));
        Assert.Equal(1, calls);
    }

    private sealed class Collect(List<double> into) : IProgress<double>
    {
        public void Report(double value) => into.Add(value);
    }
}
