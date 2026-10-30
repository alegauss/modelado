using System.Numerics;
using System.Text.Json.Nodes;
using Modelado.Geometry;

namespace Modelado.Geometry.Tests;

/// <summary>MOD46: the session folder the window shares with the Blender add-on.</summary>
public sealed class BlenderSessionTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("modelado-session-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private static Mesh Triangle(float z) => new([new(0, 0, z), new(1, 0, z), new(0, 1, z)], [0, 1, 2]);

    [Fact]
    public void Each_send_writes_its_model_then_names_it_in_sent_json()
    {
        File.WriteAllText(Path.Combine(folder, "session.json"), """{ "object": "Suzanne", "model": "model.glb" }""");
        BlenderSession session = BlenderSession.Open(folder);
        Assert.Equal(("Suzanne", Path.Combine(folder, "model.glb")), (session.ObjectName, session.Model));

        Assert.Equal(1, session.Send(Triangle(0), "Smooth"));
        Assert.Equal(2, session.Send(Triangle(2), "Simplify"));

        JsonNode sent = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "sent.json")))!;
        Assert.Equal((2, "sent-2.glb", "Simplify"), (sent["sequence"]!.GetValue<int>(), sent["model"]!.GetValue<string>(), sent["label"]!.GetValue<string>()));
        Assert.Equal(new Vector3(0, 0, 2), MeshImport.Load(Path.Combine(folder, "sent-2.glb")).Positions[0]);
        Assert.True(File.Exists(Path.Combine(folder, "sent-1.glb")));
        Assert.Empty(Directory.GetFiles(folder, "*.partial"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{ "object": "Suzanne" }""")]
    [InlineData("""{ "object": "Suzanne", "model": "../elsewhere.glb" }""")]
    [InlineData("not json")]
    public void A_folder_without_a_whole_session_is_refused(string? described)
    {
        if (described is not null)
        {
            File.WriteAllText(Path.Combine(folder, "session.json"), described);
        }

        Assert.Throws<InvalidDataException>(() => BlenderSession.Open(folder));
    }
}
