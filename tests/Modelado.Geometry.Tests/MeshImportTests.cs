using System.Numerics;
using System.Text;
using Modelado.Geometry;
using Modelado.Imaging;

namespace Modelado.Geometry.Tests;

public sealed class MeshImportTests : IDisposable
{
    private readonly string folder = Directory.CreateTempSubdirectory("modelado-import-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private static Mesh Coloured()
    {
        Mesh sphere = MarchingCubes.Extract(MarchingCubesTests.Sphere(16, 0.5f), 0);
        return new Mesh(sphere.Positions, sphere.Triangles, [.. sphere.Positions.Select(p => new Vector3(1, 0.5f, (p.Z + 1) / 2))]);
    }

    private static Mesh Textured()
    {
        RgbaImage texture = new(8, 8);
        for (int i = 0; i < texture.Pixels.Length; i += 4)
        {
            texture.Pixels[i] = 200;
            texture.Pixels[i + 3] = 255;
        }

        float[] depth = new float[16 * 16];
        return Relief.Build(depth, 16, 16, texture, default, new ReliefOptions(GridLongSide: 16, BaseThickness: 0.05f));
    }

    // A file may order the vertices its own way (OBJ splits them per texture coordinate).
    private static int Nearest(Mesh mesh, Vector3 position)
    {
        int best = 0;
        for (int i = 1; i < mesh.VertexCount; i++)
        {
            if (Vector3.DistanceSquared(mesh.Positions[i], position) < Vector3.DistanceSquared(mesh.Positions[best], position))
            {
                best = i;
            }
        }

        return best;
    }

    private Mesh RoundTrip(Mesh mesh, string name)
    {
        string path = Path.Combine(folder, name);
        MeshExport.Save(mesh, path);
        return MeshImport.Load(path);
    }

    [Theory]
    [InlineData("m.glb")]
    [InlineData("m.obj")]
    [InlineData("m.ply")]
    [InlineData("m.stl")]
    public void Every_format_reads_back_what_was_written(string name)
    {
        Mesh mesh = Coloured();
        Mesh back = RoundTrip(mesh, name);

        Assert.Equal(mesh.TriangleCount, back.TriangleCount);
        Assert.Equal(mesh.VertexCount, back.VertexCount);
        Assert.Equal(mesh.SignedVolume(), back.SignedVolume(), 1e-4);
        Assert.False(back.DoubleSided);
        if (!name.EndsWith(".stl", StringComparison.Ordinal))
        {
            Assert.NotNull(back.Colors);
            int same = Nearest(back, mesh.Positions[7]);
            Assert.Equal(mesh.Colors![7].Z, back.Colors![same].Z, 0.01f);
        }
    }

    [Theory]
    [InlineData("t.glb")]
    [InlineData("t.obj")]
    public void A_texture_comes_back_with_its_coordinates(string name)
    {
        Mesh mesh = Textured();
        Mesh back = RoundTrip(mesh, name);

        Assert.NotNull(back.Texture);
        Assert.Equal(8, back.Texture!.Width);
        Assert.Equal(200, back.Texture.Pixels[0]);
        Assert.Equal(mesh.TriangleCount, back.TriangleCount);
        Assert.Equal(mesh.TexCoords![5].Y, back.TexCoords![Nearest(back, mesh.Positions[5])].Y, 1e-4f);
    }

    [Fact]
    public void Ascii_ply_reads_back_what_was_written()
    {
        // MOD13: the Export card's PLY option.
        Mesh mesh = Coloured();
        string path = Path.Combine(folder, "a.ply");
        MeshExport.Save(mesh, path, new ExportOptions(PlyAscii: true));
        Mesh back = MeshImport.Load(path);

        Assert.StartsWith("ply\nformat ascii 1.0\n", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Equal(mesh.VertexCount, back.VertexCount);
        Assert.Equal(mesh.TriangleCount, back.TriangleCount);
        Assert.Equal(mesh.Colors![7].X, back.Colors![Nearest(back, mesh.Positions[7])].X, 0.01f);
    }

    [Fact]
    public void A_textured_obj_can_carry_its_colours_per_vertex_instead()
    {
        // MOD13: the Export card's OBJ option: no .mtl or .png, the texture sampled at each vertex.
        Mesh mesh = Textured();
        string path = Path.Combine(folder, "v.obj");
        IReadOnlyList<string> written = MeshExport.Save(mesh, path, new ExportOptions(ObjVertexColours: true));
        Mesh back = MeshImport.Load(path);

        Assert.Equal([path], written);
        Assert.Null(back.Texture);
        Assert.NotNull(back.Colors);
        Assert.Equal(200 / 255f, back.Colors![0].X, 0.01f);
        Assert.Equal(mesh.TriangleCount, back.TriangleCount);
    }

    [Fact]
    public void Ascii_stl_and_quads_in_obj_and_ply_are_read()
    {
        string stl = Path.Combine(folder, "a.stl");
        File.WriteAllText(stl, "solid t\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nendloop\nendfacet\nendsolid t\n");
        string obj = Path.Combine(folder, "q.obj");
        File.WriteAllText(obj, "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nf 1 2 3 4\n");
        string ply = Path.Combine(folder, "q.ply");
        File.WriteAllText(ply, "ply\nformat ascii 1.0\nelement vertex 4\nproperty float x\nproperty float y\nproperty float z\nelement face 1\nproperty list uchar int vertex_indices\nend_header\n0 0 0\n1 0 0\n1 1 0\n0 1 0\n4 0 1 2 3\n");

        Assert.Equal(1, MeshImport.Load(stl).TriangleCount);
        Mesh quad = MeshImport.Load(obj);
        Assert.Equal(2, quad.TriangleCount);
        Assert.True(quad.DoubleSided);
        Assert.Equal(2, MeshImport.Load(ply).TriangleCount);
    }

    // A GLB of one triangle per material, each untextured with its own baseColorFactor (linear),
    // as Blender, SketchUp and CAD exporters write a plain-coloured part.
    private static byte[] FlatColouredGlb(params float[][] factors)
    {
        int n = factors.Length;
        byte[] bin = new byte[n * 36];
        for (int t = 0; t < n; t++)
        {
            float[] corners = [t, 0, 0, t + 1, 0, 0, t, 1, 0];
            Buffer.BlockCopy(corners, 0, bin, t * 36, 36);
        }

        string accessors = string.Join(",", Enumerable.Range(0, n).Select(t =>
            $$"""{"bufferView":0,"byteOffset":{{t * 36}},"componentType":5126,"count":3,"type":"VEC3","min":[{{t}},0,0],"max":[{{t + 1}},1,0]}"""));
        string meshes = string.Join(",", Enumerable.Range(0, n).Select(t => $$"""{"attributes":{"POSITION":{{t}}},"material":{{t}}}"""));
        string materials = string.Join(",", factors.Select(f =>
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $$$"""{"pbrMetallicRoughness":{"baseColorFactor":[{{{f[0]}}},{{{f[1]}}},{{{f[2]}}},1]}}""")));
        string json = $$"""
            {"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0}],
             "meshes":[{"primitives":[{{meshes}}]}],"materials":[{{materials}}],"accessors":[{{accessors}}],
             "bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":{{bin.Length}}}],"buffers":[{"byteLength":{{bin.Length}}}]}
            """;
        byte[] text = Encoding.UTF8.GetBytes(json.PadRight((json.Length + 3) / 4 * 4));
        using MemoryStream glb = new();
        using BinaryWriter w = new(glb);
        w.Write(0x46546C67u);
        w.Write(2u);
        w.Write((uint)(12 + 8 + text.Length + 8 + bin.Length));
        w.Write((uint)text.Length);
        w.Write(0x4E4F534Au);
        w.Write(text);
        w.Write((uint)bin.Length);
        w.Write(0x004E4942u);
        w.Write(bin);
        return glb.ToArray();
    }

    [Fact]
    public void A_glb_whose_materials_are_plain_colours_opens_in_them()
    {
        // MOD59: three parts, red, green and a linear 0.2 grey (sRGB about 0.48), none textured.
        string path = Path.Combine(folder, "parts.glb");
        File.WriteAllBytes(path, FlatColouredGlb([1, 0, 0], [0, 1, 0], [0.2f, 0.2f, 0.2f]));

        Mesh mesh = MeshImport.Load(path);

        Assert.NotNull(mesh.Colors);
        Assert.True(Vector3.Distance(new Vector3(1, 0, 0), mesh.Colors![Nearest(mesh, new Vector3(0, 0, 0))]) < 1e-5f, "the first part is red");
        Assert.True(Vector3.Distance(new Vector3(0, 1, 0), mesh.Colors![Nearest(mesh, new Vector3(1, 1, 0))]) < 1e-5f, "the second green");
        Assert.Equal(0.484f, mesh.Colors![Nearest(mesh, new Vector3(2, 1, 0))].X, 0.01f);
    }

    [Fact]
    public void A_glb_whose_materials_are_all_default_white_has_no_colours()
    {
        // A white baseColorFactor is the default, not a colour: the model stays uncoloured.
        string path = Path.Combine(folder, "white.glb");
        File.WriteAllBytes(path, FlatColouredGlb([1, 1, 1], [1, 1, 1]));

        Assert.Null(MeshImport.Load(path).Colors);
    }

    [Fact]
    public void An_obj_takes_each_materials_kd_and_a_ply_its_diffuse_colours()
    {
        // MOD59: two quads under two materials of the .mtl; the second names a texture that is not
        // there, so its Kd is what it shows.
        string obj = Path.Combine(folder, "kd.obj");
        File.WriteAllText(Path.Combine(folder, "kd.mtl"), "newmtl paint\nKd 0.8 0.1 0.1\n\nnewmtl decal\nKd 0.5 0.5 0.5\nmap_Kd missing.png\n");
        File.WriteAllText(obj, "mtllib kd.mtl\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nv 2 0 0\nv 2 1 0\nusemtl paint\nf 1 2 3 4\nusemtl decal\nf 2 5 6 3\n");
        string ply = Path.Combine(folder, "diffuse.ply");
        File.WriteAllText(ply, "ply\nformat ascii 1.0\nelement vertex 3\nproperty float x\nproperty float y\nproperty float z\nproperty uchar diffuse_red\nproperty uchar diffuse_green\nproperty uchar diffuse_blue\nelement face 1\nproperty list uchar int vertex_indices\nend_header\n0 0 0 255 128 0\n1 0 0 255 128 0\n0 1 0 255 128 0\n3 0 1 2\n");

        Mesh painted = MeshImport.Load(obj);
        Mesh orange = MeshImport.Load(ply);

        Assert.Equal(new Vector3(0.8f, 0.1f, 0.1f), painted.Colors![Nearest(painted, new Vector3(0, 1, 0))]);
        Assert.Equal(new Vector3(0.5f), painted.Colors![Nearest(painted, new Vector3(2, 1, 0))]);
        Assert.Equal(4 + 4, painted.VertexCount);
        Assert.Equal(128 / 255f, orange.Colors![0].Y, 0.001f);
    }

    private static RgbaImage Solid(byte r, byte g, byte b, int side = 8)
    {
        RgbaImage image = new(side, side);
        for (int i = 0; i < image.Pixels.Length; i += 4)
        {
            (image.Pixels[i], image.Pixels[i + 1], image.Pixels[i + 2], image.Pixels[i + 3]) = (r, g, b, 255);
        }

        return image;
    }

    // A GLB of one triangle per material, each with UVs; a material with an image is textured by it,
    // and every material carries its baseColorFactor (linear).
    private static byte[] MaterialsGlb(params (float[] Factor, RgbaImage? Image)[] parts)
    {
        int n = parts.Length;
        List<byte> bin = [];
        List<string> accessors = [], views = [], meshes = [], materials = [], textures = [], images = [];
        void View(byte[] bytes)
        {
            views.Add($$"""{"buffer":0,"byteOffset":{{bin.Count}},"byteLength":{{bytes.Length}}}""");
            bin.AddRange(bytes);
            while (bin.Count % 4 != 0)
            {
                bin.Add(0);
            }
        }

        for (int t = 0; t < n; t++)
        {
            float[] corners = [t, 0, 0, t + 1, 0, 0, t, 1, 0];
            float[] uv = [0.1f, 0.9f, 0.9f, 0.9f, 0.1f, 0.1f];
            byte[] p = new byte[36], q = new byte[24];
            Buffer.BlockCopy(corners, 0, p, 0, 36);
            Buffer.BlockCopy(uv, 0, q, 0, 24);
            View(p);
            accessors.Add($$"""{"bufferView":{{views.Count - 1}},"componentType":5126,"count":3,"type":"VEC3","min":[{{t}},0,0],"max":[{{t + 1}},1,0]}""");
            View(q);
            accessors.Add($$"""{"bufferView":{{views.Count - 1}},"componentType":5126,"count":3,"type":"VEC2"}""");
            meshes.Add($$$"""{"attributes":{"POSITION":{{{2 * t}}},"TEXCOORD_0":{{{(2 * t) + 1}}}},"material":{{{t}}}}""");
            string factor = string.Join(",", parts[t].Factor.Select(f => f.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (parts[t].Image is { } image)
            {
                View(ImageCodec.EncodePng(image));
                images.Add($$"""{"bufferView":{{views.Count - 1}},"mimeType":"image/png"}""");
                textures.Add($$"""{"source":{{images.Count - 1}}}""");
                materials.Add($$$$"""{"pbrMetallicRoughness":{"baseColorFactor":[{{{{factor}}}},1],"baseColorTexture":{"index":{{{{textures.Count - 1}}}}}}}""");
            }
            else
            {
                materials.Add($$$"""{"pbrMetallicRoughness":{"baseColorFactor":[{{{factor}}},1]}}""");
            }
        }

        string json = $$"""
            {"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0}],
             "meshes":[{"primitives":[{{string.Join(",", meshes)}}]}],"materials":[{{string.Join(",", materials)}}],
             "textures":[{{string.Join(",", textures)}}],"images":[{{string.Join(",", images)}}],"accessors":[{{string.Join(",", accessors)}}],
             "bufferViews":[{{string.Join(",", views)}}],"buffers":[{"byteLength":{{bin.Count}}}]}
            """;
        byte[] text = Encoding.UTF8.GetBytes(json.PadRight((json.Length + 3) / 4 * 4));
        using MemoryStream glb = new();
        using BinaryWriter w = new(glb);
        w.Write(0x46546C67u);
        w.Write(2u);
        w.Write((uint)(12 + 8 + text.Length + 8 + bin.Count));
        w.Write((uint)text.Length);
        w.Write(0x4E4F534Au);
        w.Write(text);
        w.Write((uint)bin.Count);
        w.Write(0x004E4942u);
        w.Write(bin.ToArray());
        return glb.ToArray();
    }

    // The colour the mesh shows at a vertex near a position: its texture where it has one.
    private static Vector3 Shown(Mesh mesh, Vector3 position)
    {
        int v = Nearest(mesh, position);
        if (mesh.Texture is null)
        {
            return mesh.Colors![v];
        }

        (float r, float g, float b) = mesh.Texture.Sample(mesh.TexCoords![v].X, mesh.TexCoords[v].Y);
        return new Vector3(r, g, b);
    }

    private static readonly float[] White = [1, 1, 1];

    [Fact]
    public void A_two_texture_glb_opens_with_each_part_in_its_own_texture()
    {
        // MOD59: a red and a blue texture, packed side by side into one atlas.
        string path = Path.Combine(folder, "two.glb");
        File.WriteAllBytes(path, MaterialsGlb((White, Solid(255, 0, 0)), (White, Solid(0, 0, 255))));

        Mesh mesh = MeshImport.Load(path, out string? notice);

        Assert.Null(notice);
        Assert.Equal((16, 8), (mesh.Texture!.Width, mesh.Texture.Height));
        Assert.True(Vector3.Distance(new Vector3(1, 0, 0), Shown(mesh, new Vector3(0, 1, 0))) < 0.02f, "the first part reads red");
        Assert.True(Vector3.Distance(new Vector3(0, 0, 1), Shown(mesh, new Vector3(1, 1, 0))) < 0.02f, "the second blue");
    }

    [Fact]
    public void A_plain_part_beside_a_textured_one_keeps_its_colour_in_the_atlas()
    {
        string path = Path.Combine(folder, "mixed.glb");
        File.WriteAllBytes(path, MaterialsGlb((White, Solid(255, 0, 0)), ([0, 1, 0], null)));

        Mesh mesh = MeshImport.Load(path);

        Assert.True(Vector3.Distance(new Vector3(1, 0, 0), Shown(mesh, new Vector3(0, 1, 0))) < 0.02f, "the textured part reads red");
        Assert.True(Vector3.Distance(new Vector3(0, 1, 0), Shown(mesh, new Vector3(1, 1, 0))) < 0.02f, "the plain part green, from its palette cell");
    }

    [Fact]
    public void A_factor_tints_its_texture()
    {
        // Linear 0.5 is sRGB about 0.735: a white texture under it reads that grey.
        string path = Path.Combine(folder, "tinted.glb");
        File.WriteAllBytes(path, MaterialsGlb(([0.5f, 0.5f, 0.5f], Solid(255, 255, 255))));

        Assert.Equal(0.735f, Shown(MeshImport.Load(path), new Vector3(0, 1, 0)).X, 0.01f);
    }

    [Fact]
    public void Five_textures_are_baked_into_colours_and_said_so()
    {
        string path = Path.Combine(folder, "five.glb");
        File.WriteAllBytes(path, MaterialsGlb([.. Enumerable.Range(0, 5).Select(i => (White, (RgbaImage?)Solid((byte)(i * 60), 0, 0)))]));

        Mesh mesh = MeshImport.Load(path, out string? notice);

        Assert.Null(mesh.Texture);
        Assert.Equal(240 / 255f, mesh.Colors![Nearest(mesh, new Vector3(4, 1, 0))].X, 0.02f);
        Assert.Equal("five.glb has 5 textures, more than the 4 one model can keep, so its colours were baked into its vertices.", notice);
    }

    [Fact]
    public void An_obj_with_two_textured_materials_opens_with_each_in_its_own()
    {
        string obj = Path.Combine(folder, "pair.obj");
        ImageCodec.SavePng(Solid(255, 0, 0), Path.Combine(folder, "red.png"));
        ImageCodec.SavePng(Solid(0, 0, 255), Path.Combine(folder, "blue.png"));
        File.WriteAllText(Path.Combine(folder, "pair.mtl"), "newmtl red\nKd 1 1 1\nmap_Kd red.png\nnewmtl blue\nKd 1 1 1\nmap_Kd blue.png\n");
        File.WriteAllText(obj, "mtllib pair.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nv 2 0 0\nv 1 1 0\nvt 0.1 0.1\nvt 0.9 0.1\nvt 0.1 0.9\nusemtl red\nf 1/1 2/2 3/3\nusemtl blue\nf 2/1 4/2 5/3\n");

        Mesh mesh = MeshImport.Load(obj);

        Assert.True(Vector3.Distance(new Vector3(1, 0, 0), Shown(mesh, new Vector3(0, 1, 0))) < 0.02f, "the red material's face");
        Assert.True(Vector3.Distance(new Vector3(0, 0, 1), Shown(mesh, new Vector3(1, 1, 0))) < 0.02f, "and the blue one's");
    }

    [Fact]
    public void A_broken_file_says_so()
    {
        string bad = Path.Combine(folder, "bad.glb");
        File.WriteAllBytes(bad, Encoding.ASCII.GetBytes("not a model at all"));

        Assert.Throws<InvalidDataException>(() => MeshImport.Load(bad));
    }
}
