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

    [Fact]
    public void A_broken_file_says_so()
    {
        string bad = Path.Combine(folder, "bad.glb");
        File.WriteAllBytes(bad, Encoding.ASCII.GetBytes("not a model at all"));

        Assert.Throws<InvalidDataException>(() => MeshImport.Load(bad));
    }
}
