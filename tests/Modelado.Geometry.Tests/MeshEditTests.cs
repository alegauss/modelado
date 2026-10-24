using System.Numerics;
using Modelado.Geometry;

namespace Modelado.Geometry.Tests;

public sealed class MeshEditTests
{
    private static Mesh Sphere(int resolution = 48)
    {
        Mesh sphere = MarchingCubes.Extract(MarchingCubesTests.Sphere(resolution, 0.6f), 0);
        return new Mesh(sphere.Positions, sphere.Triangles, [.. sphere.Positions.Select(p => new Vector3((p.X + 1) / 2, 0.5f, 0.2f))]);
    }

    private static void AssertClosed(Mesh mesh) => Assert.All(MarchingCubesTests.EdgeUses(mesh).Values, uses => Assert.Equal(2, uses));

    [Fact]
    public void Simplifying_keeps_the_shape_closed_and_reaches_the_target()
    {
        Mesh sphere = Sphere();
        Mesh simple = MeshEdit.Simplify(sphere, sphere.TriangleCount / 10, TestContext.Current.CancellationToken);

        Assert.InRange(simple.TriangleCount, (sphere.TriangleCount / 10) - 2, sphere.TriangleCount / 10);
        AssertClosed(simple);
        Assert.Equal(sphere.SignedVolume(), simple.SignedVolume(), sphere.SignedVolume() * 0.03);
        Assert.All(simple.Positions, p => Assert.Equal(0.6f, p.Length(), 0.03f));
        Assert.NotNull(simple.Colors);
        Assert.True(simple.ComputeNormals().Select((n, i) => Vector3.Dot(n, simple.Positions[i])).All(d => d > 0), "no triangle folded over");
    }

    [Fact]
    public void Simplifying_an_open_surface_keeps_its_border()
    {
        float[] flat = new float[40 * 40];
        Mesh relief = Relief.Build(flat, 40, 40, new Imaging.RgbaImage(40, 40), default, new ReliefOptions(GridLongSide: 40, SmoothPasses: 0));
        Mesh simple = MeshEdit.Simplify(relief, 200, TestContext.Current.CancellationToken);

        (Vector3 min, Vector3 max) = simple.Bounds();
        Assert.Equal(1f, max.X - min.X, 1e-3f);
        Assert.Equal(1f, max.Y - min.Y, 1e-3f);
        Assert.Equal(relief.Area(), simple.Area(), 0.01);
        Assert.NotNull(simple.TexCoords);
    }

    [Fact]
    public void A_target_above_the_count_changes_nothing()
    {
        Mesh sphere = Sphere(16);
        Assert.Same(sphere, MeshEdit.Simplify(sphere, sphere.TriangleCount + 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Subdividing_quadruples_the_triangles_and_stays_closed()
    {
        Mesh sphere = Sphere(16);
        Mesh fine = MeshEdit.Subdivide(sphere, smoothing: 0);

        Assert.Equal(sphere.TriangleCount * 4, fine.TriangleCount);
        AssertClosed(fine);
        Assert.Equal(sphere.SignedVolume(), fine.SignedVolume(), 1e-4);
        Assert.Equal(fine.VertexCount, fine.Colors!.Length);
    }

    [Fact]
    public void Holes_are_filled_so_the_mesh_is_closed_again()
    {
        Mesh sphere = Sphere(32);
        // Two holes: the triangles near the top and near the bottom go.
        List<int> kept = [];
        for (int t = 0; t < sphere.Triangles.Length; t += 3)
        {
            // A triangle goes only when all its corners are past the cut, so each hole is one clean loop.
            bool top = true, bottom = true;
            for (int k = 0; k < 3; k++)
            {
                float y = sphere.Positions[sphere.Triangles[t + k]].Y;
                top &= y > 0.45f;
                bottom &= y < -0.45f;
            }

            if (!top && !bottom)
            {
                kept.AddRange(sphere.Triangles.AsSpan(t, 3));
            }
        }

        Mesh holed = MeshCleanup.Compact(sphere, [.. kept]);
        (Mesh closed, int filled) = MeshEdit.FillHoles(holed);

        AssertClosed(closed);
        Assert.InRange(filled, 2, 6);
        Assert.False(closed.DoubleSided);
        Assert.True(closed.SignedVolume() > holed.SignedVolume());
    }

    [Fact]
    public void An_open_surface_keeps_its_outer_border()
    {
        float[] flat = new float[20 * 20];
        Mesh relief = Relief.Build(flat, 20, 20, new Imaging.RgbaImage(20, 20), default, new ReliefOptions(GridLongSide: 20));

        (Mesh same, int filled) = MeshEdit.FillHoles(relief);

        Assert.Equal(0, filled);
        Assert.Equal(relief.TriangleCount, same.TriangleCount);
    }

    [Fact]
    public void Flattening_puts_the_bottom_on_one_plane()
    {
        Mesh flat = MeshEdit.FlattenBase(Sphere(32), 0.1f);

        (Vector3 min, _) = flat.Bounds();
        Assert.Equal(-0.6f + 0.12f, min.Y, 0.01f);
        Assert.True(flat.Positions.Count(p => MathF.Abs(p.Y - min.Y) < 1e-6f) > 20);
    }

    [Fact]
    public void Rotating_and_mirroring_keep_the_triangles_facing_out()
    {
        Mesh sphere = Sphere(16);
        Mesh offset = sphere.Transformed(Matrix4x4.CreateTranslation(3, 0, 0));

        Mesh turned = MeshEdit.Rotate(offset, Axis.Z, 90);
        Mesh mirrored = MeshEdit.Mirror(offset);

        Assert.Equal(offset.Bounds().Min.X, turned.Bounds().Min.X, 0.05f);
        Assert.True(turned.SignedVolume() > 0);
        Assert.True(mirrored.SignedVolume() > 0);
        Assert.Equal(offset.Bounds().Max.X, mirrored.Bounds().Max.X, 1e-4f);
    }

    [Fact]
    public void History_undoes_and_redoes_and_drops_what_an_edit_replaces()
    {
        Mesh a = Sphere(8), b = Sphere(10), c = Sphere(12);
        MeshHistory history = new();
        history.Reset(a, "Objeto 3D");
        history.Push(b, "Suavizar");

        Assert.Same(a, history.Undo());
        Assert.True(history.CanRedo);
        history.Push(c, "Simplificar");
        Assert.False(history.CanRedo);
        Assert.Same(a, history.Undo());
        Assert.False(history.CanUndo);
        Assert.Same(c, history.Redo());
        Assert.Equal("Simplificar", history.CurrentLabel);
    }
}
