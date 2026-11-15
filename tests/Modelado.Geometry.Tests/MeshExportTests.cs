using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

public sealed class MeshExportTests : IDisposable
{
    // A 3MF triangle's three corner attributes (MOD103).
    private static readonly string[] Corners = ["v1", "v2", "v3"];

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

    [Theory]
    [InlineData(1_000, 5123)]
    [InlineData(70_000, 5125)]
    public void Indices_are_16_bit_while_every_vertex_fits_and_read_back_the_same(int vertices, int componentType)
    {
        // MOD66: a fan over the vertices, its last triangle reaching the highest index.
        Vector3[] positions = [.. Enumerable.Range(0, vertices).Select(i => new Vector3(MathF.Cos(i), MathF.Sin(i), i * 1e-4f))];
        int[] triangles = [.. Enumerable.Range(1, vertices - 2).SelectMany(i => new[] { 0, i, i + 1 })];
        Mesh mesh = new(positions, triangles);
        string path = Path.Combine(folder, $"fan{vertices}.glb");
        MeshExport.Save(mesh, path);
        (JsonDocument json, _) = ReadGlb(path);

        JsonElement primitive = json.RootElement.GetProperty("meshes")[0].GetProperty("primitives")[0];
        Assert.Equal(componentType, json.RootElement.GetProperty("accessors")[primitive.GetProperty("indices").GetInt32()].GetProperty("componentType").GetInt32());
        Assert.Equal(triangles, MeshImport.Load(path).Triangles);
    }

    [Fact]
    public void A_compact_glb_quantizes_the_canyon_and_reads_back_within_a_step()
    {
        // MOD66: KHR_mesh_quantization, declared required, the node returning the bounds.
        Mesh relief = MeshImport.Load(Path.Combine(Repository.Root, "site", "public", "demo", "studio", "relief-landscape.glb"));
        string full = Path.Combine(folder, "full.glb"), compact = Path.Combine(folder, "compact.glb");
        MeshExport.Save(relief, full);
        MeshExport.Save(relief, compact, new ExportOptions(GlbCompact: true));
        long fullSize = new FileInfo(full).Length, compactSize = new FileInfo(compact).Length;
        TestContext.Current.TestOutputHelper?.WriteLine($"canyon: {fullSize:N0} bytes full, {compactSize:N0} compact");

        (JsonDocument json, _) = ReadGlb(compact);
        Assert.Equal("KHR_mesh_quantization", json.RootElement.GetProperty("extensionsRequired")[0].GetString());
        // Measured on the guest: 2,629,484 bytes standard, 1,744,996 compact.
        Assert.True(compactSize < fullSize * 0.7, $"compact {compactSize:N0} against {fullSize:N0}");

        Mesh back = MeshImport.Load(compact);
        Assert.Equal(relief.Triangles, back.Triangles);
        (Vector3 low, Vector3 high) = relief.Bounds();
        float step = (high - low).Length() / 32767f;
        float worst = relief.Positions.Zip(back.Positions, (a, b) => (a - b).Length()).Max();
        Assert.True(worst < step * 2, $"a vertex moved {worst} against a step of {step}");
        Assert.True(relief.TexCoords!.Zip(back.TexCoords!, (a, b) => (a - b).Length()).Max() < 2f / 65535);
    }

