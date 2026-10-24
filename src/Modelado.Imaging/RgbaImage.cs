namespace Modelado.Imaging;

/// <summary>
/// An 8-bit RGBA image, straight (not premultiplied) alpha, rows top to bottom with no padding.
/// What every stage hands the next: the decoded photo, the cut-out object, a mesh's texture.
/// </summary>
public sealed class RgbaImage
{
    /// <summary>Creates a transparent black image.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    public RgbaImage(int width, int height)
        : this(width, height, new byte[checked(width * height * 4)])
    {
    }

    /// <summary>Wraps existing pixels.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="pixels">width × height × 4 bytes, RGBA.</param>
    public RgbaImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != checked(width * height * 4))
        {
            throw new ArgumentException($"Expected {width * height * 4} bytes for {width} x {height}, got {pixels.Length}.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>The samples, RGBA per pixel.</summary>
    public byte[] Pixels { get; }

    /// <summary>Whether any pixel is less than fully opaque.</summary>
    public bool HasTransparency
    {
        get
        {
            for (int i = 3; i < Pixels.Length; i += 4)
            {
                if (Pixels[i] < 255)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>A copy that shares nothing with this image.</summary>
    /// <returns>The copy.</returns>
    public RgbaImage Clone() => new(Width, Height, (byte[])Pixels.Clone());

    /// <summary>A pixel's offset into <see cref="Pixels"/>.</summary>
    /// <param name="x">Column.</param>
    /// <param name="y">Row.</param>
    /// <returns>The index of its red sample.</returns>
    public int OffsetOf(int x, int y) => ((y * Width) + x) * 4;

    /// <summary>The colour at a point, bilinearly interpolated, with edges clamped.</summary>
    /// <param name="u">0 at the left edge, 1 at the right.</param>
    /// <param name="v">0 at the top edge, 1 at the bottom.</param>
    /// <returns>Red, green and blue in [0, 1].</returns>
    public (float R, float G, float B) Sample(float u, float v)
    {
        float fx = Math.Clamp((u * Width) - 0.5f, 0, Width - 1);
        float fy = Math.Clamp((v * Height) - 0.5f, 0, Height - 1);
        int x0 = (int)fx, y0 = (int)fy;
        int x1 = Math.Min(x0 + 1, Width - 1), y1 = Math.Min(y0 + 1, Height - 1);
        float tx = fx - x0, ty = fy - y0;
        float r = 0, g = 0, b = 0;
        Accumulate(x0, y0, (1 - tx) * (1 - ty));
        Accumulate(x1, y0, tx * (1 - ty));
        Accumulate(x0, y1, (1 - tx) * ty);
        Accumulate(x1, y1, tx * ty);
        return (r / 255f, g / 255f, b / 255f);

        void Accumulate(int x, int y, float weight)
        {
            int i = OffsetOf(x, y);
            r += Pixels[i] * weight;
            g += Pixels[i + 1] * weight;
            b += Pixels[i + 2] * weight;
        }
    }
}
