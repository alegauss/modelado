using System.Text.Json;
using System.Text.Json.Nodes;

namespace Modelado.Geometry;

/// <summary>
/// The window's half of a round trip with the Blender add-on (MOD46), over a folder both share.
/// <list type="bullet">
/// <item>The add-on writes <c>session.json</c>, <c>{ "object": "Suzanne", "model": "model.glb" }</c>,
/// with the object's mesh beside it, and starts Modelado.exe --blender-session &lt;folder&gt;.</item>
/// <item>Each "Send to Blender" writes the version shown as <c>sent-N.glb</c>, then
/// <c>sent.json</c>, <c>{ "sequence": N, "model": "sent-N.glb", "label": "Smooth" }</c>. Each file
/// is written whole beside its name and moved into place, so the add-on, polling <c>sent.json</c>,
/// never reads half of one, and a model is always there before the line that names it.</item>
/// </list>
/// The add-on puts each send on the object it came from as one undo step; closing the window ends
/// the session.
/// </summary>
public sealed class BlenderSession
{
    /// <summary>The file the add-on describes the session in.</summary>
    public const string SessionFile = "session.json";

    /// <summary>The file each send is announced in.</summary>
    public const string SentFile = "sent.json";

    private BlenderSession(string folder, string objectName, string model)
    {
        Folder = folder;
        ObjectName = objectName;
        Model = model;
    }

    /// <summary>The shared folder.</summary>
    public string Folder { get; }

    /// <summary>The Blender object's name, which the caption and the source chip show.</summary>
    public string ObjectName { get; }

    /// <summary>The model the add-on wrote, to open first.</summary>
    public string Model { get; }

    /// <summary>How many versions were sent so far.</summary>
    public int Sent { get; private set; }

    /// <summary>Reads a session folder the add-on prepared.</summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The session.</returns>
    /// <exception cref="InvalidDataException">The folder holds no session this build reads.</exception>
    public static BlenderSession Open(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        string described = Path.Combine(folder, SessionFile);
        try
        {
            JsonNode session = JsonNode.Parse(File.ReadAllText(described)) ?? throw new InvalidDataException($"{described} is empty.");
            string objectName = session["object"]?.GetValue<string>() is { Length: > 0 } name ? name : throw new InvalidDataException($"{described} names no object.");
            string model = session["model"]?.GetValue<string>() is { Length: > 0 } file && Path.GetFileName(file) == file
                ? Path.Combine(folder, file)
                : throw new InvalidDataException($"{described} names no model file in its folder.");
            return new BlenderSession(folder, objectName, model);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InvalidDataException($"{folder} is not a Blender session: {error.Message}", error);
        }
    }

    /// <summary>Sends a version to Blender: its GLB, then the line that names it.</summary>
    /// <param name="mesh">The version shown.</param>
    /// <param name="label">What it is, as the history names it.</param>
    /// <returns>The send's sequence number, from 1.</returns>
    public int Send(Mesh mesh, string label)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        int sequence = Sent + 1;
        string model = $"sent-{sequence}.glb";
        WriteWhole(Path.Combine(Folder, model), stream => MeshExport.WriteGlb(mesh, stream));
        JsonObject line = new() { ["sequence"] = sequence, ["model"] = model, ["label"] = label };
        WriteWhole(Path.Combine(Folder, SentFile), stream => JsonSerializer.Serialize(stream, line));
        Sent = sequence;
        return sequence;
    }

    private static void WriteWhole(string path, Action<Stream> write)
    {
        string partial = path + ".partial";
        using (FileStream stream = File.Create(partial))
        {
            write(stream);
        }

        File.Move(partial, path, overwrite: true);
    }
}
