using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

public sealed class MeshExportTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("modelado-export-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private static Mesh Coloured() => MarchingCubes.Extract(MarchingCubesTests.Sphere(16, 0.5f), 0) is { } sphere
        ? new Mesh(sphere.Positions, sphere.Triangles, [.. sphere.Positions.Select(p => new Vector3(1, 0.5f, 0))])
        : throw new InvalidOperationException();

    private static Mesh Textured()
    {
        RgbaImage texture = new(4, 4);
        Array.Fill(texture.Pixels, (byte)255);
        return new Mesh(
            [new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)],
            [0, 1, 2],
            [new(1, 1, 1), new(1, 1, 1), new(1, 1, 1)],
            [new(0, 1), new(1, 1), new(0, 0)],
            texture,
            doubleSided: true);
    }

    private static (JsonDocument Json, byte[] Bin) ReadGlb(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        Assert.Equal(0x46546C67u, BinaryPrimitives.ReadUInt32LittleEndian(file));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(4)));
        Assert.Equal((uint)file.Length, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(8)));
        int jsonLength = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(12));
        JsonDocument json = JsonDocument.Parse(file.AsMemory(20, jsonLength));
        int binLength = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(20 + jsonLength));
        return (json, file.AsSpan(28 + jsonLength, binLength).ToArray());
    }

    [Fact]
    public void Glb_holds_every_vertex_and_index_with_bounds()
    {
        Mesh mesh = Coloured();
        string path = Path.Combine(folder, "m.glb");
        MeshExport.Save(mesh, path);
        (JsonDocument json, byte[] bin) = ReadGlb(path);

        JsonElement primitive = json.RootElement.GetProperty("meshes")[0].GetProperty("primitives")[0];
        JsonElement accessors = json.RootElement.GetProperty("accessors");
        JsonElement position = accessors[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
        Assert.Equal(mesh.VertexCount, position.GetProperty("count").GetInt32());
        Assert.Equal(mesh.Triangles.Length, accessors[primitive.GetProperty("indices").GetInt32()].GetProperty("count").GetInt32());
        Assert.True(primitive.GetProperty("attributes").TryGetProperty("COLOR_0", out _));
        Assert.Equal(mesh.Bounds().Max.X, position.GetProperty("max")[0].GetSingle());
        foreach (JsonElement view in json.RootElement.GetProperty("bufferViews").EnumerateArray())
        {
            Assert.Equal(0, view.GetProperty("byteOffset").GetInt32() % 4);
            Assert.True(view.GetProperty("byteOffset").GetInt32() + view.GetProperty("byteLength").GetInt32() <= bin.Length);
        }
    }

    private static (string MimeType, byte[] Head) EmbeddedImage(JsonDocument json, byte[] bin)
    {
        JsonElement image = json.RootElement.GetProperty("images")[0];
        JsonElement view = json.RootElement.GetProperty("bufferViews")[image.GetProperty("bufferView").GetInt32()];
        return (image.GetProperty("mimeType").GetString()!, bin.AsSpan(view.GetProperty("byteOffset").GetInt32(), 8).ToArray());
    }

    [Fact]
    public void A_textured_glb_embeds_an_opaque_texture_as_jpeg_and_leaves_out_vertex_colours()
    {
        string path = Path.Combine(folder, "t.glb");
        MeshExport.Save(Textured(), path);
        (JsonDocument json, byte[] bin) = ReadGlb(path);

        (string mimeType, byte[] head) = EmbeddedImage(json, bin);
        Assert.Equal("image/jpeg", mimeType);
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, head[..3]);
        Assert.Equal("JPEG", MeshExport.GlbTextureFormat(Textured()));
        JsonElement attributes = json.RootElement.GetProperty("meshes")[0].GetProperty("primitives")[0].GetProperty("attributes");
        Assert.False(attributes.TryGetProperty("COLOR_0", out _));
        Assert.True(attributes.TryGetProperty("TEXCOORD_0", out _));
        Assert.True(json.RootElement.GetProperty("materials")[0].GetProperty("doubleSided").GetBoolean());
    }

    [Fact]
    public void A_cut_out_texture_stays_png_and_keeps_its_transparency()
    {
        Mesh mesh = Textured();
        mesh.Texture!.Pixels[3] = 0; // one transparent texel, as a removed background leaves
        string path = Path.Combine(folder, "cut.glb");
        MeshExport.Save(mesh, path);
        (JsonDocument json, byte[] bin) = ReadGlb(path);

        (string mimeType, byte[] head) = EmbeddedImage(json, bin);
        Assert.Equal("image/png", mimeType);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, head);
        Assert.Equal(0, MeshImport.Load(path).Texture!.Pixels[3]);
    }

    [Fact]
    public void The_canyon_relief_sheds_its_png_weight_and_opens_with_the_same_look()
    {
        // The site's landscape relief, written with its 768 px texture as PNG before MOD63: 4.1 MB,
        // of which the PNG is 1.0 MB and the geometry the rest (MOD66).
        string original = Path.Combine(Repository.Root, "site", "public", "demo", "studio", "relief-landscape.glb");
        Mesh relief = MeshImport.Load(original);
        Assert.False(relief.Texture!.HasTransparency);
        string path = Path.Combine(folder, "canyon.glb");
        MeshExport.Save(relief, path);

        long before = new FileInfo(original).Length, size = new FileInfo(path).Length;
        int jpeg = ImageCodec.EncodeJpeg(relief.Texture).Length;
        TestContext.Current.TestOutputHelper?.WriteLine($"canyon: {before:N0} -> {size:N0} bytes; the texture as JPEG {jpeg:N0}");
        Assert.True(jpeg < 250_000, $"the texture is {jpeg:N0} bytes as JPEG");
        Assert.True(size < before - 750_000, $"the canyon relief went from {before:N0} to {size:N0} bytes");

        RgbaImage texture = relief.Texture, after = MeshImport.Load(path).Texture!;
        Assert.Equal((texture.Width, texture.Height), (after.Width, after.Height));
        double difference = 0;
        for (int i = 0; i < texture.Pixels.Length; i++)
        {
            difference += Math.Abs(texture.Pixels[i] - after.Pixels[i]);
        }

        // JPEG at quality 90: a couple of levels out of 255 on average, not a visible change.
        Assert.True(difference / texture.Pixels.Length < 3, $"mean difference {difference / texture.Pixels.Length:0.00}");
    }

    [Fact]
    public void Stl_has_fifty_bytes_per_triangle()
    {
        Mesh mesh = Coloured();
        string path = Path.Combine(folder, "m.stl");
        MeshExport.Save(mesh, path);

        byte[] file = File.ReadAllBytes(path);
        Assert.Equal(84 + (50 * mesh.TriangleCount), file.Length);
        Assert.Equal((uint)mesh.TriangleCount, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(80)));
    }

    [Fact]
    public void Ply_header_counts_and_colours()
    {
        Mesh mesh = Coloured();
        string path = Path.Combine(folder, "m.ply");
        MeshExport.Save(mesh, path);

        byte[] file = File.ReadAllBytes(path);
        string text = Encoding.ASCII.GetString(file);
        int body = text.IndexOf("end_header\n", StringComparison.Ordinal) + 11;
        Assert.Contains($"element vertex {mesh.VertexCount}", text[..body], StringComparison.Ordinal);
        Assert.Contains($"element face {mesh.TriangleCount}", text[..body], StringComparison.Ordinal);
        Assert.Equal(body + (mesh.VertexCount * 15) + (mesh.TriangleCount * 13), file.Length);
        Assert.Equal(255, file[body + 12]);
        Assert.Equal(128, file[body + 13]);
    }

    [Fact]
    public void Obj_with_a_texture_writes_its_material_and_image_beside_it()
    {
        string path = Path.Combine(folder, "t.obj");
        IReadOnlyList<string> written = MeshExport.Save(Textured(), path);

        Assert.Equal(3, written.Count);
        string obj = File.ReadAllText(path);
        Assert.Contains("mtllib t.mtl", obj, StringComparison.Ordinal);
        Assert.Contains("f 1/1/1 2/2/2 3/3/3", obj, StringComparison.Ordinal);
        Assert.Contains("vt 0 0", obj, StringComparison.Ordinal);
        Assert.Contains("map_Kd t.png", File.ReadAllText(Path.Combine(folder, "t.mtl")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(folder, "t.png")));
    }

    [Fact]
    public void Obj_without_a_texture_carries_vertex_colours()
    {
        string path = Path.Combine(folder, "c.obj");
        MeshExport.Save(Coloured(), path);

        string first = File.ReadLines(path).First(l => l.StartsWith("v ", StringComparison.Ordinal));
        Assert.Equal(7, first.Split(' ').Length);
        Assert.EndsWith(" 1 0.5 0", first, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_extension_is_refused()
    {
        Assert.Throws<ArgumentException>(() => MeshExport.FormatOf("model.fbx"));
    }

    [Fact]
    public void Placed_stands_on_the_floor_at_the_size_asked()
    {
        Mesh placed = Coloured().Placed(100);

        (Vector3 min, Vector3 max) = placed.Bounds();
        Assert.Equal(0f, min.Y, 1e-3f);
        Assert.Equal(100f, MathF.Max(max.X - min.X, MathF.Max(max.Y - min.Y, max.Z - min.Z)), 1e-2f);
        Assert.Equal(0f, (min.X + max.X) / 2, 1e-3f);
    }
}
