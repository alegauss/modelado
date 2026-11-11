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
}
