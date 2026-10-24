using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Modelado.Imaging;

namespace Modelado.Geometry;

/// <summary>
/// Reads a 3D model to edit or improve: GLB (binary glTF 2.0, every mesh in the default scene
/// with its node transforms, vertex colours and base-colour texture), OBJ (with its .mtl's
/// diffuse texture, or vertex colours), PLY (ASCII or binary, with colours) and STL (ASCII or
/// binary). Polygons are split into triangles; a surface with border edges is marked double-sided.
/// </summary>
public static class MeshImport
{
    /// <summary>The extensions the open dialog offers.</summary>
    public static readonly IReadOnlyList<string> Extensions = [".glb", ".obj", ".ply", ".stl"];

    /// <summary>Reads a model by its extension.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The mesh.</returns>
    /// <exception cref="InvalidDataException">The file is not a model this build can read.</exception>
    public static Mesh Load(string path)
    {
        try
        {
            Mesh mesh = MeshExport.FormatOf(path) switch
            {
                MeshFormat.Glb => ReadGlb(File.ReadAllBytes(path)),
                MeshFormat.Obj => ReadObj(path),
                MeshFormat.Ply => ReadPly(File.ReadAllBytes(path)),
                _ => ReadStl(File.ReadAllBytes(path)),
            };
            if (mesh.TriangleCount == 0)
            {
                throw new InvalidDataException($"{Path.GetFileName(path)} has no triangles.");
            }

            return mesh;
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException or FormatException or KeyNotFoundException or JsonException or OverflowException or InvalidOperationException)
        {
            throw new InvalidDataException($"{Path.GetFileName(path)} could not be read: {error.Message}", error);
        }
    }

    /// <summary>Whether every edge is shared by exactly two triangles.</summary>
    /// <param name="triangles">Three indices per triangle.</param>
    /// <returns>True for a closed surface.</returns>
    public static bool IsClosed(int[] triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        Dictionary<(int, int), int> uses = [];
        for (int t = 0; t < triangles.Length; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                int a = triangles[t + k], b = triangles[t + ((k + 1) % 3)];
                (int, int) key = a < b ? (a, b) : (b, a);
                uses[key] = uses.GetValueOrDefault(key) + 1;
            }
        }

