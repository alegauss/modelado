using System.Globalization;

namespace Modelado.App;

/// <summary>
/// MOD85: fills a format string from Properties/Strings.resx. The text is whole sentences with {0}
/// placeholders, never pieces joined in code, because a translation may put the values in another
/// order; the numbers are formatted in the culture the app runs in (MOD49).
/// </summary>
internal static class Ui
{
    /// <summary>The resource's format with its placeholders filled.</summary>
    /// <param name="format">A Strings property.</param>
    /// <param name="args">The values, in placeholder order.</param>
    /// <returns>The sentence.</returns>
    public static string Format(string format, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, format, args);
}
