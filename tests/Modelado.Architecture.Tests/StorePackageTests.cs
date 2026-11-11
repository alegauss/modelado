using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Modelado.Architecture.Tests;

/// <summary>
/// The Store package's manifest (build\msix\AppxManifest.xml) against the Partner Center reservation
/// (MOD73) and against what the Inno installer registers for the unpackaged build (MOD75), so the two
/// ways Modelado is installed cannot drift apart.
/// </summary>
public sealed partial class StorePackageTests
{
    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
    private static readonly string[] ModelTypes = [".glb", ".obj", ".ply", ".stl"];

    private static XDocument Manifest() => XDocument.Load(Path.Combine(Repository.Root, "build", "msix", "AppxManifest.xml"));

    [Fact]
    public void TheIdentityIsTheReservation()
    {
        XElement identity = Manifest().Root!.Element(Foundation + "Identity")!;

        Assert.Equal("Alegauss.Modelado", (string?)identity.Attribute("Name"));
        Assert.Equal("CN=1ADDE401-7D09-4729-8E49-C28AEEA80170", (string?)identity.Attribute("Publisher"));
        Assert.Equal("{VERSION}", (string?)identity.Attribute("Version"));
    }

    [Fact]
    public void ThePackageOpensTheTypesTheInstallerRegisters()
    {
        string iss = File.ReadAllText(Path.Combine(Repository.Root, "build", "installer.iss"));
        string[] installer = [.. OpenWithProgids().Matches(iss).Select(m => m.Groups[1].Value).Order(StringComparer.Ordinal)];
        string[] package = [.. Manifest().Descendants(Uap + "FileType").Select(e => e.Value.Trim()).Order(StringComparer.Ordinal)];

        Assert.Equal(ModelTypes, installer);
        Assert.Equal(installer, package);
    }

    [GeneratedRegex(@"Software\\Classes\\(\.[a-z0-9]+)\\OpenWithProgids")]
    private static partial Regex OpenWithProgids();
}
