namespace Modelado.Inference;

/// <summary>
/// Where a path under %LOCALAPPDATA% really is for a person looking from outside an MSIX package
/// (MOD77). In a full trust package Windows redirects new files under AppData into the package's
/// private <c>LocalCache</c>: inside the process nothing changes, but Explorer runs outside the
/// package and sees the real, empty folder. So a folder Modelado names to a person, or opens in
/// Explorer, is mapped here first. A folder the person chose elsewhere is not redirected and comes
/// back as it was.
/// </summary>
public static class PackagedPaths
{
    /// <summary>The path a person outside the package finds <paramref name="path"/> at.</summary>
    /// <param name="path">A folder the app writes to.</param>
    /// <param name="packageFamilyName">The running package's family name, or null when unpackaged.</param>
    /// <param name="localAppData">%LOCALAPPDATA%.</param>
    /// <returns>The redirected folder under the package's LocalCache, or <paramref name="path"/> unchanged.</returns>
    public static string Visible(string path, string? packageFamilyName, string localAppData)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(localAppData);
        if (string.IsNullOrEmpty(packageFamilyName) || localAppData.Length == 0)
        {
            return path;
        }

        string full = Path.GetFullPath(path);
        string local = Path.TrimEndingDirectorySeparator(Path.GetFullPath(localAppData));
        string packages = Path.Combine(local, "Packages") + Path.DirectorySeparatorChar;
        bool under = full.StartsWith(local + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (!under || full.StartsWith(packages, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        string relative = full[(local.Length + 1)..];
        return Path.Combine(local, "Packages", packageFamilyName, "LocalCache", "Local", relative);
    }
}
