using Microsoft.ML.OnnxRuntime;
using Modelado.Imaging;

namespace Modelado.Inference.Pipelines;

/// <summary>
/// Cuts the object out of a photo with U-2-Net, the way TripoSR's run.py does through rembg:
/// the photo at 320 × 320, divided by its brightest sample and normalised with ImageNet's mean
/// and std; the saliency map min-max scaled and resized back to the photo.
/// </summary>
public static class BackgroundRemoval
{
    /// <summary>The model's folder name.</summary>
    public const string Model = "u2net";

    /// <summary>The object's opacity per pixel of the photo.</summary>
    /// <param name="runner">The sessions.</param>
    /// <param name="photo">The photo.</param>
    /// <returns>One value in [0, 1] per photo pixel.</returns>
    public static float[] Mask(ModelRunner runner, RgbaImage photo)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(photo);
        ModelMeta meta = runner.Library.Meta(Model);
        int size = meta.Preprocess.Size ?? 320;
        float[] input = Prepare(photo, size, meta.Preprocess);

        using OrtValue tensor = OrtValue.CreateTensorValueFromMemory(input, [1, 3, size, size]);
        string output = meta.Outputs[0].Name;
        using IDisposableReadOnlyCollection<OrtValue> results = runner.Run(Model, new Dictionary<string, OrtValue> { [meta.Inputs[0].Name] = tensor }, [output]);
        ReadOnlySpan<float> saliency = results[0].GetTensorDataAsSpan<float>()[..(size * size)];

        float low = float.MaxValue, high = float.MinValue;
        foreach (float v in saliency)
        {
            low = MathF.Min(low, v);
            high = MathF.Max(high, v);
        }

        float range = MathF.Max(high - low, 1e-6f);
        float[] scaled = new float[size * size];
        for (int i = 0; i < scaled.Length; i++)
        {
            scaled[i] = (saliency[i] - low) / range;
        }

        return ImageOps.ResizeMask(scaled, size, size, photo.Width, photo.Height);
    }

    /// <summary>The photo with the mask as its alpha, or the photo itself when it already has transparency.</summary>
    /// <param name="runner">The sessions.</param>
    /// <param name="photo">The photo.</param>
    /// <param name="force">Run the model even on a photo that already has transparency.</param>
    /// <returns>The cut-out object.</returns>
    public static RgbaImage CutOut(ModelRunner runner, RgbaImage photo, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(photo);
        return !force && photo.HasTransparency ? photo : ImageOps.WithAlpha(photo, Mask(runner, photo));
    }

    /// <summary>The 1 × 3 × size × size input rembg builds.</summary>
    /// <param name="photo">The photo.</param>
    /// <param name="size">The model's side.</param>
    /// <param name="pre">meta.json's preprocessing.</param>
    /// <returns>The planar tensor.</returns>
    public static float[] Prepare(RgbaImage photo, int size, ModelPreprocess pre)
    {
        ArgumentNullException.ThrowIfNull(pre);
        RgbaImage resized = ImageOps.Resize(photo, size, size);
        float[] rgb = ImageOps.ToPlanarRgb(resized);
        float max = 1f / 255f;
        if (pre.ScaleByMax)
        {
            foreach (float v in rgb)
            {
                max = MathF.Max(max, v);
            }
        }
        else
        {
            max = 1f;
        }

        int plane = size * size;
        for (int c = 0; c < 3; c++)
        {
            float mean = pre.Mean?[c] ?? 0f, std = pre.Std?[c] ?? 1f;
            for (int i = 0; i < plane; i++)
            {
                rgb[(c * plane) + i] = ((rgb[(c * plane) + i] / max) - mean) / std;
            }
        }

        return rgb;
    }
}
