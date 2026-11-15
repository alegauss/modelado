using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace Modelado.Geometry;

/// <summary>
/// The 3MF writer (MOD103). 3MF is an OPC package: a zip holding [Content_Types].xml, the root
/// relationship in _rels/.rels, and one model part, 3D/3dmodel.model, whose mesh is a list of
/// vertices and of triangles. The model declares unit="millimeter", so a slicer reads the size
/// rather than guessing it as it must for an STL. The colour per vertex goes in the materials
/// extension's colour group, one colour per vertex, each triangle pointing at its three corners'
/// colours; it is not listed as required, so a reader without the extension still takes the shape.
/// Positions are written as they are, as the STL writer does, so the two agree on size and on
/// which way is up. A triangle with a repeated corner is left out, which the core spec forbids.
/// </summary>
public static partial class MeshExport
{
    private const string CoreNamespace = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";
    private const string MaterialNamespace = "http://schemas.microsoft.com/3dmanufacturing/material/2015/02";

    /// <summary>The model part's path inside the package, which the root relationship points at.</summary>
    public const string ThreeMfModelPart = "3D/3dmodel.model";

    /// <summary>Writes 3MF: the mesh in millimetres, with its colour per vertex where it has one.</summary>
    /// <param name="mesh">The mesh, its coordinates in millimetres.</param>
    /// <param name="stream">Where; it is left open.</param>
    public static void Write3mf(Mesh mesh, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(stream);
        using ZipArchive package = new(stream, ZipArchiveMode.Create, leaveOpen: true);
        WritePart(package, "[Content_Types].xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
            + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
            + "<Default Extension=\"model\" ContentType=\"application/vnd.ms-package.3dmanufacturing-3dmodel+xml\"/>"
            + "</Types>\n");
        WritePart(package, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + $"<Relationship Target=\"/{ThreeMfModelPart}\" Id=\"rel0\" Type=\"http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel\"/>"
            + "</Relationships>\n");

        ZipArchiveEntry model = package.CreateEntry(ThreeMfModelPart, CompressionLevel.Optimal);
        using StreamWriter xml = new(model.Open(), Utf8) { NewLine = "\n" };
        WriteModel(mesh, xml);
    }

    private static void WritePart(ZipArchive package, string name, string content)
    {
        ZipArchiveEntry entry = package.CreateEntry(name, CompressionLevel.Optimal);
        using Stream part = entry.Open();
        part.Write(Utf8.GetBytes(content));
    }

    private static void WriteModel(Mesh mesh, StreamWriter xml)
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        Vector3[]? colours = VertexColors(mesh);
        xml.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.WriteLine($"<model unit=\"millimeter\" xml:lang=\"en-US\" xmlns=\"{CoreNamespace}\" xmlns:m=\"{MaterialNamespace}\">");
        xml.WriteLine(" <metadata name=\"Application\">Modelado</metadata>");
        xml.WriteLine(" <resources>");

        // The colour group is resource 1 and the object resource 2; the object's own pid/pindex is
        // the first colour, for any triangle a reader takes without its own.
        if (colours is not null)
        {
            xml.WriteLine("  <m:colorgroup id=\"1\">");
            foreach (Vector3 colour in colours)
            {
                xml.WriteLine(string.Format(c, "   <m:color color=\"#{0:X2}{1:X2}{2:X2}\"/>", ToByte(colour.X), ToByte(colour.Y), ToByte(colour.Z)));
            }

            xml.WriteLine("  </m:colorgroup>");
        }

        xml.WriteLine(colours is null ? "  <object id=\"2\" type=\"model\">" : "  <object id=\"2\" type=\"model\" pid=\"1\" pindex=\"0\">");
        xml.WriteLine("   <mesh>");
        xml.WriteLine("    <vertices>");
        foreach (Vector3 p in mesh.Positions)
        {
            xml.WriteLine(string.Format(c, "     <vertex x=\"{0:G9}\" y=\"{1:G9}\" z=\"{2:G9}\"/>", p.X, p.Y, p.Z));
        }

        xml.WriteLine("    </vertices>");
        xml.WriteLine("    <triangles>");
        int[] t = mesh.Triangles;
        for (int i = 0; i < t.Length; i += 3)
        {
            int a = t[i], b = t[i + 1], d = t[i + 2];
            if (a == b || b == d || a == d)
            {
                continue;
            }

            xml.WriteLine(colours is null
                ? string.Format(c, "     <triangle v1=\"{0}\" v2=\"{1}\" v3=\"{2}\"/>", a, b, d)
                : string.Format(c, "     <triangle v1=\"{0}\" v2=\"{1}\" v3=\"{2}\" pid=\"1\" p1=\"{0}\" p2=\"{1}\" p3=\"{2}\"/>", a, b, d));
        }

        xml.WriteLine("    </triangles>");
        xml.WriteLine("   </mesh>");
        xml.WriteLine("  </object>");
        xml.WriteLine(" </resources>");
        xml.WriteLine(" <build>");
        xml.WriteLine("  <item objectid=\"2\"/>");
        xml.WriteLine(" </build>");
        xml.WriteLine("</model>");
    }
}
