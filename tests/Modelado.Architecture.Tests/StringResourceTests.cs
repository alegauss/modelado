using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Modelado.Architecture.Tests;

/// <summary>
/// MOD85: every string the window shows lives in Properties/Strings.resx, the neutral English a
/// translation fills, so a new string cannot skip the file. Read from the files, as the rest of
/// these tests are. Only the app's interface is held to it: the CLI, the Blender add-on, the site,
/// the Store listing, logs, exception messages and CommandLine's refusals (which name flags for
/// scripts) stay English. Code fills whole sentences through Ui.Format, never joined pieces.
/// Strings is generated from the resx at build time; the csproj says how it reaches the XAML.
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

    /// <summary>The app's own code files, each one a place a shown string could be written.</summary>
    public static TheoryData<string> CodeFiles()
    {
        TheoryData<string> files = [];
        foreach (string path in Directory.EnumerateFiles(App, "*.cs", SearchOption.TopDirectoryOnly))
        {
            files.Add(Path.GetFileName(path));
        }

        return files;
    }

    // A literal set on a shown property, or handed to a method that shows it, in the code-behind.
    // An interpolated string counts only for the words outside its holes, so "{a}{Strings.X}{b}"
    // passes and $"Saved {name}" does not.
    [Theory]
    [MemberData(nameof(CodeFiles))]
    public void NoCodeShowsLiteralText(string file)
    {
        string source = File.ReadAllText(Path.Combine(App, file));
        List<string> literals = [];
        foreach (Match match in ShownInCode().Matches(source))
        {
            string words = Holes().Replace(match.Groups["value"].Value, "");
            if (words.Count(char.IsLetter) >= 2)
            {
                literals.Add(match.Value);
            }
        }

        Assert.True(literals.Count == 0,
            $"{file} shows text that is not in Properties/Strings.resx; use Strings.<Key> or Ui.Format:\n  "
            + string.Join("\n  ", literals));
    }

    [Fact]
    public void EveryResourceIsUsed()
    {
        string sources = string.Concat(Directory.EnumerateFiles(App, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".xaml", StringComparison.Ordinal))
            .Select(File.ReadAllText));

        string[] unused = [.. ResourceKeys().Where(key => !sources.Contains($"Strings.{key}", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

        Assert.Empty(unused);
    }

    // MOD86: the Store lists the languages the package declares, so each translation the app ships
    // (Strings.<language>.resx) is declared beside English, and nothing else is.
    [Fact]
    public void EveryShippedTranslationIsDeclaredToTheStore()
    {
        string manifest = File.ReadAllText(Path.Combine(Repository.Root, "build", "msix", "AppxManifest.xml"));
        string[] declared = [.. DeclaredLanguage().Matches(manifest).Select(m => m.Groups["language"].Value.ToLowerInvariant()).Order(StringComparer.Ordinal)];
        string[] shipped = [.. Directory.EnumerateFiles(Path.Combine(App, "Properties"), "Strings.*.resx")
            .Select(path => Path.GetFileNameWithoutExtension(path)["Strings.".Length..].ToLowerInvariant())
            .Append("en-us")
            .Order(StringComparer.Ordinal)];

        Assert.Equal(shipped, declared);
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

    [GeneratedRegex(@"(\b(Text|Content|ToolTip|Title|Header)\s*=\s*|\b(LastRunSaid|ShowSource|SaySource|SayRemoval|ShowOpenError|EditAsync|SetBusy|MessageBox\.Show)\((this,\s*)?)\$?""(?<value>(\\.|[^""\\])*)""")]
    private static partial Regex ShownInCode();

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Holes();

    [GeneratedRegex(@"<Resource Language=""(?<language>[^""]+)""")]
    private static partial Regex DeclaredLanguage();
}
