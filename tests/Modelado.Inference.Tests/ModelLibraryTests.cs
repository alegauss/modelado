using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>MOD23: removing one downloaded model, and never anything beside the app.</summary>
public sealed class ModelLibraryTests : IDisposable
{
    private readonly string beside = Directory.CreateTempSubdirectory("modelado-beside-").FullName;
    private readonly string downloads = Directory.CreateTempSubdirectory("modelado-downloads-").FullName;

    public void Dispose()
    {
        Directory.Delete(beside, recursive: true);
        Directory.Delete(downloads, recursive: true);
    }

    private static void Model(string root, string name)
    {
        Directory.CreateDirectory(Path.Combine(root, name));
        File.WriteAllText(Path.Combine(root, name, "meta.json"), "{}");
        File.WriteAllBytes(Path.Combine(root, name, "fp32.onnx"), [1, 2, 3]);
    }

    [Fact]
    public void Removing_one_download_keeps_the_others_and_the_files_beside_the_app()
    {
        Model(downloads, "depth-anything-v2-small");
        Model(downloads, "triposr-encoder");
        Model(beside, "depth-anything-v2-small");
        ModelLibrary library = new([beside, downloads]);

        Assert.True(library.RemoveDownload("depth-anything-v2-small"));

        Assert.False(Directory.Exists(Path.Combine(downloads, "depth-anything-v2-small")));
        Assert.True(Directory.Exists(Path.Combine(downloads, "triposr-encoder")));
        Assert.True(File.Exists(Path.Combine(beside, "depth-anything-v2-small", "fp32.onnx")));
        Assert.False(library.RemoveDownload("depth-anything-v2-small"));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../elsewhere")]
    [InlineData("a\\b")]
    public void A_name_that_is_not_a_bare_folder_is_refused(string name)
    {
        ModelLibrary library = new([downloads]);
        Assert.Throws<ArgumentException>(() => library.RemoveDownload(name));
    }
}
