using System.Globalization;

namespace Modelado.Cloud;

/// <summary>
/// The requests the window and modelado-cli send for the same action (MOD45), so the two price,
/// send and are recorded alike: a texture drawn from a material's name (MOD40), and the open
/// model remeshed or retextured (MOD38).
/// </summary>
public static class CloudRequests
{
    /// <summary>The material wrapped in a description that asks for a flat, seamless square.</summary>
    public const string TextureTemplate = "{material}, a flat seamless tileable texture seen straight on, evenly lit, no shadows, no perspective, no objects, no text";

    /// <summary>A texture drawn from a material's name.</summary>
    /// <param name="material">The material, e.g. "weathered oak planks".</param>
    /// <param name="count">How many candidates.</param>
    /// <returns>The request.</returns>
    public static CloudJobRequest Texture(string material, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(material);
        return new CloudJobRequest(
            CloudCapability.TextToImage,
            Prompt: TextureTemplate.Replace("{material}", material.Trim(), StringComparison.Ordinal),
            Options: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["count"] = count.ToString(CultureInfo.InvariantCulture),
                ["speed"] = "turbo",
                ["aspect"] = "1x1",
            });
    }

    /// <summary>A model remeshed to a polycount.</summary>
    /// <param name="glb">The model, as a GLB.</param>
    /// <param name="polycount">The triangles or quads to reach.</param>
    /// <param name="quads">Quads rather than triangles.</param>
    /// <returns>The request.</returns>
    public static CloudJobRequest Remesh(byte[] glb, int polycount, bool quads) => new(
        CloudCapability.Remesh,
        Model: glb,
        Options: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["polycount"] = polycount.ToString(CultureInfo.InvariantCulture),
            ["topology"] = quads ? "quad" : "triangle",
        });

    /// <summary>A model given a new texture from a description.</summary>
    /// <param name="glb">The model, as a GLB.</param>
    /// <param name="prompt">What the new surface looks like.</param>
    /// <param name="keepUv">Keep the model's own UVs, where it has them.</param>
    /// <returns>The request.</returns>
    public static CloudJobRequest Retexture(byte[] glb, string prompt, bool keepUv)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        return new CloudJobRequest(
            CloudCapability.Retexture,
            Prompt: prompt.Trim(),
            Model: glb,
            Options: new Dictionary<string, string>(StringComparer.Ordinal) { ["keepuv"] = keepUv ? "true" : "false" });
    }
}
