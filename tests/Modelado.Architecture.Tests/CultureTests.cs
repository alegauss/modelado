namespace Modelado.Architecture.Tests;

/// <summary>
/// MOD49: the app and the CLI format numbers in en-US on every thread, set before anything is
/// formatted. MOD86: the CLI's text stays English too, while the app's text follows the language
/// About chose or Windows' own. Read from the files, as the rest of these tests are.
/// </summary>
public sealed class CultureTests
{
    [Fact]
    public void TheCliSetsOneCultureForEveryThread()
    {
        string source = File.ReadAllText(Path.Combine(Repository.Root, "tools", "Modelado.Cli", "Program.cs"));

        Assert.Contains("CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture", source, StringComparison.Ordinal);
        Assert.Contains("CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture", source, StringComparison.Ordinal);
        Assert.Contains("GetCultureInfo(\"en-US\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAppFormatsInOneCultureAndSpeaksTheChosenLanguage()
    {
        string source = File.ReadAllText(Path.Combine(Repository.Root, "src", "Modelado.App", "App.xaml.cs"));

        Assert.Contains("CultureInfo numbers = CultureInfo.GetCultureInfo(\"en-US\")", source, StringComparison.Ordinal);
        Assert.Contains("CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = numbers", source, StringComparison.Ordinal);
        Assert.Contains("CultureInfo language = InterfaceLanguage.ForThisRun()", source, StringComparison.Ordinal);
        Assert.Contains("CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = language", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheUiCasesRunInEnglishWhateverTheDesksLanguage()
    {
        string source = File.ReadAllText(Path.Combine(Repository.Root, "tests", "Modelado.Cases", "CasesRun.cs"));

        Assert.Contains("Environment.SetEnvironmentVariable(LanguageVariable, \"en\")", source, StringComparison.Ordinal);
    }
}
