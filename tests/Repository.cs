namespace Modelado.Tests;

/// <summary>Paths in the repository, found once from the test's own folder.</summary>
internal static class Repository
{
    /// <summary>The repository's root: the folder holding Modelado.slnx.</summary>
    public static string Root { get; } = Find();

    /// <summary>The models export_models.py wrote, which the end-to-end tests use when present.</summary>
    public static string Models => Path.Combine(Root, ".cache", "models");

    /// <summary>The test images download_models.py fetched.</summary>
    public static string TestImages => Path.Combine(Root, ".cache", "weights", "test-images");

    /// <summary>Whether the exported models are here, so the end-to-end tests can run.</summary>
    public static bool HasModels => File.Exists(Path.Combine(Models, "triposr-encoder", "meta.json"));

    private static string Find()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Modelado.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Modelado.slnx was not found above the test's folder.");
    }
}
