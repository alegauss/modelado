using Modelado.Imaging;
using SkiaSharp;

/// <summary>
/// MOD81: one Microsoft Store screenshot, a capture of the real window composed at 1920 x 1080 under its
/// caption, after claude-tray's T412 and a layout the owner liked: the brand mark in the top-left corner,
/// a headline whose key words are in the accent colour, a line under it, the window on the icon's blues
/// with a wire mesh in two corners, and, where the model was made from a photo, that photo as a card
/// with an arrow to the window. The window is drawn as captured: the Store's policy, and the people who
/// install it, both expect the listing to show the app as it is.
/// </summary>
internal static class StoreShot
{
    private const int Width = 1920;
    private const int Height = 1080;
    private const float Margin = 64;

    // The headline's words in [brackets] are drawn in this; the rest in Ink.
    private static readonly SKColor Accent = new(0x5C, 0xD6, 0xFF);
    private static readonly SKColor Ink = new(0xF4, 0xF7, 0xFB);
    private static readonly SKColor Soft = new(0xB4, 0xC2, 0xD6);
    private static readonly SKColor Top = new(0x15, 0x22, 0x40);
    private static readonly SKColor Bottom = new(0x0A, 0x10, 0x20);

    /// <summary>Composes the screenshot.</summary>
    /// <param name="capture">The window, as a PNG.</param>
    /// <param name="icon">The app icon, as a PNG at least twice the 84 px it is drawn at.</param>
    /// <param name="headline">The caption, with the words to stress in [brackets].</param>
    /// <param name="subline">The smaller line under it.</param>
    /// <param name="photo">The photo the model was made from, or null.</param>
    /// <returns>The PNG.</returns>
    public static byte[] Compose(byte[] capture, byte[] icon, string headline, string subline, byte[]? photo)
    {
        using SKSurface surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        SKCanvas canvas = surface.Canvas;
        using SKImage window = SKImage.FromEncodedData(capture);

        SKRect area = new(Margin, 214, Width - Margin, Height - 36);
        SKRect windowArea = photo is null ? area : new SKRect(600, area.Top, area.Right, area.Bottom);
        float scale = Math.Min(1f, Math.Min(windowArea.Width / window.Width, windowArea.Height / window.Height));
        float w = window.Width * scale, h = window.Height * scale;
        float x = photo is null ? windowArea.MidX - (w / 2) : windowArea.Right - w;
        float y = windowArea.Top + ((windowArea.Height - h) / 2);
        SKRect frame = new(x, y, x + w, y + h);

        Backdrop(canvas, frame);
        BrandMark(canvas, icon);
        Headline(canvas, headline, subline, 470);
        Window(canvas, window, frame);
        if (photo is not null)
        {
            PhotoCard(canvas, photo, frame);
        }

        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void Backdrop(SKCanvas canvas, SKRect frame)
    {
        using (SKPaint fill = new() { IsAntialias = true })
        {
            fill.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, Height), [Top, Bottom], SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, Width, Height, fill);
        }

        // A glow of the icon's light blue behind the window, so it reads as lit rather than pasted on.
        using (SKPaint glow = new() { IsAntialias = true })
        {
            SKColor light = new(Brand.IconTileLight.R, Brand.IconTileLight.G, Brand.IconTileLight.B);
            glow.Shader = SKShader.CreateRadialGradient(new SKPoint(frame.MidX, frame.MidY), 1000, [light.WithAlpha(70), light.WithAlpha(0)], SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, Width, Height, glow);
        }

