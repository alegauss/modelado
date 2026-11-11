using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>
/// MOD77: under an MSIX package a folder under %LOCALAPPDATA% is named and opened where Explorer,
/// which runs outside the package, finds it: the package's LocalCache.
/// </summary>
public sealed class PackagedPathsTests
{
    private const string Family = "Alegauss.Modelado_d7cpypw1jc0bm";
    private static readonly string Local = Path.Combine(Path.GetTempPath(), "modelado-local");
    private static readonly string Models = Path.Combine(Local, "Modelado", "models");

    [Fact]
    public void UnpackagedPathsAreUnchanged() =>
        Assert.Equal(Models, PackagedPaths.Visible(Models, null, Local));

    [Fact]
    public void UnderAPackageTheDownloadsAreInItsLocalCache() =>
        Assert.Equal(
            Path.Combine(Local, "Packages", Family, "LocalCache", "Local", "Modelado", "models"),
            PackagedPaths.Visible(Models, Family, Local));

    [Fact]
    public void AFolderChosenElsewhereIsNotRedirected()
    {
        string chosen = Path.Combine(Path.GetTempPath(), "elsewhere", "models");
        Assert.Equal(chosen, PackagedPaths.Visible(chosen, Family, Local));
    }

    [Fact]
    public void APathAlreadyInPackagesIsLeftAlone()
    {
        string cache = Path.Combine(Local, "Packages", Family, "LocalCache", "Local", "Modelado");
        Assert.Equal(cache, PackagedPaths.Visible(cache, Family, Local));
    }

    [Fact]
    public void AFolderThatOnlySharesThePrefixIsNotUnderIt()
    {
        string sibling = Local + "-other" + Path.DirectorySeparatorChar + "Modelado";
        Assert.Equal(sibling, PackagedPaths.Visible(sibling, Family, Local));
    }
}