    [Fact]
    public void A_compact_glb_keeps_vertex_colours_within_an_eight_bit_step()
    {
        Mesh coloured = Coloured();
        string path = Path.Combine(folder, "coloured.glb");
        MeshExport.Save(coloured, path, new ExportOptions(GlbCompact: true));

        Mesh back = MeshImport.Load(path);
        Assert.Equal(coloured.Triangles, back.Triangles);
        Assert.True(coloured.Colors!.Zip(back.Colors!, (a, b) => (a - b).Length()).Max() < 0.02f);
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
        // The site's landscape relief and its 768 px photo, which before MOD63 went in as a 1.0 MB PNG
        // of a 4.1 MB file. Its texture is weighed as the GLB carries it against that PNG.
        Mesh relief = MeshImport.Load(Path.Combine(Repository.Root, "site", "public", "demo", "studio", "relief-landscape.glb"));
        Assert.False(relief.Texture!.HasTransparency);
        string path = Path.Combine(folder, "canyon.glb");
        MeshExport.Save(relief, path);

        (JsonDocument json, _) = ReadGlb(path);
        int carried = json.RootElement.GetProperty("bufferViews")[json.RootElement.GetProperty("images")[0].GetProperty("bufferView").GetInt32()].GetProperty("byteLength").GetInt32();
        int png = ImageCodec.EncodePng(relief.Texture).Length;
        TestContext.Current.TestOutputHelper?.WriteLine($"canyon: {new FileInfo(path).Length:N0} bytes; its texture {carried:N0} as carried, {png:N0} as PNG");
        Assert.True(carried < 250_000 && carried < png / 4, $"the texture is {carried:N0} bytes as carried, {png:N0} as PNG");

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
    public void A_3mf_is_a_core_spec_package_in_millimetres_with_its_colours()
    {
        // MOD103: the package parts the core spec names, the root relationship to the model part, the
        // unit, every vertex and triangle in range, and one colour per vertex in the materials group.
        Mesh mesh = Coloured().Placed(80);
        string path = Path.Combine(folder, "m.3mf");
        Assert.Equal([path], MeshExport.Save(mesh, path));

        using System.IO.Compression.ZipArchive package = System.IO.Compression.ZipFile.OpenRead(path);
        string Part(string name)
        {
            using StreamReader reader = new(package.GetEntry(name)?.Open() ?? throw new Xunit.Sdk.XunitException($"{name} is missing"));
            return reader.ReadToEnd();
        }

        System.Xml.Linq.XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
        System.Xml.Linq.XDocument contentTypes = System.Xml.Linq.XDocument.Parse(Part("[Content_Types].xml"));
        Assert.Contains(contentTypes.Root!.Elements(types + "Default"), d => (string?)d.Attribute("Extension") == "model" && (string?)d.Attribute("ContentType") == "application/vnd.ms-package.3dmanufacturing-3dmodel+xml");
        Assert.Contains(contentTypes.Root!.Elements(types + "Default"), d => (string?)d.Attribute("Extension") == "rels");

        System.Xml.Linq.XNamespace rels = "http://schemas.openxmlformats.org/package/2006/relationships";
        System.Xml.Linq.XElement root = System.Xml.Linq.XDocument.Parse(Part("_rels/.rels")).Root!.Element(rels + "Relationship")!;
        Assert.Equal("http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel", (string?)root.Attribute("Type"));
        Assert.Equal("/" + MeshExport.ThreeMfModelPart, (string?)root.Attribute("Target"));

        System.Xml.Linq.XNamespace core = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";
        System.Xml.Linq.XNamespace material = "http://schemas.microsoft.com/3dmanufacturing/material/2015/02";
        System.Xml.Linq.XElement model = System.Xml.Linq.XDocument.Parse(Part(MeshExport.ThreeMfModelPart)).Root!;
        Assert.Equal(core + "model", model.Name);
        Assert.Equal("millimeter", (string?)model.Attribute("unit"));
        Assert.Null(model.Attribute("requiredextensions"));

        System.Xml.Linq.XElement[] vertices = [.. model.Descendants(core + "vertex")];
        System.Xml.Linq.XElement[] triangles = [.. model.Descendants(core + "triangle")];
        string[] colours = [.. model.Descendants(material + "color").Select(e => (string)e.Attribute("color")!)];
        Assert.Equal(mesh.VertexCount, vertices.Length);
        Assert.Equal(mesh.TriangleCount, triangles.Length);
        Assert.Equal(mesh.VertexCount, colours.Length);
        Assert.All(colours, colour => Assert.Equal("#FF8000", colour));

        float[] xs = [.. vertices.Select(v => float.Parse((string)v.Attribute("x")!, System.Globalization.CultureInfo.InvariantCulture))];
        Assert.Equal(80f, xs.Max() - xs.Min(), 0.01f);
        Assert.All(triangles, triangle =>
        {
            int[] corners = [.. Corners.Select(a => (int)triangle.Attribute(a)!)];
            Assert.All(corners, v => Assert.InRange(v, 0, mesh.VertexCount - 1));
            Assert.Equal(3, corners.Distinct().Count());
            Assert.Equal("1", (string?)triangle.Attribute("pid"));
        });

        System.Xml.Linq.XElement item = model.Element(core + "build")!.Element(core + "item")!;
        Assert.Equal((string?)model.Descendants(core + "object").Single().Attribute("id"), (string?)item.Attribute("objectid"));
    }

    [Fact]
    public void A_3mf_of_an_uncoloured_mesh_has_no_colour_group_and_no_degenerate_triangle()
    {
        Mesh grey = new([new(0, 0, 0), new(10, 0, 0), new(0, 10, 0), new(0, 0, 10)], [0, 1, 2, 0, 1, 3, 1, 1, 2]);
        string path = Path.Combine(folder, "g.3mf");
        MeshExport.Save(grey, path);

        using System.IO.Compression.ZipArchive package = System.IO.Compression.ZipFile.OpenRead(path);
        using StreamReader reader = new(package.GetEntry(MeshExport.ThreeMfModelPart)!.Open());
        string model = reader.ReadToEnd();
        Assert.DoesNotContain("colorgroup", model, StringComparison.Ordinal);
        Assert.Equal(2, model.Split("<triangle ").Length - 1);
        Assert.Throws<InvalidDataException>(() => MeshImport.Load(path));
    }

    [Fact]
    public void An_unknown_extension_is_refused()
    {
        Assert.Throws<ArgumentException>(() => MeshExport.FormatOf("model.fbx"));
    }

    [Fact]
    public void On_the_plate_a_relief_lies_face_up_with_its_back_on_z_0_and_keeps_its_millimetres()
    {
        // MOD109: a slicer is Z-up; a relief's face is +Z and its back flat, so moving it is enough.
        float[] depth = [.. Enumerable.Range(0, 32 * 24).Select(i => (i % 32) / 32f)];
        Mesh relief = Relief.Build(depth, 32, 24, new RgbaImage(32, 24), ReadOnlySpan<float>.Empty,
            new ReliefOptions(GridLongSide: 32, Depth: 3, BaseThickness: 2, SmoothPasses: 0, PrintWidth: 60));

        Mesh plate = relief.OnPlate();
        (Vector3 min, Vector3 max) = plate.Bounds();
        Assert.Equal(0f, min.Z, 1e-4f);
        Assert.Equal(5f, max.Z, 1e-3f);
        Assert.Equal(60f, max.X - min.X, 1e-3f);
        Assert.Equal(0f, (min.X + max.X) / 2, 1e-3f);
        Assert.Equal(0f, (min.Y + max.Y) / 2, 1e-3f);

        // The back is the flat face on the plate: a third of the vertices or more sit at z = 0.
        Assert.True(plate.Positions.Count(p => MathF.Abs(p.Z) < 1e-4f) >= plate.VertexCount / 3);

        // Written as STL and read back, it is where it was laid.
        string path = Path.Combine(folder, "plate.stl");
        MeshExport.Save(plate, path);
        Assert.Equal(0f, MeshImport.Load(path).Bounds().Min.Z, 1e-4f);

        // A size rescales it as Placed does.
        (Vector3 small, Vector3 large) = relief.OnPlate(30).Bounds();
        Assert.Equal(30f, large.X - small.X, 1e-3f);
        Assert.Equal(0f, small.Z, 1e-4f);
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
