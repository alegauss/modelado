using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Modelado.Imaging;

namespace Modelado.Geometry;

/// <summary>The file formats a mesh is saved as.</summary>
public enum MeshFormat
{
    /// <summary>Binary glTF 2.0: one file with the texture inside; Windows' 3D Viewer, Blender, the web.</summary>
    Glb,

    /// <summary>Wavefront OBJ, with an .mtl and a .png beside it when textured.</summary>
    Obj,

    /// <summary>Binary PLY with vertex colours: MeshLab, CloudCompare.</summary>
    Ply,

    /// <summary>Binary STL, geometry only: slicers for 3D printing.</summary>
    Stl,
}

/// <summary>The choices a format has beyond the format itself (MOD13, the Export card's options).</summary>
/// <param name="ObjVertexColours">OBJ: the colours per vertex (sampled from the texture) rather than an .mtl and a .png.</param>
/// <param name="PlyAscii">PLY: text rather than binary, for tools and eyes that read it.</param>
public sealed record ExportOptions(bool ObjVertexColours = false, bool PlyAscii = false)
{
    /// <summary>What a format is written as when nothing is chosen.</summary>
    public static ExportOptions Default { get; } = new();
}

/// <summary>Writes meshes to GLB, OBJ, PLY and STL.</summary>
public static class MeshExport
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The format a file name's extension names.</summary>
    /// <param name="path">The file name.</param>
    /// <returns>The format.</returns>
    /// <exception cref="ArgumentException">The extension is not .glb, .obj, .ply or .stl.</exception>
    public static MeshFormat FormatOf(string path) => Path.GetExtension(path).ToUpperInvariant() switch
    {
        ".GLB" => MeshFormat.Glb,
        ".OBJ" => MeshFormat.Obj,
        ".PLY" => MeshFormat.Ply,
        ".STL" => MeshFormat.Stl,
        _ => throw new ArgumentException($"{Path.GetFileName(path)}: save as .glb, .obj, .ply or .stl.", nameof(path)),
    };

    /// <summary>Saves a mesh in the format its extension names.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="path">The file; an OBJ's .mtl and .png go beside it.</param>
    /// <param name="options">The format's choices; null for <see cref="ExportOptions.Default"/>.</param>
    /// <returns>Every file written.</returns>
    public static IReadOnlyList<string> Save(Mesh mesh, string path, ExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        options ??= ExportOptions.Default;
        MeshFormat format = FormatOf(path);
        if (format == MeshFormat.Obj)
        {
            return WriteObj(mesh, path, options.ObjVertexColours);
        }

        using (FileStream stream = File.Create(path))
        {
            switch (format)
            {
                case MeshFormat.Glb:
                    WriteGlb(mesh, stream);
                    break;
                case MeshFormat.Ply:
                    WritePly(mesh, stream, options.PlyAscii);
                    break;
                default:
                    WriteStl(mesh, stream);
                    break;
            }
        }

        return [path];
    }

    /// <summary>
    /// How a GLB carries the mesh's texture (MOD63): JPEG, a tenth of the PNG's weight for a photo,
    /// unless the alpha is used, where PNG keeps a cut-out relief's transparent background.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>"JPEG" or "PNG"; null for a mesh with no texture.</returns>
    public static string? GlbTextureFormat(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Texture is null)
        {
            return null;
        }

        return mesh.Texture.HasTransparency ? "PNG" : "JPEG";
    }

    /// <summary>Writes binary glTF 2.0.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="stream">Where.</param>
    public static void WriteGlb(Mesh mesh, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(stream);
        using MemoryStream bin = new();
        JsonArray views = [], accessors = [];
        JsonObject attributes = [];

        (Vector3 min, Vector3 max) = mesh.Bounds();
        attributes["POSITION"] = Accessor(Floats(mesh.Positions), 5126, mesh.VertexCount, "VEC3", 34962, new JsonArray(min.X, min.Y, min.Z), new JsonArray(max.X, max.Y, max.Z));
        attributes["NORMAL"] = Accessor(Floats(mesh.ComputeNormals()), 5126, mesh.VertexCount, "VEC3", 34962);
        if (mesh.TexCoords is not null)
        {
            attributes["TEXCOORD_0"] = Accessor(Floats(mesh.TexCoords), 5126, mesh.VertexCount, "VEC2", 34962);
        }
        else if (mesh.Colors is not null)
        {
            // glTF's vertex colours are linear, and multiply the texture: only an untextured mesh carries them.
            Vector3[] linear = [.. mesh.Colors.Select(c => new Vector3(ToLinear(c.X), ToLinear(c.Y), ToLinear(c.Z)))];
            attributes["COLOR_0"] = Accessor(Floats(linear), 5126, mesh.VertexCount, "VEC3", 34962);
        }

        byte[] indices = new byte[mesh.Triangles.Length * 4];
        for (int i = 0; i < mesh.Triangles.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(indices.AsSpan(i * 4), mesh.Triangles[i]);
        }

        int indexAccessor = Accessor(indices, 5125, mesh.Triangles.Length, "SCALAR", 34963);

        JsonObject pbr = new() { ["metallicFactor"] = 0.0, ["roughnessFactor"] = 0.9 };
        JsonObject root = new()
        {
            ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = "Modelado" },
            ["scene"] = 0,
            ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(0) }),
            ["nodes"] = new JsonArray(new JsonObject { ["mesh"] = 0, ["name"] = "Modelado" }),
            ["meshes"] = new JsonArray(new JsonObject
            {
                ["primitives"] = new JsonArray(new JsonObject { ["attributes"] = attributes, ["indices"] = indexAccessor, ["material"] = 0 }),
            }),
            ["materials"] = new JsonArray(new JsonObject { ["pbrMetallicRoughness"] = pbr, ["doubleSided"] = mesh.DoubleSided }),
        };

        if (mesh.Texture is not null)
        {
            bool jpeg = GlbTextureFormat(mesh) == "JPEG";
            byte[] image = jpeg ? ImageCodec.EncodeJpeg(mesh.Texture) : ImageCodec.EncodePng(mesh.Texture);
            int view = View(image, null);
            root["images"] = new JsonArray(new JsonObject { ["bufferView"] = view, ["mimeType"] = jpeg ? "image/jpeg" : "image/png" });
            root["samplers"] = new JsonArray(new JsonObject { ["magFilter"] = 9729, ["minFilter"] = 9987, ["wrapS"] = 33071, ["wrapT"] = 33071 });
            root["textures"] = new JsonArray(new JsonObject { ["source"] = 0, ["sampler"] = 0 });
            pbr["baseColorTexture"] = new JsonObject { ["index"] = 0 };
        }

        root["bufferViews"] = views;
        root["accessors"] = accessors;
        root["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = bin.Length });

        byte[] json = Utf8.GetBytes(root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        int jsonLength = Pad4(json.Length), binLength = Pad4((int)bin.Length);
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 0x46546C67); // "glTF"
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(header[8..], (uint)(12 + 8 + jsonLength + 8 + binLength));
        stream.Write(header);
        WriteChunk(stream, json, jsonLength, 0x4E4F534A, (byte)' '); // "JSON"
        WriteChunk(stream, bin.ToArray(), binLength, 0x004E4942, 0); // "BIN"

        int View(byte[] data, int? target)
        {
            while (bin.Length % 4 != 0)
            {
                bin.WriteByte(0);
            }

            JsonObject view = new() { ["buffer"] = 0, ["byteOffset"] = bin.Length, ["byteLength"] = data.Length };
            if (target is int t)
            {
                view["target"] = t;
            }

            bin.Write(data);
            views.Add(view);
            return views.Count - 1;
        }

        int Accessor(byte[] data, int componentType, int count, string type, int target, JsonArray? lower = null, JsonArray? upper = null)
        {
            JsonObject accessor = new() { ["bufferView"] = View(data, target), ["componentType"] = componentType, ["count"] = count, ["type"] = type };
            if (lower is not null && upper is not null)
            {
                accessor["min"] = lower;
                accessor["max"] = upper;
            }

            accessors.Add(accessor);
            return accessors.Count - 1;
        }
    }

    /// <summary>Writes PLY with per-vertex colour (sampled from the texture when there are none).</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="stream">Where.</param>
    /// <param name="ascii">Text rather than binary little-endian.</param>
    public static void WritePly(Mesh mesh, Stream stream, bool ascii = false)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(stream);
        Vector3[]? colors = VertexColors(mesh);
        StringBuilder header = new();
        header.Append(ascii ? "ply\nformat ascii 1.0\ncomment Modelado\n" : "ply\nformat binary_little_endian 1.0\ncomment Modelado\n");
        header.Append(CultureInfo.InvariantCulture, $"element vertex {mesh.VertexCount}\n");
        header.Append("property float x\nproperty float y\nproperty float z\n");
        if (colors is not null)
        {
            header.Append("property uchar red\nproperty uchar green\nproperty uchar blue\n");
        }

        header.Append(CultureInfo.InvariantCulture, $"element face {mesh.TriangleCount}\n");
        header.Append("property list uchar int vertex_indices\nend_header\n");
        stream.Write(Encoding.ASCII.GetBytes(header.ToString()));
        if (ascii)
        {
            WritePlyText(mesh, colors, stream);
            return;
        }

        using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            Vector3 p = mesh.Positions[i];
            writer.Write(p.X);
            writer.Write(p.Y);
            writer.Write(p.Z);
            if (colors is not null)
            {
                writer.Write(ToByte(colors[i].X));
                writer.Write(ToByte(colors[i].Y));
                writer.Write(ToByte(colors[i].Z));
            }
        }

        for (int t = 0; t < mesh.Triangles.Length; t += 3)
        {
            writer.Write((byte)3);
            writer.Write(mesh.Triangles[t]);
            writer.Write(mesh.Triangles[t + 1]);
            writer.Write(mesh.Triangles[t + 2]);
        }
    }

    // ASCII PLY's body: one vertex per line, then one face per line, as the header declared them.
    private static void WritePlyText(Mesh mesh, Vector3[]? colors, Stream stream)
    {
        using StreamWriter text = new(stream, Encoding.ASCII, leaveOpen: true) { NewLine = "\n" };
        CultureInfo c = CultureInfo.InvariantCulture;
        for (int i = 0; i < mesh.VertexCount; i++)
        {
            Vector3 p = mesh.Positions[i];
            text.WriteLine(colors is null
                ? string.Format(c, "{0:R} {1:R} {2:R}", p.X, p.Y, p.Z)
                : string.Format(c, "{0:R} {1:R} {2:R} {3} {4} {5}", p.X, p.Y, p.Z, ToByte(colors[i].X), ToByte(colors[i].Y), ToByte(colors[i].Z)));
        }

        for (int t = 0; t < mesh.Triangles.Length; t += 3)
        {
            text.WriteLine(string.Format(c, "3 {0} {1} {2}", mesh.Triangles[t], mesh.Triangles[t + 1], mesh.Triangles[t + 2]));
        }
    }

    /// <summary>Writes binary STL: geometry only, one facet normal per triangle.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="stream">Where.</param>
    public static void WriteStl(Mesh mesh, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(stream);
        using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: true);
        byte[] header = new byte[80];
        Encoding.ASCII.GetBytes("Modelado binary STL").CopyTo(header, 0);
        writer.Write(header);
        writer.Write((uint)mesh.TriangleCount);
        for (int t = 0; t < mesh.Triangles.Length; t += 3)
        {
            Vector3 a = mesh.Positions[mesh.Triangles[t]], b = mesh.Positions[mesh.Triangles[t + 1]], c = mesh.Positions[mesh.Triangles[t + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            n = n.LengthSquared() > 0 ? Vector3.Normalize(n) : Vector3.Zero;
            foreach (Vector3 v in (ReadOnlySpan<Vector3>)[n, a, b, c])
            {
                writer.Write(v.X);
                writer.Write(v.Y);
                writer.Write(v.Z);
            }

            writer.Write((ushort)0);
        }
    }

    /// <summary>
    /// Writes Wavefront OBJ: with a texture, an .mtl and a .png named after the file beside it;
    /// otherwise vertex colours as the widely read "v x y z r g b" extension.
    /// </summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="path">The .obj file.</param>
    /// <param name="vertexColours">A textured mesh's texture sampled at each vertex, instead of the .mtl and .png.</param>
    /// <returns>Every file written.</returns>
    public static IReadOnlyList<string> WriteObj(Mesh mesh, string path, bool vertexColours = false)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        List<string> written = [path];
        string folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string stem = Path.GetFileNameWithoutExtension(path);
        bool textured = mesh.Texture is not null && !vertexColours;
        Vector3[]? colours = textured ? null : VertexColors(mesh);
        Vector3[] normals = mesh.ComputeNormals();
        CultureInfo c = CultureInfo.InvariantCulture;

        using (StreamWriter obj = new(path, false, Utf8))
        {
            obj.NewLine = "\n";
            obj.WriteLine("# Modelado");
            if (textured)
            {
                obj.WriteLine($"mtllib {stem}.mtl");
            }

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                Vector3 p = mesh.Positions[i];
                if (colours is not null)
                {
                    Vector3 col = colours[i];
                    obj.WriteLine(string.Format(c, "v {0:0.######} {1:0.######} {2:0.######} {3:0.####} {4:0.####} {5:0.####}", p.X, p.Y, p.Z, col.X, col.Y, col.Z));
                }
                else
                {
                    obj.WriteLine(string.Format(c, "v {0:0.######} {1:0.######} {2:0.######}", p.X, p.Y, p.Z));
                }
            }

            foreach (Vector3 n in normals)
            {
                obj.WriteLine(string.Format(c, "vn {0:0.####} {1:0.####} {2:0.####}", n.X, n.Y, n.Z));
            }

            if (textured)
            {
                // OBJ's v runs up from the image's bottom.
                foreach (Vector2 uv in mesh.TexCoords!)
                {
                    obj.WriteLine(string.Format(c, "vt {0:0.######} {1:0.######}", uv.X, 1 - uv.Y));
                }

                obj.WriteLine("usemtl modelado");
            }

            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                int a = mesh.Triangles[t] + 1, b = mesh.Triangles[t + 1] + 1, d = mesh.Triangles[t + 2] + 1;
                obj.WriteLine(textured
                    ? string.Format(c, "f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}", a, b, d)
                    : string.Format(c, "f {0}//{0} {1}//{1} {2}//{2}", a, b, d));
            }
        }

        if (textured)
        {
            string mtl = Path.Combine(folder, stem + ".mtl");
            string png = Path.Combine(folder, stem + ".png");
            File.WriteAllText(mtl, $"newmtl modelado\nKa 1 1 1\nKd 1 1 1\nKs 0 0 0\nd 1\nillum 1\nmap_Kd {stem}.png\n", Utf8);
            ImageCodec.SavePng(mesh.Texture!, png);
            written.Add(mtl);
            written.Add(png);
        }

        return written;
    }

    /// <summary>The mesh's colour per vertex: its own, or its texture sampled at each vertex.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <returns>The colours, or null for a mesh with neither.</returns>
    public static Vector3[]? VertexColors(Mesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Colors is not null)
        {
            return mesh.Colors;
        }

        if (mesh.Texture is null || mesh.TexCoords is null)
        {
            return null;
        }

        Vector3[] colors = new Vector3[mesh.VertexCount];
        for (int i = 0; i < colors.Length; i++)
        {
            (float r, float g, float b) = mesh.Texture.Sample(mesh.TexCoords[i].X, mesh.TexCoords[i].Y);
            colors[i] = new Vector3(r, g, b);
        }

        return colors;
    }

    private static float ToLinear(float s) => s <= 0.04045f ? s / 12.92f : MathF.Pow((s + 0.055f) / 1.055f, 2.4f);

    private static byte ToByte(float v) => (byte)Math.Clamp(MathF.Round(v * 255f), 0, 255);

    private static int Pad4(int length) => (length + 3) & ~3;

    private static byte[] Floats(Vector3[] values)
    {
        byte[] data = new byte[values.Length * 12];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i * 12), values[i].X);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan((i * 12) + 4), values[i].Y);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan((i * 12) + 8), values[i].Z);
        }

        return data;
    }

    private static byte[] Floats(Vector2[] values)
    {
        byte[] data = new byte[values.Length * 8];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(i * 8), values[i].X);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan((i * 8) + 4), values[i].Y);
        }

        return data;
    }

    private static void WriteChunk(Stream stream, byte[] data, int paddedLength, uint type, byte pad)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)paddedLength);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], type);
        stream.Write(header);
        stream.Write(data);
        for (int i = data.Length; i < paddedLength; i++)
        {
            stream.WriteByte(pad);
        }
    }
}
