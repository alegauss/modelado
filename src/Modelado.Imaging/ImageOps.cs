using SkiaSharp;

namespace Modelado.Imaging;

/// <summary>Resizing, cropping and alpha: the steps between a photo and a model's input.</summary>
public static class ImageOps
{
    /// <summary>
    /// Resizes with Skia's cubic (Mitchell) filter, which averages enough source pixels when
    /// shrinking to avoid aliasing; colour and alpha are filtered unpremultiplied.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="width">New width.</param>
    /// <param name="height">New height.</param>
    /// <returns>The resized image.</returns>
    public static RgbaImage Resize(RgbaImage image, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width == width && image.Height == height)
        {
            return image.Clone();
        }

        using SKBitmap source = ImageCodec.ToBitmap(image);
        using SKBitmap target = new(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        source.ScalePixels(target, new SKSamplingOptions(SKCubicResampler.Mitchell));
        byte[] pixels = new byte[width * height * 4];
        System.Runtime.InteropServices.Marshal.Copy(target.GetPixels(), pixels, 0, pixels.Length);
        return new RgbaImage(width, height, pixels);
    }

    /// <summary>Scales so the longer side is at most <paramref name="longest"/>; smaller images are kept.</summary>
    /// <param name="image">The image.</param>
    /// <param name="longest">The longest side allowed.</param>
    /// <returns>The image or a smaller copy.</returns>
    public static RgbaImage Fit(RgbaImage image, int longest)
    {
        ArgumentNullException.ThrowIfNull(image);
        int side = Math.Max(image.Width, image.Height);
        if (side <= longest)
        {
            return image;
        }

        double scale = (double)longest / side;
        return Resize(image, Math.Max(1, (int)Math.Round(image.Width * scale)), Math.Max(1, (int)Math.Round(image.Height * scale)));
    }

    /// <summary>A mask (one byte per pixel) resized bilinearly, for a model's low-resolution output.</summary>
    /// <param name="mask">The mask, values 0 to 1.</param>
    /// <param name="width">Its width.</param>
    /// <param name="height">Its height.</param>
    /// <param name="newWidth">New width.</param>
    /// <param name="newHeight">New height.</param>
    /// <returns>The resized mask.</returns>
    public static float[] ResizeMask(ReadOnlySpan<float> mask, int width, int height, int newWidth, int newHeight)
    {
        float[] result = new float[newWidth * newHeight];
        float sx = (float)width / newWidth, sy = (float)height / newHeight;
        for (int y = 0; y < newHeight; y++)
        {
            float fy = Math.Clamp(((y + 0.5f) * sy) - 0.5f, 0, height - 1);
            int y0 = (int)fy, y1 = Math.Min(y0 + 1, height - 1);
            float ty = fy - y0;
            for (int x = 0; x < newWidth; x++)
            {
                float fx = Math.Clamp(((x + 0.5f) * sx) - 0.5f, 0, width - 1);
                int x0 = (int)fx, x1 = Math.Min(x0 + 1, width - 1);
                float tx = fx - x0;
                float top = (mask[(y0 * width) + x0] * (1 - tx)) + (mask[(y0 * width) + x1] * tx);
                float bottom = (mask[(y1 * width) + x0] * (1 - tx)) + (mask[(y1 * width) + x1] * tx);
                result[(y * newWidth) + x] = (top * (1 - ty)) + (bottom * ty);
            }
        }

        return result;
    }

    /// <summary>The image with its alpha replaced by a mask.</summary>
    /// <param name="image">The image.</param>
    /// <param name="mask">One value in [0, 1] per pixel.</param>
    /// <returns>A copy whose alpha is the mask.</returns>
    public static RgbaImage WithAlpha(RgbaImage image, ReadOnlySpan<float> mask)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (mask.Length != image.Width * image.Height)
        {
            throw new ArgumentException("The mask is not one value per pixel.", nameof(mask));
        }

        RgbaImage result = image.Clone();
        for (int i = 0; i < mask.Length; i++)
        {
            result.Pixels[(i * 4) + 3] = (byte)Math.Clamp(MathF.Round(mask[i] * 255f), 0, 255);
        }

        return result;
    }

    /// <summary>The smallest rectangle holding every pixel whose alpha is above the threshold.</summary>
    /// <param name="image">The image.</param>
    /// <param name="threshold">Alpha, 0 to 255, a pixel must exceed.</param>
    /// <returns>The rectangle, or null when no pixel does.</returns>
    public static (int X, int Y, int Width, int Height)? OpaqueBounds(RgbaImage image, byte threshold = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        int left = image.Width, top = image.Height, right = -1, bottom = -1;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (image.Pixels[image.OffsetOf(x, y) + 3] > threshold)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        return right < 0 ? null : (left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    /// TripoSR's run.py framing: the object cropped, centred on a transparent square, and padded
    /// so it fills <paramref name="ratio"/> of the side.
    /// </summary>
    /// <param name="image">An image whose alpha is the object.</param>
    /// <param name="ratio">The share of the side the object fills, 0.85 in TripoSR.</param>
    /// <returns>The square image.</returns>
    /// <exception cref="InvalidOperationException">No pixel is opaque.</exception>
    public static RgbaImage FrameObject(RgbaImage image, double ratio)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ratio);
        (int x, int y, int w, int h) = OpaqueBounds(image) ?? throw new InvalidOperationException("No object was found in the image: every pixel is transparent.");
        int side = Math.Max(w, h);
        int padded = Math.Max(side, (int)(side / ratio));
        RgbaImage result = new(padded, padded);
        int offsetX = ((padded - side) / 2) + ((side - w) / 2);
        int offsetY = ((padded - side) / 2) + ((side - h) / 2);
        for (int row = 0; row < h; row++)
        {
            Array.Copy(image.Pixels, image.OffsetOf(x, y + row), result.Pixels, result.OffsetOf(offsetX, offsetY + row), w * 4);
        }

        return result;
    }

    /// <summary>The image laid over a flat grey, alpha becoming opaque.</summary>
    /// <param name="image">The image.</param>
    /// <param name="grey">The background, 0 to 1 (0.5 for TripoSR).</param>
    /// <returns>An opaque copy.</returns>
    public static RgbaImage Composite(RgbaImage image, float grey)
    {
        ArgumentNullException.ThrowIfNull(image);
        RgbaImage result = image.Clone();
        float background = grey * 255f;
        byte[] p = result.Pixels;
        for (int i = 0; i < p.Length; i += 4)
        {
            float a = p[i + 3] / 255f;
            p[i] = (byte)MathF.Round((p[i] * a) + (background * (1 - a)));
            p[i + 1] = (byte)MathF.Round((p[i + 1] * a) + (background * (1 - a)));
            p[i + 2] = (byte)MathF.Round((p[i + 2] * a) + (background * (1 - a)));
            p[i + 3] = 255;
        }

        return result;
    }

    /// <summary>The image as a 1 × 3 × H × W tensor of RGB in [0, 1].</summary>
    /// <param name="image">The image.</param>
    /// <returns>The planar floats.</returns>
    public static float[] ToPlanarRgb(RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        int plane = image.Width * image.Height;
        float[] tensor = new float[plane * 3];
        byte[] p = image.Pixels;
        for (int i = 0; i < plane; i++)
        {
            tensor[i] = p[i * 4] / 255f;
            tensor[plane + i] = p[(i * 4) + 1] / 255f;
            tensor[(2 * plane) + i] = p[(i * 4) + 2] / 255f;
        }

        return tensor;
    }
}
