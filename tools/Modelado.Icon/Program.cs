using Modelado.Imaging;
using SkiaSharp;

// Modelado's icon (MOD7): an isometric cube on a rounded tile, drawn on a 512 grid with shapes large
// enough to survive 16 px. A Windows .ico holds one picture per size and the shell picks the
// closest, so each size is drawn rather than scaled from one: up to 32 px the cube is three flat
// faces, and from 48 px a triangle grid on them says "mesh". Each entry is a PNG, which the .ico
// format has allowed since Windows Vista, as in PhotoBoost's app-icon script.
//
//   dotnet run --project tools\Modelado.Icon [out.ico]     default src\Modelado.App\Modelado.ico

int[] sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
string output = args.Length > 0 ? args[0] : Path.Combine(FindRoot(), "src", "Modelado.App", "Modelado.ico");

List<byte[]> pngs = [.. sizes.Select(Draw)];
using (FileStream file = File.Create(output))
using (BinaryWriter writer = new(file))
{
    // ICONDIR (6 bytes), one ICONDIRENTRY (16 bytes) per image, then the images themselves.
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)sizes.Length);
    int offset = 6 + (16 * sizes.Length);
    for (int i = 0; i < sizes.Length; i++)
    {
        byte side = sizes[i] >= 256 ? (byte)0 : (byte)sizes[i]; // 0 means 256
        writer.Write(side);
        writer.Write(side);
        writer.Write((byte)0); // palette colours: none
        writer.Write((byte)0); // reserved
        writer.Write((ushort)1); // colour planes
        writer.Write((ushort)32); // bits per pixel
        writer.Write(pngs[i].Length);
        writer.Write(offset);
        offset += pngs[i].Length;
    }

    foreach (byte[] png in pngs)
    {
        writer.Write(png);
    }
}

Console.WriteLine($"app icon: {output}  {string.Join(", ", sizes)} px  ({new FileInfo(output).Length / 1024} kB)");
return 0;

static byte[] Draw(int size)
{
    using SKSurface surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
    SKCanvas canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    canvas.Scale(size / 512f);

    // The tile, lit from the top left.
    using (SKPaint tile = new() { IsAntialias = true })
    {
        tile.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(512, 512), [Sk(Brand.IconTileLight), Sk(Brand.IconTileDeep)], SKShaderTileMode.Clamp);
        canvas.DrawRoundRect(new SKRect(0, 0, 512, 512), 112, 112, tile);
    }

    // The cube, edge 176 on the isometric axes, centred on the tile.
    SKPoint top = new(256, 80), left = new(103.6f, 168), right = new(408.4f, 168), centre = new(256, 256);
    SKPoint lowLeft = new(103.6f, 344), lowRight = new(408.4f, 344), bottom = new(256, 432);
    SKPoint[][] faces = [[top, right, centre, left], [left, centre, bottom, lowLeft], [centre, right, lowRight, bottom]];
    (byte R, byte G, byte B)[] shades = [Brand.IconCubeTop, Brand.IconCubeLeft, Brand.IconCubeRight];
    for (int f = 0; f < faces.Length; f++)
    {
        using SKPaint face = new() { IsAntialias = true, Color = Sk(shades[f]) };
        using SKPathBuilder builder = new();
        builder.AddPoly(faces[f], close: true);
        using SKPath path = builder.Detach();
        canvas.DrawPath(path, face);
    }

    // From 48 px, each face as a 2 × 2 grid of quads split into triangles, faint, so the cube reads
    // as a mesh without the lines turning to mush at small sizes.
    if (size >= 48)
    {
        using SKPaint line = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 7,
            StrokeCap = SKStrokeCap.Round,
            Color = Sk(Brand.IconTileDeep).WithAlpha(110),
        };
        foreach (SKPoint[] q in faces)
        {
            SKPoint At(float u, float v) => Lerp(Lerp(q[0], q[1], u), Lerp(q[3], q[2], u), v);
            canvas.DrawLine(At(0.5f, 0), At(0.5f, 1), line);
            canvas.DrawLine(At(0, 0.5f), At(1, 0.5f), line);
            canvas.DrawLine(At(0, 0), At(1, 1), line);
        }
    }

    using SKImage image = surface.Snapshot();
    using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

static SKColor Sk((byte R, byte G, byte B) colour) => new(colour.R, colour.G, colour.B);

static SKPoint Lerp(SKPoint a, SKPoint b, float t) => new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));

static string FindRoot()
{
    for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Modelado.slnx")))
        {
            return dir.FullName;
        }
    }

    throw new InvalidOperationException("Modelado.slnx was not found above the tool's folder.");
}
