namespace Modelado.Architecture.Tests;

/// <summary>
/// MOD49: the app and the CLI speak English only, so each sets en-US as the culture of every thread
/// before anything is formatted. Read from the files, as the rest of these tests are.
/// </summary>
public sealed class CultureTests
{
    [Theory]
    [InlineData("src", "Modelado.App", "App.xaml.cs")]
    [InlineData("tools", "Modelado.Cli", "Program.cs")]
    public void TheEntryPointSetsOneCultureForEveryThread(string folder, string project, string file)
    {
        string source = File.ReadAllText(Path.Combine(Repository.Root, folder, project, file));

        Assert.Contains("CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture", source, StringComparison.Ordinal);
        Assert.Contains("CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture", source, StringComparison.Ordinal);
        Assert.Contains("GetCultureInfo(\"en-US\")", source, StringComparison.Ordinal);
    }
}
