using System.Numerics;
using Modelado.Geometry;
using Modelado.Imaging;
using Modelado.Inference;
using Modelado.Inference.Pipelines;

namespace Modelado.Inference.Tests;

/// <summary>
/// The pipelines on the real models scripts/export_models.py writes to .cache/models, on the CPU.
/// Without them each test says so and passes: run download_models.py and export_models.py first.
/// </summary>
public sealed class PipelineTests : IDisposable
{
    private readonly ModelRunner? runner = Repository.HasModels
        ? new ModelRunner(new ModelLibrary([Repository.Models]), ExecutionProvider.Cpu)
        : null;

    public void Dispose() => runner?.Dispose();

    private bool Skip()
    {
        if (runner is null)
        {
            TestContext.Current.SendDiagnosticMessage($"skipped: no models in {Repository.Models}");
            return true;
        }

        return false;
    }

    [Fact]
    public void Every_meta_json_reads_and_names_its_license()
    {
        if (Skip())
        {
            return;
        }

        IReadOnlyList<InstalledModel> installed = runner!.Library.Installed();
        Assert.Equal(["depth-anything-v2-small", "triposr-decoder", "triposr-encoder", "u2net"], installed.Select(m => m.Name));
        Assert.All(installed, m => Assert.Contains("fp32", m.Variants));
        Assert.All(installed, m => Assert.False(string.IsNullOrEmpty(m.Meta.Upstream.License)));
        Assert.Equal(0.87, runner.Library.Meta(ObjectReconstruction.Decoder).Param("radius"), 6);
    }

    [Fact]
    public void Background_removal_finds_the_chair_its_own_alpha_shows()
    {
        if (Skip())
        {
            return;
        }

        RgbaImage chair = ImageCodec.Load(Path.Combine(Repository.TestImages, "chair.png"));
        RgbaImage onWhite = ImageOps.Composite(chair, 1f);
        float[] mask = BackgroundRemoval.Mask(runner!, onWhite);

        int both = 0, either = 0;
        for (int i = 0; i < mask.Length; i++)
        {
            bool found = mask[i] > 0.5f, truth = chair.Pixels[(i * 4) + 3] > 127;
            both += found && truth ? 1 : 0;
            either += found || truth ? 1 : 0;
        }

        Assert.True(both / (double)either > 0.9, $"IoU {both / (double)either:0.000}");
    }

    [Fact]
    public void The_chair_becomes_a_closed_coloured_mesh_facing_the_front()
    {
        if (Skip())
        {
            return;
        }

        RgbaImage chair = ImageCodec.Load(Path.Combine(Repository.TestImages, "chair.png"));
        ReconstructionResult result = ObjectReconstruction.Run(runner!, chair, new ObjectOptions(Resolution: 128), cancellation: TestContext.Current.CancellationToken);
        Mesh mesh = result.Mesh;

        Assert.InRange(mesh.TriangleCount, 5000, 200000);
        Assert.True(mesh.SignedVolume() > 0, "triangles face outward");
        Dictionary<(int, int), int> uses = [];
        for (int t = 0; t < mesh.Triangles.Length; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int a = mesh.Triangles[t + k], b = mesh.Triangles[t + ((k + 1) % 3)];
                uses[a < b ? (a, b) : (b, a)] = uses.GetValueOrDefault(a < b ? (a, b) : (b, a)) + 1;
            }
        }

        Assert.All(uses.Values, n => Assert.Equal(2, n));

        // Upright: a chair is taller than deep, and its seat is not the top.
        (Vector3 min, Vector3 max) = mesh.Bounds();
        Assert.True(max.Y - min.Y > max.Z - min.Z);
        Assert.NotNull(mesh.Colors);
        Assert.All(mesh.Colors!, c => Assert.InRange(c.X, 0f, 1f));
        Assert.Equal(512, result.Input.Width);
    }

    [Fact]
    public void A_photo_becomes_a_textured_relief()
    {
        if (Skip())
        {
            return;
        }

        RgbaImage photo = ImageOps.Fit(ImageCodec.Load(Path.Combine(Repository.TestImages, "captured.jpeg")), 1024);
        ReconstructionResult result = ReliefReconstruction.Run(runner!, photo, new ReliefRunOptions(new ReliefOptions(GridLongSide: 128, BaseThickness: 0.05f), RemoveBackground: true), cancellation: TestContext.Current.CancellationToken);

        Assert.NotNull(result.Mesh.Texture);
        Assert.True(result.Mesh.SignedVolume() > 0);
        // The plush toy, nearer than the chair it sits on, stands out in the middle.
        (Vector3 min, Vector3 max) = result.Mesh.Bounds();
        Assert.True(max.Z > 0.2f);
        Assert.True(max.X - min.X < 1f, "only the object is kept");
    }
}
