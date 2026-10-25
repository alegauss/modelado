using System.Text.RegularExpressions;

namespace Modelado.Architecture.Tests;

/// <summary>
/// MOD29: the version lives in Directory.Build.props and in the app's manifest, and
/// build\update-release.ps1 sets both; a release whose two disagree is refused here first.
/// </summary>
public sealed partial class VersionTests
{
    [Fact]
    public void TheManifestCarriesThePropsVersion()
    {
        string props = File.ReadAllText(Path.Combine(Repository.Root, "Directory.Build.props"));
        string manifest = File.ReadAllText(Path.Combine(Repository.Root, "src", "Modelado.App", "app.manifest"));

        string version = PropsVersion().Match(props).Groups[1].Value;
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
        Assert.Equal($"{version}.0", ManifestVersion().Match(manifest).Groups[1].Value);
    }

    [GeneratedRegex(@"<Version>\s*([0-9.]+)\s*</Version>")]
    private static partial Regex PropsVersion();

    [GeneratedRegex(@"<assemblyIdentity\s+version=""([0-9.]+)""")]
    private static partial Regex ManifestVersion();
}
