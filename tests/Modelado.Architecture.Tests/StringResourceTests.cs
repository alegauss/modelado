using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Modelado.Architecture.Tests;

/// <summary>
/// MOD85: every string the window shows lives in Properties/Strings.resx, the neutral English a
/// translation fills, so a new string cannot skip the file. Read from the files, as the rest of
/// these tests are.
/// </summary>
public sealed partial class StringResourceTests
{
    private static readonly string App = Path.Combine(Repository.Root, "src", "Modelado.App");

    /// <summary>The XAML files of the app, each one a source of interface text.</summary>
    public static TheoryData<string> XamlFiles()
    {
        TheoryData<string> files = [];
        foreach (string path in Directory.EnumerateFiles(App, "*.xaml", SearchOption.TopDirectoryOnly))
        {
            files.Add(Path.GetFileName(path));
        }

        return files;
    }

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void NoXamlAttributeHoldsLiteralText(string file)
    {
        string source = File.ReadAllText(Path.Combine(App, file));
        List<string> literals = [];
        foreach (Match match in ShownAttribute().Matches(source))
        {
            // Decoded first, so an icon's &#xE80F; is the one glyph it draws, not six letters.
            string value = System.Net.WebUtility.HtmlDecode(match.Groups["value"].Value);
            bool markup = value.StartsWith('{') && !value.StartsWith("{}", StringComparison.Ordinal);

            // Two letters make a word to translate; a glyph, a number, "25%" or an axis's "X" do not.
            if (!markup && value.Count(char.IsLetter) >= 2)
            {
                literals.Add($"{match.Groups["attribute"].Value}=\"{value}\"");
            }
        }

        Assert.True(literals.Count == 0,
            $"{file} shows text that is not in Properties/Strings.resx; use {{x:Static p:Strings.<Key>}}:\n  "
            + string.Join("\n  ", literals));
    }

    [Theory]
    [MemberData(nameof(XamlFiles))]
    public void EveryKeyTheXamlNamesIsInTheResourceFile(string file)
    {
        string source = File.ReadAllText(Path.Combine(App, file));
        HashSet<string> keys = ResourceKeys();

        string[] missing = [.. StaticString().Matches(source).Select(m => m.Groups["key"].Value).Where(k => !keys.Contains(k))];

        Assert.Empty(missing);
    }

    [Fact]
    public void TheResourceFileIsTheNeutralCultureTheAssemblyDeclares()
    {
        string props = File.ReadAllText(Path.Combine(Repository.Root, "Directory.Build.props"));
        string project = File.ReadAllText(Path.Combine(App, "Modelado.App.csproj"));

        Assert.Contains("<NeutralLanguage>en</NeutralLanguage>", props, StringComparison.Ordinal);
        Assert.Contains("<StronglyTypedClassName>Strings</StronglyTypedClassName>", project, StringComparison.Ordinal);
        Assert.NotEmpty(ResourceKeys());
    }

    private static HashSet<string> ResourceKeys() =>
        [.. XDocument.Load(Path.Combine(App, "Properties", "Strings.resx"))
            .Root!.Elements("data").Select(d => (string)d.Attribute("name")!)];

    [GeneratedRegex(@"\s(?<attribute>Text|Content|Header|ToolTip|Title)=""(?<value>[^""]*)""")]
    private static partial Regex ShownAttribute();

    [GeneratedRegex("""\{x:Static p:Strings\.(?<key>\w+)\}""")]
    private static partial Regex StaticString();
}
