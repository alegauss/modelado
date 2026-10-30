using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Modelado.Geometry;

namespace Modelado.App;

/// <summary>
/// The Export card's "Open in Blender" (MOD46): finds Blender and starts it importing the model
/// shown, written as a GLB in a folder of Modelado's own under %TEMP%. Blender is, in order, the
/// MODELADO_BLENDER variable (the path the add-on's tests use too), the program Windows opens .blend
/// files with, the newest Blender Foundation install, then a portable copy under
/// %LOCALAPPDATA%\Programs.
/// </summary>
public static class BlenderLauncher
{
    /// <summary>Blender's executable, or null where none is found.</summary>
    /// <returns>The path.</returns>
    public static string? Find()
    {
        if (Environment.GetEnvironmentVariable("MODELADO_BLENDER") is { Length: > 0 } named)
        {
            return File.Exists(named) ? named : null;
        }

        if (Registered() is { } registered)
        {
            return registered;
        }

        string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string local = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
        return Newest(Path.Combine(programs, "Blender Foundation"), "Blender*") ?? Newest(Path.Combine(local, "Programs"), "blender-*");
    }

    /// <summary>Writes the model and starts Blender importing it into an empty scene.</summary>
    /// <param name="blender">Blender's executable.</param>
    /// <param name="mesh">The model shown.</param>
    /// <param name="name">What the file is called, without its extension.</param>
    /// <returns>The GLB Blender opens.</returns>
    public static string Open(string blender, Mesh mesh, string name)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        string folder = Path.Combine(Path.GetTempPath(), "Modelado", "Blender");
        Directory.CreateDirectory(folder);
        string glb = Path.Combine(folder, $"{name}.glb");
        using (FileStream stream = File.Create(glb))
        {
            MeshExport.WriteGlb(mesh, stream);
        }

        ProcessStartInfo start = new(blender) { UseShellExecute = false };
        start.ArgumentList.Add("--python-expr");
        start.ArgumentList.Add($"import bpy; bpy.ops.wm.read_homefile(use_empty=True); bpy.ops.import_scene.gltf(filepath=r'{glb}')");
        Process.Start(start)?.Dispose();
        return glb;
    }

    // `"C:\...\blender-launcher.exe" "%1"`: the program .blend files open with, when it is Blender's.
    private static string? Registered()
    {
        try
        {
            using RegistryKey? command = Registry.ClassesRoot.OpenSubKey(@"blendfile\shell\open\command");
            if (command?.GetValue(null) is not string line || line.Length < 2)
            {
                return null;
            }

            string program = line.StartsWith('"') ? line[1..line.IndexOf('"', 1)] : line.Split(' ')[0];
            string blender = Path.Combine(Path.GetDirectoryName(program) ?? "", "blender.exe");
            return File.Exists(blender) ? blender : null;
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
        {
            return null;
        }
    }

    private static string? Newest(string parent, string pattern) => Directory.Exists(parent)
        ? Directory.GetDirectories(parent, pattern)
            .Select(folder => Path.Combine(folder, "blender.exe"))
            .Where(File.Exists)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
        : null;
}
