using System.Text.RegularExpressions;

namespace Modelado.Architecture.Tests;

/// <summary>
/// MOD136: a WPF ItemsControl keys its items' automation peers by Equals, so a list rebuilt with rows
/// equal by value hands the new rows the old peers and drops them out of the accessibility tree while
/// they still show (MOD94 found it on Recent). Every row or choice record Modelado.App binds as a list
/// item is therefore equal only to itself.
/// </summary>
public sealed partial class RowIdentityTests
{
    private static readonly string App = Path.Combine(Repository.Root, "src", "Modelado.App");

    [GeneratedRegex(@"private sealed record (\w+(?:Row|Choice))\b")]
    private static partial Regex ListRecord();

    [Fact]
    public void Every_list_row_record_is_equal_only_to_itself()
    {
        List<string> found = [];
        List<string> byValue = [];
        foreach (string file in Directory.GetFiles(App, "*.cs"))
        {
            string source = File.ReadAllText(file);
            foreach (Match match in ListRecord().Matches(source))
            {
                string type = match.Groups[1].Value;
                found.Add(type);
                if (!source.Contains($"public bool Equals({type}? other) => ReferenceEquals(this, other);", StringComparison.Ordinal)
                    || !source.Contains("RuntimeHelpers.GetHashCode(this)", StringComparison.Ordinal))
                {
                    byValue.Add($"{Path.GetFileName(file)}: {type}");
                }
            }
        }

        Assert.Contains("SourceRow", found);
        Assert.Contains("RecentRow", found);
        Assert.Empty(byValue);
    }
}
