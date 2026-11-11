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

    [Fact]
    public void OneApplicationWithModeladoCliAsItsConsoleAlias()
    {
        XNamespace uap5 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/5";
        XNamespace desktop4 = "http://schemas.microsoft.com/appx/manifest/desktop/windows10/4";
        XElement[] applications = [.. Manifest().Descendants(Foundation + "Application")];

        // MOD79: a second application without a Start entry is refused by the Store as headless, so
        // there is one, the window's, in the Start menu.
        XElement application = Assert.Single(applications);
        Assert.Equal("Modelado.exe", (string?)application.Attribute("Executable"));
        Assert.Null(application.Descendants(Uap + "VisualElements").Single().Attribute("AppListEntry"));

        // MOD76: the Blender add-on and a terminal reach modelado-cli through its alias, on the
        // console, and the window through "modelado-cli window".
        XElement alias = Assert.Single(application.Descendants(uap5 + "Extension"), e => (string?)e.Attribute("Category") == "windows.appExecutionAlias");
        Assert.Equal("modelado-cli.exe", (string?)alias.Attribute("Executable"));
        Assert.Equal("console", (string?)alias.Element(uap5 + "AppExecutionAlias")!.Attribute(desktop4 + "Subsystem"));
        Assert.Equal("modelado-cli.exe", (string?)alias.Descendants(uap5 + "ExecutionAlias").Single().Attribute("Alias"));
    }

    [GeneratedRegex(@"Software\\Classes\\(\.[a-z0-9]+)\\OpenWithProgids")]
    private static partial Regex OpenWithProgids();
}
