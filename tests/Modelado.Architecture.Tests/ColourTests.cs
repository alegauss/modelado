using System.Text.RegularExpressions;

namespace Modelado.Architecture.Tests;

/// <summary>
/// MOD4: every colour Modelado decides is written in Brand.cs and nowhere else, so changing one is
/// one edit and two surfaces cannot drift apart (from PhotoBoost's ColourTests). C# and XAML under
/// src\ and tools\; the matcher is run against text written to break it first.
/// </summary>
public sealed partial class ColourTests
{
    private const string Home = "Brand.cs";

    /// <summary>The folders whose code paints something: the libraries, the app and the command line.</summary>
    private static readonly string[] Scanned = ["src", "tools"];

    /// <summary>WPF's constructors given literal channels.</summary>
    [GeneratedRegex(@"\bColor\.From(?:Rgb|Argb|ScRgb)\s*\(\s*(?:0x[0-9A-Fa-f]{1,2}|\d)")]
    private static partial Regex WpfLiteral { get; }

    /// <summary>A hex colour in a string or a XAML attribute: "#1E1E1E", "#FF1E1E1E", "#FFF".</summary>
    [GeneratedRegex(@"""#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3,4})""")]
    private static partial Regex HexLiteral { get; }

    /// <summary>
    /// A named colour from WPF's tables, Colors.Red or Brushes.Red, except Transparent. Not a member
    /// that happens to share the name, as a mesh's own mesh.Colors is.
    /// </summary>
    [GeneratedRegex(@"(?<![.\w])(?:Colors|Brushes)\.(?!Transparent\b)[A-Z][A-Za-z]+")]
    private static partial Regex NamedLiteral { get; }

    /// <summary>A XAML attribute given a named colour: Background="Red", Fill="White".</summary>
    [GeneratedRegex(@"\b(?:Background|Foreground|Fill|Stroke|BorderBrush|Color)=""(?!Transparent"")(?!\{)[A-Za-z]+""")]
    private static partial Regex XamlNamed { get; }

    /// <summary>A packed opaque pixel, 0xFF2B2B2B, the way the software renderer spells a colour.</summary>
    [GeneratedRegex(@"\b0xFF[0-9A-Fa-f]{6}\b(?!u?\s*[|&>])")]
    private static partial Regex PackedLiteral { get; }

    [Fact]
    public void EveryColourIsWrittenInBrand()
    {
        string[] strays = [.. Sources()
            .Where(path => Path.GetFileName(path) != Home)
            .SelectMany(path => Declared(File.ReadAllText(path)).Select(found => $"{Path.GetRelativePath(Repository.Root, path)}: {found}"))];

        Assert.True(strays.Length == 0, "Colours are declared in Brand.cs; refer to Brand or a Fluent brush instead: " + string.Join("; ", strays));
    }

    [Fact]
    public void BrandDeclaresSomething() =>
        Assert.Contains(Sources(), path => Path.GetFileName(path) == Home);

    [Theory]
    [InlineData("var c = Color.FromRgb(10, 20, 30);")]
    [InlineData("Color.FromArgb(0xFF, 1, 2, 3)")]
    [InlineData("<Grid Background=\"#FF1C1F24\" />")]
    [InlineData("string s = \"#FF00FF00\";")]
    [InlineData("brush = Brushes.Red;")]
    [InlineData("<Rectangle Fill=\"White\" />")]
    [InlineData("pixels[index] = 0xFF2B2B2B;")]
    [InlineData("Background = (0xFF3A3F47, 0xFF1C1F24);")]
    public void TheMatcherFindsEverySpelling(string text) => Assert.NotEmpty(Declared(text));

    [Theory]
    [InlineData("Color.FromRgb(colour.R, colour.G, colour.B)")]
    [InlineData("<Grid Background=\"{DynamicResource ViewportGroundBrush}\" />")]
    [InlineData("<Border Background=\"Transparent\" />")]
    [InlineData("ground.SetValue(Border.BackgroundProperty, Brushes.Transparent);")]
    [InlineData("// see issue #123 and \"#region\"")]
    [InlineData("return 0xFF000000u | (r << 16) | (g << 8) | b;")]
    [InlineData("uint p = 0xFF00FF00 & mask;")]
    [InlineData("mesh.Colors.Select(c => c * 255)")]
    public void TheMatcherIgnoresWhatOnlyLooksLikeAColour(string text) => Assert.Empty(Declared(text));

    private static IEnumerable<string> Declared(string text) =>
        new[] { WpfLiteral, HexLiteral, NamedLiteral, XamlNamed, PackedLiteral }.SelectMany(regex => regex.Matches(text).Select(m => m.Value));

    private static string[] Sources() =>
        [.. Scanned
            .SelectMany(folder => Directory.GetFiles(Path.Combine(Repository.Root, folder), "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".xaml", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))];
}
