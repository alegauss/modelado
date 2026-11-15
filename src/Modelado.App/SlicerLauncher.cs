using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Modelado.Geometry;

namespace Modelado.App;

/// <summary>A slicer found on this PC (MOD110).</summary>
/// <param name="Name">What the button calls it, e.g. Bambu Studio.</param>
/// <param name="Program">Its executable.</param>
public sealed record Slicer(string Name, string Program);

/// <summary>
/// The Export card's "Open in slicer" (MOD110): finds the slicers installed on the PC and starts the
/// chosen one on the model shown, written as a 3MF (in millimetres, laid on the plate, MOD109) in a
/// folder of Modelado's own under %TEMP%, its path on the command line, which all four open. A slicer
/// is found from its uninstall entry (its install folder, or the folder of the icon it registered)
/// and then from its usual folder under Program Files. MODELADO_SLICERS, "Name=path;Name=path", names
/// them instead, the way MODELADO_BLENDER names Blender, so a UI case can stand a stub in; set to
/// nothing, it means none. None of the four is published in the Microsoft Store, so the Store is not
/// searched.
/// </summary>
public static class SlicerLauncher
{
    // The name an uninstall entry starts with, the executable, and the folders under Program Files
    // it is usually installed to (a trailing * for a folder that carries the version).
    private static readonly (string Name, string Exe, string[] Folders)[] Known =
    [
        ("Bambu Studio", "bambu-studio.exe", ["Bambu Studio"]),
        ("OrcaSlicer", "orca-slicer.exe", ["OrcaSlicer"]),
        ("PrusaSlicer", "prusa-slicer.exe", [@"Prusa3D\PrusaSlicer"]),
        ("UltiMaker Cura", "UltiMaker-Cura.exe", ["UltiMaker Cura*"]),
    ];

    /// <summary>The slicers found, in the order above, each once.</summary>
    /// <returns>The list, empty where there is none.</returns>
    public static IReadOnlyList<Slicer> Find()
    {
        if (Environment.GetEnvironmentVariable("MODELADO_SLICERS") is { } named)
        {
            return Named(named);
        }

        List<Slicer> found = [];
        IReadOnlyList<(string DisplayName, string? Location, string? Icon)> entries = Uninstall();
        foreach ((string name, string exe, string[] folders) in Known)
        {
            string? program = entries
                .Where(e => e.DisplayName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                .SelectMany(e => new[] { e.Location, IconFolder(e.Icon) })
                .Where(folder => !string.IsNullOrEmpty(folder))
                .Select(folder => Path.Combine(folder!, exe))
                .FirstOrDefault(File.Exists)
                ?? InProgramFiles(exe, folders);
            if (program is not null)
            {
                found.Add(new Slicer(name, program));
            }
        }

        return found;
    }

    /// <summary>"Name=path;Name=path" read as slicers, those whose program exists.</summary>
    /// <param name="named">The list.</param>
    /// <returns>The slicers.</returns>
    public static IReadOnlyList<Slicer> Named(string named)
    {
        ArgumentNullException.ThrowIfNull(named);
        return [.. named.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2 && pair[0].Length > 0 && File.Exists(pair[1]))
            .Select(pair => new Slicer(pair[0], pair[1]))];
    }

    /// <summary>Writes the model as a 3MF and starts the slicer on it.</summary>
    /// <param name="slicer">The slicer.</param>
    /// <param name="mesh">The model, as Save as would write it for printing.</param>
    /// <param name="name">What the file is called, without its extension.</param>
    /// <returns>The 3MF the slicer opens.</returns>
    public static string Open(Slicer slicer, Mesh mesh, string name)
    {
        ArgumentNullException.ThrowIfNull(slicer);
        ArgumentNullException.ThrowIfNull(mesh);
        string folder = Path.Combine(Path.GetTempPath(), "Modelado", "Slicer");
        Directory.CreateDirectory(folder);
        string model = Path.Combine(folder, $"{name}.3mf");
        using (FileStream stream = File.Create(model))
        {
            MeshExport.Write3mf(mesh, stream);
        }

        ProcessStartInfo start = new(slicer.Program) { UseShellExecute = false };
        start.ArgumentList.Add(model);
        Process.Start(start)?.Dispose();
        return model;
    }

    // DisplayIcon is "C:\...\bambu-studio.exe,0" or quoted; its folder is where the program is.
    private static string? IconFolder(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return null;
        }

        string path = icon.Trim().Trim('"');
        int comma = path.LastIndexOf(',');
        path = comma > 0 && !File.Exists(path) ? path[..comma].Trim('"') : path;
        try
        {
            return Path.GetDirectoryName(path);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? InProgramFiles(string exe, string[] folders)
    {
        foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }.Where(r => r.Length > 0).Distinct())
        {
            foreach (string folder in folders)
            {
                if (!folder.EndsWith('*'))
                {
                    string program = Path.Combine(root, folder, exe);
                    if (File.Exists(program))
                    {
                        return program;
                    }

                    continue;
                }

                string parent = Path.Combine(root, Path.GetDirectoryName(folder) ?? "");
                if (Directory.Exists(parent)
                    && Directory.GetDirectories(parent, Path.GetFileName(folder))
                        .Select(d => Path.Combine(d, exe)).Where(File.Exists)
                        .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase).FirstOrDefault() is { } newest)
                {
                    return newest;
                }
            }
        }

        return null;
    }

    // Every uninstall entry's name, install folder and icon, machine-wide (both views) and the user's.
    private static List<(string DisplayName, string? Location, string? Icon)> Uninstall()
    {
        List<(string, string?, string?)> entries = [];
        foreach ((RegistryHive hive, RegistryView view) in new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32), (RegistryHive.CurrentUser, RegistryView.Default),
        })
        {
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                foreach (string sub in uninstall?.GetSubKeyNames() ?? [])
                {
                    using RegistryKey? entry = uninstall!.OpenSubKey(sub);
                    if (entry?.GetValue("DisplayName") is string displayName)
                    {
                        entries.Add((displayName, entry.GetValue("InstallLocation") as string, entry.GetValue("DisplayIcon") as string));
                    }
                }
            }
            catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // A hive this user cannot read holds nothing to offer.
            }
        }

        return entries;
    }
}
