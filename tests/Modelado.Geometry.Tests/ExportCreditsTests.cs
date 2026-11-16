using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using Modelado.Geometry;

namespace Modelado.Geometry.Tests;

/// <summary>MOD126: the notice a model's license requires, written into the exported file's metadata.</summary>
public sealed class ExportCreditsTests
{
    private static readonly ExportCredit[] Sf3d = [new("sf3d", "Powered by Stability AI")];

    private static Mesh Triangle() => new([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [0, 1, 2]);

    private static JsonObject GlbJson(byte[] glb)
    {
        int length = BitConverter.ToInt32(glb, 12);
        return JsonNode.Parse(Encoding.UTF8.GetString(glb, 20, length))!.AsObject();
    }

    private static string ThreeMfModel(byte[] package)
    {
        using ZipArchive zip = new(new MemoryStream(package));
        using StreamReader reader = new(zip.GetEntry(MeshExport.ThreeMfModelPart)!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void A_glb_carries_the_notice_as_its_copyright_and_names_the_model()
    {
        using MemoryStream glb = new();
        MeshExport.WriteGlb(Triangle(), glb, credits: Sf3d);

        JsonObject asset = GlbJson(glb.ToArray())["asset"]!.AsObject();
        Assert.Equal("Powered by Stability AI", (string?)asset["copyright"]);
        Assert.Equal("sf3d", (string?)asset["extras"]!["models"]![0]!["name"]);
    }

    [Fact]
    public void A_3mf_carries_the_notice_as_its_copyright_metadata()
    {
        using MemoryStream package = new();
        MeshExport.Write3mf(Triangle(), package, Sf3d);

        string model = ThreeMfModel(package.ToArray());
        Assert.Contains("<metadata name=\"Copyright\">Powered by Stability AI</metadata>", model, StringComparison.Ordinal);
        Assert.Contains("<metadata name=\"Description\">Made with sf3d</metadata>", model, StringComparison.Ordinal);
    }

    [Fact]
    public void A_result_of_permissive_models_carries_no_notice()
    {
        using MemoryStream glb = new(), package = new();
        MeshExport.WriteGlb(Triangle(), glb);
        MeshExport.Write3mf(Triangle(), package, []);

        Assert.Null(GlbJson(glb.ToArray())["asset"]!["copyright"]);
        Assert.DoesNotContain("Copyright", ThreeMfModel(package.ToArray()), StringComparison.Ordinal);
    }
}
