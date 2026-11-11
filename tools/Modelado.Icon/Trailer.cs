using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Modelado.Geometry;
using Modelado.Imaging;
using SkiaSharp;

/// <summary>
/// MOD84: the Store listing's trailer, about 37 seconds at 1920 x 1080 and 30 frames a second, in the
/// screenshots' look (MOD81). The models turn as the app's own renderer draws them, the camera stepped a
/// fixed angle per frame, so the trailer is remade from the same files whenever the renderer changes and
/// shows nothing the app does not. The steps with the interface are the composed screenshots of the
/// listing's language, eased in. Frames go straight into ffmpeg's standard input as raw RGBA, so no
/// folder of a thousand PNGs is written. The thumbnail the Store asks beside each trailer is one frame
/// of the first scene.
/// </summary>
internal static class Trailer
{
    private const int Width = 1920;
    private const int Height = 1080;
    private const int Fps = 30;
    private const int Fade = 9;

    private static readonly JsonSerializerOptions SpecJson = new() { PropertyNameCaseInsensitive = true };

    /// <summary>What one language's trailer says and shows, read from the JSON store-trailer.ps1 writes.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Made by JsonSerializer.")]
    internal sealed record Spec(
        string[] Headlines,
        string[] Sublines,
        string PhotoLabel,
        string ViewsHeadline,
        string ViewsSubline,
        string End,
        string ObjectModel,
        string ObjectPhoto,
        string ReliefModel,
        string ReliefPhoto,
        string[] Screenshots);

    private sealed record Scene(int Frames, SKImage Background, Action<SKCanvas, float>? Draw);