        return uses.Values.All(n => n == 2);
    }

    /// <summary>Reads binary or ASCII STL, welding vertices that share a position.</summary>
    /// <param name="data">The file's bytes.</param>
    /// <returns>The mesh.</returns>
    public static Mesh ReadStl(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        List<Vector3> corners = [];
        bool binary = data.Length >= 84 && 84 + ((long)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(80)) * 50) == data.Length;
        if (binary)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(80));
            for (int t = 0; t < count; t++)
            {
                int at = 84 + (t * 50) + 12;
                for (int k = 0; k < 3; k++)
                {
                    corners.Add(ReadVector(data, at + (k * 12)));
                }
            }
        }
        else
        {
            foreach (string line in Encoding.ASCII.GetString(data).Split('\n'))
            {
                string[] parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4 && parts[0] == "vertex")
                {
                    corners.Add(new Vector3(Float(parts[1]), Float(parts[2]), Float(parts[3])));
                }
            }
        }

        Dictionary<Vector3, int> index = [];
        List<Vector3> positions = [];
        int[] triangles = new int[corners.Count - (corners.Count % 3)];
        for (int i = 0; i < triangles.Length; i++)
        {
            if (!index.TryGetValue(corners[i], out int v))
            {
                v = positions.Count;
                index.Add(corners[i], v);
                positions.Add(corners[i]);
            }

            triangles[i] = v;
        }

        return new Mesh([.. positions], triangles, doubleSided: !IsClosed(triangles));
    }

    /// <summary>Reads ASCII or little-endian binary PLY: positions, optional colours, polygons.</summary>
    /// <param name="data">The file's bytes.</param>
    /// <returns>The mesh.</returns>
    public static Mesh ReadPly(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        int end = IndexOf(data, "end_header"u8);
        if (end < 0 || !Encoding.ASCII.GetString(data, 0, 3).Equals("ply", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Not a PLY file.");
        }

        int body = Array.IndexOf(data, (byte)'\n', end) + 1;
        string[] header = Encoding.ASCII.GetString(data, 0, end).Split('\n', StringSplitOptions.TrimEntries);
        string format = header.First(l => l.StartsWith("format", StringComparison.Ordinal)).Split(' ')[1];
        if (format == "binary_big_endian")
        {
            throw new InvalidDataException("Big-endian PLY is not supported.");
        }

        List<(string Name, int Count, List<(string Name, string Type, string? CountType)> Properties)> elements = [];
        foreach (string line in header)
        {
            string[] p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length >= 3 && p[0] == "element")
            {
                elements.Add((p[1], int.Parse(p[2], CultureInfo.InvariantCulture), []));
            }
            else if (p.Length >= 3 && p[0] == "property")
            {
                elements[^1].Properties.Add(p[1] == "list" ? (p[4], p[3], p[2]) : (p[2], p[1], null));
            }
        }

        bool ascii = format == "ascii";
        PlyReader reader = ascii ? new PlyReader(Encoding.ASCII.GetString(data, body, data.Length - body)) : new PlyReader(data, body);
        List<Vector3> positions = [];
        List<Vector3> colors = [];
        List<int> triangles = [];
        bool hasColor = false;
        foreach (var element in elements)
        {
            for (int n = 0; n < element.Count; n++)
            {
                if (ascii)
                {
                    reader.NextLine();
                }

                Vector3 position = default, color = Vector3.One;
                List<int>? face = null;
                foreach (var property in element.Properties)
                {
                    if (property.CountType is not null)
                    {
                        int count = (int)reader.Read(property.CountType);
                        List<int> indices = new(count);
                        for (int i = 0; i < count; i++)
                        {
                            indices.Add((int)reader.Read(property.Type));
                        }

                        if (property.Name is "vertex_indices" or "vertex_index")
                        {
                            face = indices;
                        }

                        continue;
                    }

                    double value = reader.Read(property.Type);
                    float scale = property.Type is "uchar" or "uint8" ? 1 / 255f : property.Type is "ushort" or "uint16" ? 1 / 65535f : 1f;
                    switch (property.Name)
                    {
                        case "x": position.X = (float)value; break;
                        case "y": position.Y = (float)value; break;
                        case "z": position.Z = (float)value; break;
                        case "red" or "r": color.X = (float)value * scale; hasColor = true; break;
                        case "green" or "g": color.Y = (float)value * scale; break;
                        case "blue" or "b": color.Z = (float)value * scale; break;
                    }
                }

                if (element.Name == "vertex")
                {
                    positions.Add(position);
                    colors.Add(color);
                }
                else if (element.Name == "face" && face is { Count: >= 3 })
                {
                    Fan(face, triangles);
                }
            }
        }

        int[] t = [.. triangles];
        return new Mesh([.. positions], t, hasColor ? [.. colors] : null, doubleSided: !IsClosed(t));
    }

    /// <summary>Reads Wavefront OBJ, with its .mtl's diffuse texture when it names one beside the file.</summary>
    /// <param name="path">The .obj file.</param>
    /// <returns>The mesh.</returns>
    public static Mesh ReadObj(string path)
    {
        List<Vector3> v = [], vc = [];
        List<Vector2> vt = [];
        List<int> triangles = [];
        Dictionary<(int, int), int> corners = [];
        List<Vector3> positions = [], colors = [];
        List<Vector2> uvs = [];
        bool hasColor = false;
        string? mtl = null;
        foreach (string raw in File.ReadLines(path))
        {
            string[] p = raw.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length == 0)
            {
                continue;
            }

            switch (p[0])
            {
                case "v":
                    v.Add(new Vector3(Float(p[1]), Float(p[2]), Float(p[3])));
                    hasColor |= p.Length >= 7;
                    vc.Add(p.Length >= 7 ? new Vector3(Float(p[4]), Float(p[5]), Float(p[6])) : Vector3.One);
                    break;
                case "vt":
                    // OBJ's v runs up from the image's bottom; Mesh's runs down from its top.
                    vt.Add(new Vector2(Float(p[1]), 1 - (p.Length > 2 ? Float(p[2]) : 0)));
                    break;
                case "mtllib":
                    mtl = string.Join(' ', p[1..]);
                    break;
                case "f":
                    List<int> face = [];
                    for (int i = 1; i < p.Length; i++)
                    {
                        string[] refs = p[i].Split('/');
                        int pi = Resolve(int.Parse(refs[0], CultureInfo.InvariantCulture), v.Count);
                        int ti = refs.Length > 1 && refs[1].Length > 0 ? Resolve(int.Parse(refs[1], CultureInfo.InvariantCulture), vt.Count) : -1;
                        if (!corners.TryGetValue((pi, ti), out int index))
                        {
                            index = positions.Count;
                            corners.Add((pi, ti), index);
                            positions.Add(v[pi]);
                            colors.Add(vc[pi]);
                            uvs.Add(ti >= 0 ? vt[ti] : Vector2.Zero);
                        }

                        face.Add(index);
                    }

                    Fan(face, triangles);
                    break;
            }
        }

        RgbaImage? texture = mtl is null ? null : DiffuseTexture(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, mtl));
        bool textured = texture is not null && vt.Count > 0;
        int[] t = [.. triangles];
        return new Mesh([.. positions], t, hasColor ? [.. colors] : null, textured ? [.. uvs] : null, textured ? texture : null, !IsClosed(t));
    }

    /// <summary>Reads binary glTF 2.0: every mesh the default scene reaches, in world space.</summary>
    /// <param name="data">The file's bytes.</param>
    /// <returns>The meshes merged into one.</returns>
    public static Mesh ReadGlb(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(data) != 0x46546C67)
        {
            throw new InvalidDataException("Not a GLB file (a .gltf with separate files is not supported; save it as .glb).");
        }

        int jsonLength = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(12));
        using JsonDocument document = JsonDocument.Parse(data.AsMemory(20, jsonLength));
        JsonElement root = document.RootElement;
        int binStart = 20 + jsonLength + 8;
        ReadOnlyMemory<byte> bin = binStart <= data.Length ? data.AsMemory(binStart, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(20 + jsonLength))) : ReadOnlyMemory<byte>.Empty;
        GltfReader gltf = new(root, bin);

        List<Vector3> positions = [], colors = [];
        List<Vector2> uvs = [];
        List<int> triangles = [];
        bool anyColor = false, anyUv = false, doubleSided = false;
        RgbaImage? texture = null;

        int scene = root.TryGetProperty("scene", out JsonElement s) ? s.GetInt32() : 0;
        IEnumerable<int> nodes = root.TryGetProperty("scenes", out JsonElement scenes)
            ? scenes[scene].GetProperty("nodes").EnumerateArray().Select(n => n.GetInt32())
            : Enumerable.Range(0, root.TryGetProperty("nodes", out JsonElement all) ? all.GetArrayLength() : 0);
        foreach (int node in nodes)
        {
            Visit(node, Matrix4x4.Identity);
        }

        int[] t = [.. triangles];
        bool keepUv = anyUv && texture is not null;
        return new Mesh([.. positions], t, anyColor ? [.. colors] : null, keepUv ? [.. uvs] : null, keepUv ? texture : null, doubleSided || !IsClosed(t));

        void Visit(int index, Matrix4x4 parent)
        {
            JsonElement node = root.GetProperty("nodes")[index];
            Matrix4x4 world = GltfReader.LocalMatrix(node) * parent;
            if (node.TryGetProperty("mesh", out JsonElement meshIndex))
            {
                foreach (JsonElement primitive in root.GetProperty("meshes")[meshIndex.GetInt32()].GetProperty("primitives").EnumerateArray())
                {
                    if (primitive.TryGetProperty("mode", out JsonElement mode) && mode.GetInt32() != 4)
                    {
                        continue; // points and lines carry no surface
                    }

                    JsonElement attributes = primitive.GetProperty("attributes");
                    Vector3[] p = gltf.Vectors3(attributes.GetProperty("POSITION").GetInt32());
                    int offset = positions.Count;
                    positions.AddRange(p.Select(x => Vector3.Transform(x, world)));
                    if (attributes.TryGetProperty("COLOR_0", out JsonElement c))
                    {
                        anyColor = true;
                        colors.AddRange(gltf.Colors(c.GetInt32()).Select(x => new Vector3(ToSrgb(x.X), ToSrgb(x.Y), ToSrgb(x.Z))));
                    }
                    else
                    {
                        colors.AddRange(Enumerable.Repeat(Vector3.One, p.Length));
                    }

                    if (attributes.TryGetProperty("TEXCOORD_0", out JsonElement uv))
                    {
                        anyUv = true;
                        uvs.AddRange(gltf.Vectors2(uv.GetInt32()));
                    }
                    else
                    {
                        uvs.AddRange(Enumerable.Repeat(Vector2.Zero, p.Length));
                    }

                    int[] indices = primitive.TryGetProperty("indices", out JsonElement ix) ? gltf.Indices(ix.GetInt32()) : [.. Enumerable.Range(0, p.Length)];
                    bool mirrored = world.GetDeterminant() < 0;
                    for (int i = 0; i + 2 < indices.Length; i += 3)
                    {
                        triangles.Add(offset + indices[i]);
                        triangles.Add(offset + indices[mirrored ? i + 2 : i + 1]);
                        triangles.Add(offset + indices[mirrored ? i + 1 : i + 2]);
                    }

                    if (primitive.TryGetProperty("material", out JsonElement m))
                    {
                        JsonElement material = root.GetProperty("materials")[m.GetInt32()];
                        doubleSided |= material.TryGetProperty("doubleSided", out JsonElement ds) && ds.GetBoolean();
                        texture ??= gltf.BaseColorTexture(material);
                    }
                }
            }

            if (node.TryGetProperty("children", out JsonElement children))
            {
                foreach (JsonElement child in children.EnumerateArray())
                {
                    Visit(child.GetInt32(), world);
                }
            }
        }
    }

    private static RgbaImage? DiffuseTexture(string mtlPath)
    {
        if (!File.Exists(mtlPath))
        {
            return null;
        }

        foreach (string line in File.ReadLines(mtlPath))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("map_Kd ", StringComparison.Ordinal))
            {
                // Options such as -s come before the file name, which is the last word.
                string file = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
                string image = Path.Combine(Path.GetDirectoryName(mtlPath)!, file);
                return File.Exists(image) ? ImageCodec.Load(image) : null;
            }
        }

        return null;
    }

    private static int Resolve(int index, int count) => index > 0 ? index - 1 : count + index;

    private static void Fan(List<int> polygon, List<int> triangles)
    {
        for (int i = 1; i + 1 < polygon.Count; i++)
        {
            triangles.Add(polygon[0]);
            triangles.Add(polygon[i]);
            triangles.Add(polygon[i + 1]);
        }
    }

    private static float Float(string text) => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static Vector3 ReadVector(byte[] data, int at) => new(
        BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(at)),
        BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(at + 4)),
        BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(at + 8)));

    private static float ToSrgb(float linear) => linear <= 0.0031308f ? linear * 12.92f : (1.055f * MathF.Pow(linear, 1 / 2.4f)) - 0.055f;

    private static int IndexOf(byte[] data, ReadOnlySpan<byte> pattern) => data.AsSpan(0, Math.Min(data.Length, 4096)).IndexOf(pattern);

    /// <summary>Reads PLY values one at a time, from text or little-endian binary.</summary>
    private sealed class PlyReader
    {
        private readonly byte[]? data;
        private readonly string[]? lines;
        private int position;
        private int line = -1;
        private string[] words = [];
        private int word;

        public PlyReader(byte[] data, int start)
        {
            this.data = data;
            position = start;
        }

        public PlyReader(string text) => lines = text.Split('\n');

        public void NextLine()
        {
            do
            {
                line++;
                words = lines![line].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            }
            while (words.Length == 0);

            word = 0;
        }

        public double Read(string type)
        {
            if (lines is not null)
            {
                return double.Parse(words[word++], NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            ReadOnlySpan<byte> at = data.AsSpan(position);
            (double value, int size) = type switch
            {
                "char" or "int8" => ((double)(sbyte)at[0], 1),
                "uchar" or "uint8" => (at[0], 1),
                "short" or "int16" => (BinaryPrimitives.ReadInt16LittleEndian(at), 2),
                "ushort" or "uint16" => (BinaryPrimitives.ReadUInt16LittleEndian(at), 2),
                "int" or "int32" => (BinaryPrimitives.ReadInt32LittleEndian(at), 4),
                "uint" or "uint32" => (BinaryPrimitives.ReadUInt32LittleEndian(at), 4),
                "float" or "float32" => (BinaryPrimitives.ReadSingleLittleEndian(at), 4),
                "double" or "float64" => (BinaryPrimitives.ReadDoubleLittleEndian(at), 8),
                _ => throw new InvalidDataException($"Unknown PLY type {type}."),
            };
            position += size;
            return value;
        }
    }

    /// <summary>glTF accessors, node transforms and the base-colour texture.</summary>
    private sealed class GltfReader(JsonElement root, ReadOnlyMemory<byte> bin)
    {
        public static Matrix4x4 LocalMatrix(JsonElement node)
        {
            if (node.TryGetProperty("matrix", out JsonElement m))
            {
                float[] v = [.. m.EnumerateArray().Select(x => x.GetSingle())];
                // glTF stores column-major for column vectors; read in order it is System.Numerics' row-vector matrix.
                return new Matrix4x4(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], v[10], v[11], v[12], v[13], v[14], v[15]);
            }

            Vector3 scale = node.TryGetProperty("scale", out JsonElement s) ? Vec3(s) : Vector3.One;
            Quaternion rotation = node.TryGetProperty("rotation", out JsonElement r)
                ? new Quaternion(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle())
                : Quaternion.Identity;
            Vector3 translation = node.TryGetProperty("translation", out JsonElement t) ? Vec3(t) : Vector3.Zero;
            return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
        }

        public Vector3[] Vectors3(int accessor)
        {
            float[] f = Floats(accessor, out int components);
            Vector3[] result = new Vector3[f.Length / components];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new Vector3(f[i * components], f[(i * components) + 1], f[(i * components) + 2]);
            }

            return result;
        }

        public Vector3[] Colors(int accessor) => Vectors3(accessor);

        public Vector2[] Vectors2(int accessor)
        {
            float[] f = Floats(accessor, out int components);
            Vector2[] result = new Vector2[f.Length / components];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new Vector2(f[i * components], f[(i * components) + 1]);
            }

            return result;
        }

        public int[] Indices(int accessor)
        {
            (ReadOnlyMemory<byte> bytes, int count, int type, int stride, _) = View(accessor);
            int[] result = new int[count];
            int size = type == 5121 ? 1 : type == 5123 ? 2 : 4;
            for (int i = 0; i < count; i++)
            {
                ReadOnlySpan<byte> at = bytes.Span[(i * (stride == 0 ? size : stride))..];
                result[i] = size == 1 ? at[0] : size == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(at) : (int)BinaryPrimitives.ReadUInt32LittleEndian(at);
            }

            return result;
        }

        public RgbaImage? BaseColorTexture(JsonElement material)
        {
            if (!material.TryGetProperty("pbrMetallicRoughness", out JsonElement pbr) || !pbr.TryGetProperty("baseColorTexture", out JsonElement reference))
            {
                return null;
            }

            JsonElement texture = root.GetProperty("textures")[reference.GetProperty("index").GetInt32()];
            JsonElement image = root.GetProperty("images")[texture.GetProperty("source").GetInt32()];
            if (!image.TryGetProperty("bufferView", out JsonElement viewIndex))
            {
                return null; // an external image file, which a .glb should not need
            }

            JsonElement view = root.GetProperty("bufferViews")[viewIndex.GetInt32()];
            int offset = view.TryGetProperty("byteOffset", out JsonElement o) ? o.GetInt32() : 0;
            using MemoryStream stream = new(bin.Slice(offset, view.GetProperty("byteLength").GetInt32()).ToArray());
            return ImageCodec.Decode(stream, "the model's texture");
        }

        private static Vector3 Vec3(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());

        private float[] Floats(int accessor, out int components)
        {
            (ReadOnlyMemory<byte> bytes, int count, int type, int stride, JsonElement described) = View(accessor);
            components = described.GetProperty("type").GetString() switch
            {
                "SCALAR" => 1,
                "VEC2" => 2,
                "VEC3" => 3,
                "VEC4" => 4,
                string other => throw new InvalidDataException($"Unsupported accessor type {other}."),
                null => throw new InvalidDataException("An accessor has no type."),
            };
            bool normalized = described.TryGetProperty("normalized", out JsonElement n) && n.GetBoolean();
            int size = type switch { 5126 => 4, 5123 or 5122 => 2, 5121 or 5120 => 1, _ => 4 };
            int elementStride = stride == 0 ? size * components : stride;
            float[] result = new float[count * components];
            for (int i = 0; i < count; i++)
            {
                for (int c = 0; c < components; c++)
                {
                    ReadOnlySpan<byte> at = bytes.Span[((i * elementStride) + (c * size))..];
                    result[(i * components) + c] = type switch
                    {
                        5126 => BinaryPrimitives.ReadSingleLittleEndian(at),
                        5123 => BinaryPrimitives.ReadUInt16LittleEndian(at) / (normalized ? 65535f : 1f),
                        5121 => at[0] / (normalized ? 255f : 1f),
                        5122 => Math.Max(BinaryPrimitives.ReadInt16LittleEndian(at) / (normalized ? 32767f : 1f), -1f),
                        5120 => Math.Max((sbyte)at[0] / (normalized ? 127f : 1f), -1f),
                        _ => throw new InvalidDataException($"Unsupported component type {type}."),
                    };
                }
            }

            return result;
        }

        private (ReadOnlyMemory<byte> Bytes, int Count, int Type, int Stride, JsonElement Accessor) View(int index)
        {
            JsonElement accessor = root.GetProperty("accessors")[index];
            int count = accessor.GetProperty("count").GetInt32();
            int type = accessor.GetProperty("componentType").GetInt32();
            if (!accessor.TryGetProperty("bufferView", out JsonElement viewIndex))
            {
                throw new InvalidDataException("Sparse or empty accessors are not supported.");
            }

            JsonElement view = root.GetProperty("bufferViews")[viewIndex.GetInt32()];
            if (view.GetProperty("buffer").GetInt32() != 0)
            {
                throw new InvalidDataException("Only the GLB's own binary buffer is supported.");
            }

            int offset = (view.TryGetProperty("byteOffset", out JsonElement vo) ? vo.GetInt32() : 0) + (accessor.TryGetProperty("byteOffset", out JsonElement ao) ? ao.GetInt32() : 0);
            int stride = view.TryGetProperty("byteStride", out JsonElement st) ? st.GetInt32() : 0;
            return (bin[offset..], count, type, stride, accessor);
        }
    }
}
