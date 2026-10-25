using Modelado.Imaging;

namespace Modelado.Imaging.Tests;

/// <summary>MOD39: a generated image carries the prompt and seed that remake it, in the PNG itself.</summary>
public sealed class PngTextTests
{
    [Fact]
    public void The_text_survives_the_file_and_the_image_still_reads()
    {
        RgbaImage image = new(3, 2);
        image.Pixels[0] = 255;
        image.Pixels[3] = 255; // opaque, so the encoder keeps its colour
        Dictionary<string, string> text = new() { ["Prompt"] = "a wooden chair, centred, café light", ["Seed"] = "1234" };

        byte[] png = ImageCodec.EncodePng(image, text);

        Assert.Equal(text, ImageCodec.ReadPngText(png));
        using MemoryStream stream = new(png);
        RgbaImage read = ImageCodec.Decode(stream);
        Assert.Equal((3, 2, 255), (read.Width, read.Height, read.Pixels[0]));
    }

    [Fact]
    public void A_plain_png_carries_no_text()
    {
        Assert.Empty(ImageCodec.ReadPngText(ImageCodec.EncodePng(new RgbaImage(1, 1))));
    }

    [Fact]
    public void A_keyword_png_does_not_allow_is_refused()
    {
        Assert.Throws<ArgumentException>(() => ImageCodec.EncodePng(new RgbaImage(1, 1), new Dictionary<string, string> { ["Prompt\n"] = "x" }));
    }
}