        // The wire mesh, bottom left, and the same turned half round into the top right.
        Wave(canvas);
        canvas.Save();
        canvas.Translate(Width, Height);
        canvas.Scale(-1, -1);
        Wave(canvas);
        canvas.Restore();
    }

    // A height field seen in perspective, as a wire grid fading with distance: "3D" without a word.
    private static void Wave(SKCanvas canvas)
    {
        const int across = 40, deep = 16;
        const float left = -120, ground = Height + 60, span = 980, depth = 420, lift = 70;
        SKPoint At(int i, int j)
        {
            float u = i / (float)across, v = j / (float)deep;
            float shrink = 1 - (0.4f * v);
            float px = left + (u * span * shrink) + (v * span * 0.18f);
            float py = ground - (v * depth) - (lift * MathF.Sin((u * 2.6f * MathF.PI) + (v * 3.1f)) * (1 - (0.45f * v)));
            return new SKPoint(px, py);
        }

        using SKPaint line = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.4f };
        for (int j = 0; j <= deep; j++)
        {
            line.Color = Accent.WithAlpha((byte)(70 * (1 - (0.75f * j / deep))));
            for (int i = 0; i < across; i++)
            {
                canvas.DrawLine(At(i, j), At(i + 1, j), line);
            }
        }

        for (int i = 0; i <= across; i++)
        {
            for (int j = 0; j < deep; j++)
            {
                line.Color = Accent.WithAlpha((byte)(55 * (1 - (0.75f * j / deep))));
                canvas.DrawLine(At(i, j), At(i, j + 1), line);
            }
        }
    }

    private static void BrandMark(SKCanvas canvas, byte[] iconPng)
    {
        using SKImage icon = SKImage.FromEncodedData(iconPng);
        canvas.DrawImage(icon, new SKRect(Margin, 52, Margin + 84, 136), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        using SKTypeface semibold = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using SKTypeface regular = SKTypeface.FromFamilyName("Segoe UI");
        using SKFont name = new(semibold, 46);
        using SKFont by = new(regular, 24);
        using SKPaint ink = new() { IsAntialias = true, Color = Ink };
        using SKPaint soft = new() { IsAntialias = true, Color = Soft };
        canvas.DrawText("Modelado", 168, 104, SKTextAlign.Left, name, ink);
        canvas.DrawText("by Alegauss", 170, 136, SKTextAlign.Left, by, soft);

        using SKPaint rule = new() { IsAntialias = true, Color = Soft.WithAlpha(90), StrokeWidth = 2 };
        canvas.DrawLine(430, 50, 430, 150, rule);
    }

    private static void Headline(SKCanvas canvas, string headline, string subline, float left)
    {
        // "[" and "]" split the headline into runs that alternate plain and stressed.
        List<(string Text, bool Stressed)> runs = [];
        bool stressed = false;
        foreach (string part in headline.Split('[', ']'))
        {
            if (part.Length > 0)
            {
                runs.Add((part, stressed));
            }

            stressed = !stressed;
        }

        using SKTypeface bold = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using SKTypeface regular = SKTypeface.FromFamilyName("Segoe UI");

        // The type shrinks before the headline runs past the right margin, down to a floor.
        float room = Width - Margin - left, size = 66;
        using SKFont font = new(bold, size);
        while (size > 44 && runs.Sum(r => font.MeasureText(r.Text)) > room)
        {
            size -= 1;
            font.Size = size;
        }

        float x = left;
        using SKPaint paint = new() { IsAntialias = true };
        foreach ((string text, bool accent) in runs)
        {
            paint.Color = accent ? Accent : Ink;
            canvas.DrawText(text, x, 112, SKTextAlign.Left, font, paint);
            x += font.MeasureText(text);
        }

        using SKFont small = new(regular, 31);
        using SKPaint soft = new() { IsAntialias = true, Color = Soft };
        canvas.DrawText(subline, left + 2, 162, SKTextAlign.Left, small, soft);
    }

    private static void Window(SKCanvas canvas, SKImage window, SKRect frame)
    {
        SKRoundRect rounded = new(frame, 12, 12);
        using (SKPaint shadow = new() { IsAntialias = true, Color = SKColors.Black })
        {
            shadow.ImageFilter = SKImageFilter.CreateDropShadowOnly(0, 18, 28, 28, SKColors.Black.WithAlpha(150));
            canvas.DrawRoundRect(rounded, shadow);
        }

        canvas.Save();
        canvas.ClipRoundRect(rounded, SKClipOperation.Intersect, antialias: true);
        canvas.DrawImage(window, frame, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.Restore();

        using SKPaint edge = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, Color = SKColors.White.WithAlpha(50) };
        canvas.DrawRoundRect(rounded, edge);
    }

    // The photo the model came from, as a print tilted a little, left of the window, and an arrow from
    // it into the window's viewport: "this photo became this model".
    private static void PhotoCard(SKCanvas canvas, byte[] photoPng, SKRect frame)
    {
        using SKImage photo = SKImage.FromEncodedData(photoPng);
        float fit = Math.Min(400f / photo.Width, 440f / photo.Height);
        float pw = photo.Width * fit, ph = photo.Height * fit;
        const float border = 12;
        float cx = 300, cy = frame.MidY - 10;
        SKRect picture = new(cx - (pw / 2), cy - (ph / 2), cx + (pw / 2), cy + (ph / 2));
        SKRect card = SKRect.Inflate(picture, border, border);

        canvas.Save();
        canvas.RotateDegrees(-3, cx, cy);
        using (SKPaint paper = new() { IsAntialias = true, Color = new SKColor(0xFA, 0xFB, 0xFD) })
        {
            paper.ImageFilter = SKImageFilter.CreateDropShadow(0, 16, 22, 22, SKColors.Black.WithAlpha(150));
            canvas.DrawRoundRect(card, 10, 10, paper);
        }

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(picture, 4, 4), SKClipOperation.Intersect, antialias: true);
        canvas.DrawImage(photo, picture, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.Restore();
        canvas.Restore();

        using SKTypeface semibold = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
        using SKFont label = new(semibold, 28);
        using SKPaint soft = new() { IsAntialias = true, Color = Soft };
        canvas.DrawText("Your photo", cx, card.Bottom + 52, SKTextAlign.Center, label, soft);

        // The arrow: from the card's right edge, over the gap, into the viewport.
        SKPoint from = new(card.Right + 10, cy + 30), to = new(frame.Left + 70, cy - 10);
        SKPoint c1 = new(from.X + 40, cy - 90), c2 = new(to.X - 90, cy - 90);
        using SKPathBuilder curve = new();
        curve.MoveTo(from);
        curve.CubicTo(c1, c2, to);
        using SKPath path = curve.Detach();
        using SKPaint stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 12, StrokeCap = SKStrokeCap.Round, Color = Accent };
        stroke.ImageFilter = SKImageFilter.CreateDropShadow(0, 4, 8, 8, SKColors.Black.WithAlpha(120));
        canvas.DrawPath(path, stroke);

        float angle = MathF.Atan2(to.Y - c2.Y, to.X - c2.X);
        SKPoint Along(float back, float side) => new(
            to.X + (MathF.Cos(angle) * back) - (MathF.Sin(angle) * side),
            to.Y + (MathF.Sin(angle) * back) + (MathF.Cos(angle) * side));
        using SKPathBuilder head = new();
        head.AddPoly([Along(18, 0), Along(-22, -22), Along(-22, 22)], close: true);
        using SKPath tip = head.Detach();
        using SKPaint fill = new() { IsAntialias = true, Color = Accent };
        canvas.DrawPath(tip, fill);
    }
}
