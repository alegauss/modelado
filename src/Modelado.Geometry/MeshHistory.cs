namespace Modelado.Geometry;

/// <summary>
/// The versions of a model as it is edited, for undo and redo. Meshes are never changed in
/// place, so a version is the mesh itself and keeping one costs nothing until it is replaced.
/// </summary>
public sealed class MeshHistory
{
    private readonly List<(Mesh Mesh, string Label)> versions = [];
    private int current = -1;

    /// <summary>Creates a history that keeps at most <paramref name="limit"/> versions.</summary>
    /// <param name="limit">The oldest versions past this are forgotten.</param>
    public MeshHistory(int limit = 20)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 2);
        Limit = limit;
    }

    /// <summary>The most versions kept.</summary>
    public int Limit { get; }

    /// <summary>The version shown, or null before the first.</summary>
    public Mesh? Current => current >= 0 ? versions[current].Mesh : null;

    /// <summary>What made the version shown, e.g. "Simplify to 25%".</summary>
    public string? CurrentLabel => current >= 0 ? versions[current].Label : null;

    /// <summary>What made each version, oldest first: what the History card lists (MOD12).</summary>
    public IReadOnlyList<string> Labels => [.. versions.Select(v => v.Label)];

    /// <summary>Where the version shown is in <see cref="Labels"/>, or -1 before the first.</summary>
    public int Position => current;

    /// <summary>Whether there is an older version.</summary>
    public bool CanUndo => current > 0;

    /// <summary>Whether an undone version can come back.</summary>
    public bool CanRedo => current >= 0 && current < versions.Count - 1;

    /// <summary>Starts over with a new model (a reconstruction or an import).</summary>
    /// <param name="mesh">The model.</param>
    /// <param name="label">What made it.</param>
    public void Reset(Mesh mesh, string label)
    {
        versions.Clear();
        current = -1;
        Push(mesh, label);
    }

    /// <summary>Records an edit; versions undone before it are dropped.</summary>
    /// <param name="mesh">The edited model.</param>
    /// <param name="label">The edit.</param>
    public void Push(Mesh mesh, string label)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        versions.RemoveRange(current + 1, versions.Count - current - 1);
        versions.Add((mesh, label));
        if (versions.Count > Limit)
        {
            versions.RemoveAt(0);
        }

        current = versions.Count - 1;
    }

    /// <summary>Goes back one version.</summary>
    /// <returns>The version now shown.</returns>
    public Mesh? Undo()
    {
        if (CanUndo)
        {
            current--;
        }

        return Current;
    }

    /// <summary>Goes forward one version.</summary>
    /// <returns>The version now shown.</returns>
    public Mesh? Redo()
    {
        if (CanRedo)
        {
            current++;
        }

        return Current;
    }
}
