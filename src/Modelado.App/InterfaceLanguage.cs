using System.Globalization;
using System.Resources;
using Modelado.Inference;

namespace Modelado.App;

/// <summary>
/// MOD86: which languages the window can show, the ones a Strings satellite assembly ships for
/// beside English, and the one it shows this run (<see cref="LanguageSetting.Resolve"/>). A change
/// applies at the next start rather than re-binding the live window.
/// </summary>
internal static class InterfaceLanguage
{
    /// <summary>English, then each planned language whose translation this build carries.</summary>
    public static IReadOnlyList<string> Available { get; } =
        [LanguageSetting.English, .. LanguageSetting.Planned.Skip(1).Where(Ships)];

    /// <summary>
    /// The language this run shows, read once at start, before anything sets the UI culture: until
    /// then it is the user's Windows display language.
    /// </summary>
    /// <returns>Its culture.</returns>
    public static CultureInfo ForThisRun()
    {
        Windows = CultureInfo.CurrentUICulture;
        return CultureInfo.GetCultureInfo(Shown(LanguageSetting.Load(SettingsFile.DefaultPath)));
    }

    /// <summary>The user's Windows display language, as it was before the app set its own.</summary>
    public static CultureInfo Windows { get; private set; } = CultureInfo.CurrentUICulture;

    /// <summary>The language a start with this choice shows.</summary>
    /// <param name="chosen">A language's name, or null to follow Windows.</param>
    /// <returns>Its name.</returns>
    public static string Shown(string? chosen) =>
        LanguageSetting.Resolve(Environment.GetEnvironmentVariable(LanguageSetting.Variable), chosen, Windows, Available);

    /// <summary>A language as the list names it: in its own words, so a reader finds theirs.</summary>
    /// <param name="name">The language's name, e.g. pt-BR.</param>
    /// <returns>e.g. português (Brasil).</returns>
    public static string Native(string name) => CultureInfo.GetCultureInfo(name).NativeName;

    // A satellite only, never the neutral English the parents fall back to.
    private static bool Ships(string name)
    {
        try
        {
            return Strings.ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo(name), createIfNotExists: true, tryParents: false) is not null;
        }
        catch (MissingManifestResourceException)
        {
            return false;
        }
    }
}
