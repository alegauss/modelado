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
        // MOD59: two quads under two materials of the .mtl, one textured (its Kd is the texture's tint, left alone).
        string obj = Path.Combine(folder, "kd.obj");
        File.WriteAllText(Path.Combine(folder, "kd.mtl"), "newmtl paint\nKd 0.8 0.1 0.1\n\nnewmtl decal\nKd 0.5 0.5 0.5\nmap_Kd missing.png\n");
        File.WriteAllText(obj, "mtllib kd.mtl\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nv 2 0 0\nv 2 1 0\nusemtl paint\nf 1 2 3 4\nusemtl decal\nf 2 5 6 3\n");
        string ply = Path.Combine(folder, "diffuse.ply");
        File.WriteAllText(ply, "ply\nformat ascii 1.0\nelement vertex 3\nproperty float x\nproperty float y\nproperty float z\nproperty uchar diffuse_red\nproperty uchar diffuse_green\nproperty uchar diffuse_blue\nelement face 1\nproperty list uchar int vertex_indices\nend_header\n0 0 0 255 128 0\n1 0 0 255 128 0\n0 1 0 255 128 0\n3 0 1 2\n");

        Mesh painted = MeshImport.Load(obj);
        Mesh orange = MeshImport.Load(ply);

        Assert.Equal(new Vector3(0.8f, 0.1f, 0.1f), painted.Colors![Nearest(painted, new Vector3(0, 1, 0))]);
        Assert.Equal(Vector3.One, painted.Colors![Nearest(painted, new Vector3(2, 1, 0))]);
        Assert.Equal(4 + 4, painted.VertexCount);
        Assert.Equal(128 / 255f, orange.Colors![0].Y, 0.001f);
    }

    [Fact]
    public void A_broken_file_says_so()
    {
        string bad = Path.Combine(folder, "bad.glb");
        File.WriteAllBytes(bad, Encoding.ASCII.GetBytes("not a model at all"));

        Assert.Throws<InvalidDataException>(() => MeshImport.Load(bad));
    }
}
