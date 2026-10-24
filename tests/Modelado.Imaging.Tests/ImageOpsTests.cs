using Modelado.Imaging;

namespace Modelado.Imaging.Tests;

public sealed class ImageOpsTests
{
    private static RgbaImage Square(int side, int left, int top, int size, byte alpha = 255)
    {
        RgbaImage image = new(side, side);
        for (int y = top; y < top + size; y++)
        {
            for (int x = left; x < left + size; x++)
            {
                int i = image.OffsetOf(x, y);
                image.Pixels[i] = 200;
                image.Pixels[i + 1] = 100;
                image.Pixels[i + 2] = 50;
                image.Pixels[i + 3] = alpha;
            }
        }

        return image;
    }

    [Fact]
    public void Framing_centres_the_object_at_the_ratio()
    {
        RgbaImage framed = ImageOps.FrameObject(Square(100, 5, 60, 20), 0.5);

        Assert.Equal(40, framed.Width);
        Assert.Equal((10, 10, 20, 20), ImageOps.OpaqueBounds(framed));
    }

    [Fact]
    public void Framing_an_empty_image_says_no_object_was_found()
    {
        Assert.Throws<InvalidOperationException>(() => ImageOps.FrameObject(new RgbaImage(10, 10), 0.85));
    }

    [Fact]
    public void Compositing_lays_transparency_on_the_grey()
    {
        RgbaImage composite = ImageOps.Composite(Square(4, 0, 0, 2), 0.5f);

        Assert.Equal(new byte[] { 200, 100, 50, 255 }, composite.Pixels[..4]);
        Assert.Equal(new byte[] { 128, 128, 128, 255 }, composite.Pixels[^4..]);
        Assert.False(composite.HasTransparency);
    }

    [Fact]
    public void Planar_rgb_is_three_planes_in_zero_to_one()
    {
        float[] tensor = ImageOps.ToPlanarRgb(Square(2, 0, 0, 2));

        Assert.Equal(12, tensor.Length);
        Assert.Equal(200 / 255f, tensor[0]);
        Assert.Equal(100 / 255f, tensor[4]);
        Assert.Equal(50 / 255f, tensor[8]);
    }

    [Fact]
    public void A_mask_resizes_bilinearly_and_keeps_its_range()
    {
        float[] mask = [0, 1, 0, 1];
        float[] wide = ImageOps.ResizeMask(mask, 2, 2, 4, 4);

        Assert.Equal(16, wide.Length);
        Assert.All(wide, v => Assert.InRange(v, 0f, 1f));
        Assert.Equal(0f, wide[0]);
        Assert.Equal(1f, wide[3]);
    }

    [Fact]
    public void Fit_shrinks_only_what_is_too_large()
    {
        RgbaImage small = new(100, 50);

        Assert.Same(small, ImageOps.Fit(small, 200));
        RgbaImage fitted = ImageOps.Fit(new RgbaImage(400, 100), 200);
        Assert.Equal((200, 50), (fitted.Width, fitted.Height));
    }

    [Fact]
    public void Png_round_trips_every_sample()
    {
        RgbaImage image = Square(8, 2, 2, 4, alpha: 77);
        RgbaImage back = ImageCodec.Decode(new MemoryStream(ImageCodec.EncodePng(image)));

        Assert.Equal(image.Pixels, back.Pixels);
    }

    [Fact]
    public void A_file_that_is_not_an_image_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => ImageCodec.Decode(new MemoryStream("not an image"u8.ToArray()), "notes.txt"));
    }

    [Fact]
    public void Sampling_interpolates_between_pixels()
    {
        RgbaImage image = new(2, 1, [0, 0, 0, 255, 255, 255, 255, 255]);

        Assert.Equal(0.5f, image.Sample(0.5f, 0.5f).R, 1e-3f);
        Assert.Equal(0f, image.Sample(0f, 0.5f).R, 1e-3f);
        Assert.Equal(1f, image.Sample(1f, 0.5f).R, 1e-3f);
    }
}