    /// <summary>Renders the trailer into an MP4 and its thumbnail beside it, as a PNG of the same name.</summary>
    /// <param name="output">The MP4 to write.</param>
    /// <param name="specPath">The JSON spec.</param>
    /// <param name="icon">The app icon as a PNG at least 168 px.</param>
    /// <param name="bigIcon">The app icon as a PNG at least 280 px, for the closing card.</param>
    /// <returns>ffmpeg's exit code.</returns>
    public static int Make(string output, string specPath, byte[] icon, byte[] bigIcon)
    {
        Spec spec = JsonSerializer.Deserialize<Spec>(File.ReadAllText(specPath), SpecJson)
            ?? throw new InvalidDataException("The trailer spec is empty.");

        MeshRenderer chair = new(MeshImport.Load(spec.ObjectModel));
        MeshRenderer relief = new(MeshImport.Load(spec.ReliefModel));
        byte[] chairPhoto = File.ReadAllBytes(spec.ObjectPhoto);
        byte[] reliefPhoto = File.ReadAllBytes(spec.ReliefPhoto);

        // Where a model is drawn: right of a photo card, or in the middle under the headline.
        SKRect besidePhoto = Square(new SKRect(600, 214, Width - 64, Height - 36));
        SKRect centred = Square(new SKRect(64, 214, Width - 64, Height - 20));

        List<Scene> scenes =
        [
            // One photo becomes a whole model: a full turn.
            new(Frames(9.0), Backdrop(icon, spec.Headlines[0], spec.Sublines[0], chairPhoto, spec.PhotoLabel, besidePhoto),
                (c, t) => Model(c, chair, besidePhoto, new OrbitCamera(Yaw: 20 + (360 * t), Pitch: 14), RenderStyle.Color)),

            // The viewer's three ways of drawing it, still turning; the wireframe from closer, where its
            // triangles can be told apart.
            new(Frames(7.5), Backdrop(icon, spec.ViewsHeadline, spec.ViewsSubline, null, "", centred),
                (c, t) => Model(c, chair, centred, new OrbitCamera(Yaw: 20 + (200 * t), Pitch: 16, Zoom: t < 2 / 3f ? 1f : 1.3f),
                    t < 1 / 3f ? RenderStyle.Color : t < 2 / 3f ? RenderStyle.Clay : RenderStyle.Wireframe)),

            // A relief has a front only, so it swings rather than turns.
            new(Frames(7.0), Backdrop(icon, spec.Headlines[1], spec.Sublines[1], reliefPhoto, spec.PhotoLabel, besidePhoto),
                (c, t) => Model(c, relief, besidePhoto, new OrbitCamera(Yaw: 38 * MathF.Sin(t * 2 * MathF.PI), Pitch: 8), RenderStyle.Color)),

            // The interface: Edit and improve, then Export, as the listing's screenshots show them.
            Still(spec.Screenshots[2], 5.0),
            Still(spec.Screenshots[3], 5.0),

            new(Frames(4.0), Closing(bigIcon, spec.End), null),
        ];

        string thumbnail = Path.ChangeExtension(output, ".png");
        SKImageInfo info = new(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(info);
        byte[] pixels = new byte[Width * Height * 4];
        GCHandle pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        using Process ffmpeg = StartFfmpeg(output);
        int written = 0;
        try
        {
            using Stream input = ffmpeg.StandardInput.BaseStream;
            foreach (Scene scene in scenes)
            {
                for (int f = 0; f < scene.Frames; f++)
                {
                    SKCanvas canvas = surface.Canvas;
                    canvas.DrawImage(scene.Background, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
                    scene.Draw?.Invoke(canvas, f / (float)scene.Frames);

                    // In and out through the backdrop's dark, so scenes do not cut.
                    int edge = Math.Min(f, scene.Frames - 1 - f);
                    if (edge < Fade)
                    {
                        using SKPaint dark = new() { Color = StoreShot.Bottom.WithAlpha((byte)(255 * (1 - (edge / (float)Fade)))) };
                        canvas.DrawRect(0, 0, Width, Height, dark);
                    }

                    // The thumbnail: one second in, past the fade, the chair still facing as in its photo.
                    if (written == Fps)
                    {
                        using SKImage still = surface.Snapshot();
                        using SKData png = still.Encode(SKEncodedImageFormat.Png, 100);
                        File.WriteAllBytes(thumbnail, png.ToArray());
                    }

                    surface.ReadPixels(info, pinned.AddrOfPinnedObject(), Width * 4, 0, 0);
                    input.Write(pixels);
                    written++;
                }

                scene.Background.Dispose();
            }
        }
        finally
        {
            pinned.Free();
        }

        ffmpeg.WaitForExit();
        Console.WriteLine($"trailer: {output}  {written} frames, {written / (double)Fps:0.0} s; thumbnail {thumbnail}");
        return ffmpeg.ExitCode;
    }

    private static int Frames(double seconds) => (int)Math.Round(seconds * Fps);

    // The headline, the brand mark, the photo card and the backdrop: everything in a scene that does not move.
    private static SKImage Backdrop(byte[] icon, string headline, string subline, byte[]? photo, string photoLabel, SKRect model)
    {
        using SKSurface surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;
        StoreShot.Backdrop(canvas, Width, Height, new SKPoint(model.MidX, model.MidY));
        StoreShot.BrandMark(canvas, icon);
        StoreShot.Headline(canvas, headline, subline, 470);
        if (photo is not null)
        {
            StoreShot.PhotoCard(canvas, photo, model, photoLabel);
        }

        return surface.Snapshot();
    }

    // One frame of a turning model, drawn by the app's renderer on a clear background into its square.
    private static void Model(SKCanvas canvas, MeshRenderer renderer, SKRect at, OrbitCamera camera, RenderStyle style)
    {
        int side = (int)at.Width;
        RgbaImage image = renderer.RenderImage(side, side, camera, style, transparent: true);
        using SKData data = SKData.CreateCopy(image.Pixels);
        using SKImage model = SKImage.FromPixels(new SKImageInfo(side, side, SKColorType.Rgba8888, SKAlphaType.Unpremul), data, side * 4);
        canvas.DrawImage(model, at, new SKSamplingOptions(SKFilterMode.Linear));
    }

    // A composed screenshot, eased in from 100% to 105% so it does not sit like a slide.
    private static Scene Still(string png, double seconds)
    {
        SKImage shot = SKImage.FromEncodedData(png);
        using SKSurface blank = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        blank.Canvas.Clear(StoreShot.Bottom);
        return new Scene(Frames(seconds), blank.Snapshot(), (canvas, t) =>
        {
            float zoom = 1 + (0.05f * t);
            canvas.Save();
            canvas.Scale(zoom, zoom, Width / 2f, Height / 2f);
            canvas.DrawImage(shot, new SKRect(0, 0, Width, Height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            canvas.Restore();
        });
    }

    // The closing card: the icon large, the name, the maker and where to get it.
    private static SKImage Closing(byte[] bigIcon, string end)
    {
        using SKSurface surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;
        StoreShot.Backdrop(canvas, Width, Height, new SKPoint(Width / 2f, Height / 2f));
        using SKImage icon = SKImage.FromEncodedData(bigIcon);
        canvas.DrawImage(icon, SKRect.Create((Width / 2f) - 140, 210, 280, 280), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        using SKTypeface bold = StoreShot.Face("Modelado", SKFontStyleWeight.Bold);
        using SKTypeface regular = StoreShot.Face("by Alegauss", SKFontStyleWeight.Normal);
        using SKTypeface endFace = StoreShot.Face(end, SKFontStyleWeight.SemiBold);
        using SKPaint ink = new() { IsAntialias = true, Color = StoreShot.Ink };
        using SKPaint soft = new() { IsAntialias = true, Color = StoreShot.Soft };
        using SKPaint accent = new() { IsAntialias = true, Color = StoreShot.Accent };
        using SKFont name = new(bold, 110);
        using SKFont by = new(regular, 36);
        using SKFont where = new(endFace, 52);
        canvas.DrawText("Modelado", Width / 2f, 620, SKTextAlign.Center, name, ink);
        canvas.DrawText("by Alegauss", Width / 2f, 680, SKTextAlign.Center, by, soft);
        canvas.DrawText(end, Width / 2f, 800, SKTextAlign.Center, where, accent);
        return surface.Snapshot();
    }

    private static SKRect Square(SKRect area)
    {
        float side = Math.Min(area.Width, area.Height);
        return SKRect.Create(area.MidX - (side / 2), area.MidY - (side / 2), side, side);
    }

    private static Process StartFfmpeg(string output)
    {
        ProcessStartInfo start = new("ffmpeg") { RedirectStandardInput = true, UseShellExecute = false };
        foreach (string arg in new[]
        {
            "-y", "-loglevel", "error",
            "-f", "rawvideo", "-pix_fmt", "rgba", "-s", $"{Width}x{Height}", "-r", $"{Fps}", "-i", "-",
            "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            output,
        })
        {
            start.ArgumentList.Add(arg);
        }

        return Process.Start(start) ?? throw new InvalidOperationException("ffmpeg did not start; it must be on the PATH.");
    }
}
