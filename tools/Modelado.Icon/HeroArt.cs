using Modelado.Geometry;
using Modelado.Imaging;
using SkiaSharp;

/// <summary>
/// MOD82: the listing's 16:9 Super hero art, without which the Store plays no trailer. A model the app
/// made from a photo (the site's chair), drawn by the app's own renderer on the screenshots' backdrop,
/// whose wire mesh says "3D" without a word. Not half as a wireframe: at 46,000 triangles the edges
/// merge into a grey wash that reads as damage, not as a mesh. No text: the
/// Store sets the title and the buttons over the left third and the bottom, so the model sits right
/// of centre and above the bottom quarter.
/// </summary>
internal static class HeroArt
{
    /// <summary>Draws the art.</summary>
    /// <param name="model">The GLB to draw.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <returns>The PNG.</returns>
    public static byte[] Draw(string model, int width, int height)
    {
        Mesh mesh = MeshImport.Load(model);
        MeshRenderer renderer = new(mesh);
        int side = (int)(height * 0.86f);
        OrbitCamera camera = new(Yaw: 32, Pitch: 14, Zoom: 1.05f);
        using SKImage colour = ToSkia(renderer.RenderImage(side, side, camera, RenderStyle.Color, transparent: true));

        using SKSurface surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;
        SKRect at = SKRect.Create(width * 0.66f - (side / 2f), height * 0.40f - (side / 2f), side, side);
        StoreShot.Backdrop(canvas, width, height, new SKPoint(at.MidX, at.MidY));

        // A soft shadow under the model, then the model.
        using (SKPaint shadow = new() { IsAntialias = true })
        {
            shadow.Shader = SKShader.CreateRadialGradient(new SKPoint(at.MidX, at.Bottom - (side * 0.07f)), side * 0.32f,
                [SKColors.Black.WithAlpha(120), SKColors.Black.WithAlpha(0)], SKShaderTileMode.Clamp);
            canvas.Save();
            canvas.Scale(1, 0.18f, at.MidX, at.Bottom - (side * 0.07f));
            canvas.DrawCircle(at.MidX, at.Bottom - (side * 0.07f), side * 0.32f, shadow);
            canvas.Restore();
        }

        canvas.DrawImage(colour, at, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKImage ToSkia(RgbaImage image)
    {
        SKImageInfo info = new(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKData pixels = SKData.CreateCopy(image.Pixels);
        return SKImage.FromPixels(info, pixels, image.Width * 4);
    }
}
