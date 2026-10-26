using SkiaSharp;

namespace Modelado.Imaging;

/// <summary>Reads photos into <see cref="RgbaImage"/> and writes PNG and JPEG, through SkiaSharp.</summary>
public static class ImageCodec
{
    /// <summary>The extensions the open dialog offers.</summary>
    public static readonly IReadOnlyList<string> Extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"];

    /// <summary>Decodes a file, turned upright by its EXIF orientation.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The image.</returns>
    /// <exception cref="InvalidDataException">The file is not an image this build can read.</exception>
    public static RgbaImage Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Decode(stream, path);
    }

    /// <summary>Decodes a stream, turned upright by its EXIF orientation.</summary>
    /// <param name="stream">The encoded image.</param>
    /// <param name="name">What error messages call it.</param>
    /// <returns>The image.</returns>
    /// <exception cref="InvalidDataException">The bytes are not an image this build can read.</exception>
    public static RgbaImage Decode(Stream stream, string name = "the image")
    {
        using SKCodec? codec = SKCodec.Create(stream);
        if (codec is null)
        {
            throw new InvalidDataException($"{name} is not an image Modelado can read (PNG, JPEG, WebP, BMP or GIF).");
        }

        SKImageInfo info = new(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul, SKColorSpace.CreateSrgb());
        using SKBitmap decoded = new(info);
        SKCodecResult result = codec.GetPixels(info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            throw new InvalidDataException($"{name} could not be decoded: {result}.");
        }

        using SKBitmap upright = Orient(decoded, codec.EncodedOrigin);
        return FromBitmap(upright);
    }

    /// <summary>Encodes an image as JPEG, dropping any alpha (MOD63: a GLB's opaque texture).</summary>
    /// <param name="image">The image.</param>
    /// <param name="quality">0 to 100.</param>
    /// <returns>The JPEG file's bytes.</returns>
    public static byte[] EncodeJpeg(RgbaImage image, int quality = 90)
    {
        ArgumentNullException.ThrowIfNull(image);
        using SKBitmap bitmap = ToBitmap(image);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }

    /// <summary>Encodes an image as PNG.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The PNG file's bytes.</returns>
    public static byte[] EncodePng(RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using SKBitmap bitmap = ToBitmap(image);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Encodes an image as PNG carrying text (MOD39): each entry an iTXt chunk, PNG's UTF-8 text,
    /// placed after the header, so a generated image keeps the prompt and seed that remake it.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="text">Keyword and text, e.g. "Prompt": "a wooden chair".</param>
    /// <returns>The PNG file's bytes.</returns>
    public static byte[] EncodePng(RgbaImage image, IReadOnlyDictionary<string, string> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        byte[] png = EncodePng(image);
        const int AfterHeader = 8 + 4 + 4 + 13 + 4; // signature, then IHDR's length, type, data and CRC
        using MemoryStream output = new();
        output.Write(png, 0, AfterHeader);
        foreach ((string keyword, string value) in text)
        {
            if (keyword.Length is 0 or > 79 || keyword.Any(c => c is < ' ' or > '~'))
            {
                throw new ArgumentException($"'{keyword}' is not a PNG text keyword.", nameof(text));
            }

            // keyword \0, uncompressed (0, 0), no language tag \0, no translated keyword \0, UTF-8 text.
            byte[] data = [.. System.Text.Encoding.Latin1.GetBytes(keyword), 0, 0, 0, 0, 0, .. System.Text.Encoding.UTF8.GetBytes(value)];
            WriteChunk(output, "iTXt", data);
        }

        output.Write(png, AfterHeader, png.Length - AfterHeader);
        return output.ToArray();
    }

    /// <summary>The iTXt and tEXt entries a PNG carries.</summary>
    /// <param name="png">The PNG file's bytes.</param>
    /// <returns>Keyword and text; empty for a file that is not a PNG.</returns>
    public static IReadOnlyDictionary<string, string> ReadPngText(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        Dictionary<string, string> text = new(StringComparer.Ordinal);
        for (int at = 8; at + 12 <= png.Length;)
        {
            int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
            if (length < 0 || at + 12 + length > png.Length)
            {
                break;
            }

            string type = System.Text.Encoding.ASCII.GetString(png, at + 4, 4);
            ReadOnlySpan<byte> data = png.AsSpan(at + 8, length);
            int nul = data.IndexOf((byte)0);
            if (nul > 0 && type is "tEXt")
            {
                text[System.Text.Encoding.Latin1.GetString(data[..nul])] = System.Text.Encoding.Latin1.GetString(data[(nul + 1)..]);
            }
            else if (nul > 0 && type is "iTXt" && data.Length >= nul + 3 && data[nul + 1] == 0)
            {
                ReadOnlySpan<byte> rest = data[(nul + 3)..];
                int language = rest.IndexOf((byte)0);
                int translated = language < 0 ? -1 : rest[(language + 1)..].IndexOf((byte)0);
                if (translated >= 0)
                {
                    text[System.Text.Encoding.Latin1.GetString(data[..nul])] = System.Text.Encoding.UTF8.GetString(rest[(language + 1 + translated + 1)..]);
                }
            }

            at += 12 + length;
        }

        return text;
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        byte[] typed = [.. System.Text.Encoding.ASCII.GetBytes(type), .. data];
        output.Write(typed);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(number, Crc32(typed));
        output.Write(number);
    }

    // PNG's CRC-32 (ISO 3309), over a chunk's type and data.
    private static uint Crc32(byte[] bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return ~crc;
    }

    /// <summary>Writes an image as a PNG file.</summary>
    /// <param name="image">The image.</param>
    /// <param name="path">The file to create or replace.</param>
    public static void SavePng(RgbaImage image, string path) => File.WriteAllBytes(path, EncodePng(image));

    internal static SKBitmap ToBitmap(RgbaImage image)
    {
        SKImageInfo info = new(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKBitmap bitmap = new(info);
        System.Runtime.InteropServices.Marshal.Copy(image.Pixels, 0, bitmap.GetPixels(), image.Pixels.Length);
        return bitmap;
    }

    private static RgbaImage FromBitmap(SKBitmap bitmap)
    {
        byte[] pixels = new byte[bitmap.Width * bitmap.Height * 4];
        System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return new RgbaImage(bitmap.Width, bitmap.Height, pixels);
    }

    // The eight EXIF orientations as the transform that makes the stored pixels upright.
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
        {
            return source.Copy();
        }

        bool swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int width = swaps ? source.Height : source.Width;
        int height = swaps ? source.Width : source.Height;
        SKBitmap target = new(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using SKCanvas canvas = new(target);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Scale(-1, 1, width / 2f, 0);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.RotateDegrees(180, width / 2f, height / 2f);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Scale(1, -1, 0, height / 2f);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1, 0, source.Height / 2f);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, -1, source.Width / 2f, source.Height / 2f);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
        }

        using SKPaint paint = new() { BlendMode = SKBlendMode.Src };
        using SKImage pixels = SKImage.FromBitmap(source);
        canvas.DrawImage(pixels, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), paint);
        return target;
    }
}
