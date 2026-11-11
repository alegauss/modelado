using System.Runtime.InteropServices;

namespace Modelado.App;

/// <summary>
/// Whether this process runs inside an MSIX package, the Microsoft Store build (MOD73, MOD74), after
/// claude-tray's PackageIdentity (T409). Asked of the process at runtime, never set by a build flag, so
/// the exe the gate checks is the one that ships both ways.
///
/// <para>Under a package the updater stands down (MOD74): the Store owns updates, its policy forbids
/// an app that updates itself, and the Inno installer run from a <c>WindowsApps</c> folder would land
/// a second copy under <c>%LOCALAPPDATA%\Programs\Modelado</c> rather than replace the package.</para>
/// </summary>
internal static class PackageIdentity
{
    private const int ErrorInsufficientBuffer = 122;

    public static bool IsPackaged { get; } = Detect();

    /// <summary>The running package's family name (MOD77), or null when unpackaged.</summary>
    public static string? FamilyName { get; } = IsPackaged ? ReadFamilyName() : null;

    /// <summary>
    /// Where a person outside the package finds <paramref name="path"/> (MOD77): the package's LocalCache
    /// for a folder under %LOCALAPPDATA%, which Windows redirects there; anything else unchanged.
    /// </summary>
    public static string Visible(string path) =>
        Modelado.Inference.PackagedPaths.Visible(path, FamilyName, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    private static string? ReadFamilyName()
    {
        try
        {
            int length = 0;
            if (GetCurrentPackageFamilyName(ref length, null) != ErrorInsufficientBuffer)
            {
                return null;
            }

            char[] name = new char[length];
            return GetCurrentPackageFamilyName(ref length, name) == 0 ? new string(name, 0, Math.Max(0, length - 1)) : null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
    }

    private static bool Detect()
    {
        try
        {
            // A zero-length buffer answers ERROR_INSUFFICIENT_BUFFER when there is a package to name, and
            // APPMODEL_ERROR_NO_PACKAGE when there is none. Anything else is read as unpackaged, so a
            // failure here can never switch the updater off for a copy that still needs it.
            int length = 0;
            return GetCurrentPackageFullName(ref length, nint.Zero) == ErrorInsufficientBuffer;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, nint packageFullName);

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, [Out] char[]? packageFamilyName);
}
