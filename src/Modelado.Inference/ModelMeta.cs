using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modelado.Inference;

/// <summary>
/// One model's meta.json, as scripts/export_models.py writes it beside the ONNX variants.
/// A field that changes meaning bumps <see cref="Schema"/>, which <see cref="Load"/> refuses
/// when unknown, so an old app never misreads a newer model.
/// </summary>
/// <param name="Schema">The meta.json format version.</param>
/// <param name="Name">The model's name, e.g. triposr-encoder.</param>
/// <param name="Task">image-to-triplane, triplane-decoder, depth or background-removal.</param>
/// <param name="Inputs">The ONNX inputs, in order.</param>
/// <param name="Outputs">The ONNX outputs, in order.</param>
/// <param name="Preprocess">How an image becomes the first input.</param>
/// <param name="Params">The task's numbers, e.g. TripoSR's radius and density threshold.</param>
/// <param name="Variants">The ONNX files, by variant name (fp32, fp16).</param>
/// <param name="Upstream">Where the weights came from and under which license.</param>
public sealed record ModelMeta(
    int Schema,
    string Name,
    string Task,
    IReadOnlyList<ModelTensor> Inputs,
    IReadOnlyList<ModelTensor> Outputs,
    ModelPreprocess Preprocess,
    IReadOnlyDictionary<string, double> Params,
    IReadOnlyDictionary<string, ModelVariant> Variants,
    ModelUpstream Upstream)
{
    /// <summary>The only meta.json format this build reads.</summary>
    public const int SupportedSchema = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    /// <summary>Reads and checks a meta.json file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The model's description.</returns>
    /// <exception cref="InvalidDataException">The file is not a meta.json this build can trust.</exception>
    public static ModelMeta Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        ModelMeta meta;
        try
        {
            meta = JsonSerializer.Deserialize<ModelMeta>(stream, Options) ?? throw new InvalidDataException($"{path} is empty.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"{path} is not a valid meta.json: {error.Message}", error);
        }

        if (meta.Schema != SupportedSchema)
        {
            throw new InvalidDataException($"{path} is meta.json schema {meta.Schema}; this build reads {SupportedSchema}.");
        }

        if (!meta.Variants.ContainsKey("fp32"))
        {
            throw new InvalidDataException($"{path} lists no fp32 variant.");
        }

        if (meta.Inputs.Count == 0 || meta.Outputs.Count == 0)
        {
            throw new InvalidDataException($"{path} names no input or no output.");
        }

        return meta;
    }

    /// <summary>A number from <see cref="Params"/>.</summary>
    /// <param name="name">Its key.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">meta.json does not have it.</exception>
    public double Param(string name) =>
        Params.TryGetValue(name, out double value) ? value : throw new InvalidDataException($"{Name}'s meta.json has no params.{name}.");
}

/// <summary>An ONNX input or output.</summary>
/// <param name="Name">Its name in the graph.</param>
/// <param name="Shape">Its dimensions; a dynamic one is a name such as "n" or "height", read as -1.</param>
public sealed record ModelTensor(string Name, [property: JsonConverter(typeof(ShapeConverter))] IReadOnlyList<int> Shape);

/// <summary>How an image becomes the model's input.</summary>
/// <param name="ValueRange">[low, high] that pixel 0 and 255 map to, before any mean and std.</param>
/// <param name="Size">The square side the image is resized to, or the longest side for a model with a <paramref name="Multiple"/>.</param>
/// <param name="Multiple">Height and width must be a multiple of this (14 for Depth Anything), or null.</param>
/// <param name="Mean">Subtracted per channel after scaling, or null when the graph normalises itself.</param>
/// <param name="Std">Divided per channel after the mean, or null.</param>
/// <param name="ScaleByMax">rembg's habit: divide by the image's brightest sample instead of 255.</param>
/// <param name="Background">The grey the cut-out object is laid on (TripoSR), or null.</param>
/// <param name="ForegroundRatio">The share of the side the object fills (TripoSR), or null.</param>
public sealed record ModelPreprocess(
    IReadOnlyList<double> ValueRange,
    int? Size = null,
    int? Multiple = null,
    IReadOnlyList<float>? Mean = null,
    IReadOnlyList<float>? Std = null,
    bool ScaleByMax = false,
    float? Background = null,
    double? ForegroundRatio = null);

/// <summary>One ONNX file.</summary>
/// <param name="File">Its name in the model's folder.</param>
/// <param name="Bytes">Its size.</param>
/// <param name="Sha256">Its SHA-256, lowercase hex, which a download is checked against.</param>
public sealed record ModelVariant(string File, long Bytes, string Sha256);

/// <summary>Where the weights came from.</summary>
/// <param name="Url">The upstream weights.</param>
/// <param name="License">Their license, as the About panel shows it.</param>
/// <param name="LicenseUrl">The license text.</param>
public sealed record ModelUpstream(string Url, string License, string LicenseUrl);

/// <summary>Reads a shape whose dynamic dimensions are names, as -1. Public only for [JsonConverter].</summary>
public sealed class ShapeConverter : JsonConverter<IReadOnlyList<int>>
{
    /// <inheritdoc/>
    public override IReadOnlyList<int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("A shape is an array.");
        }

        List<int> shape = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            shape.Add(reader.TokenType switch
            {
                JsonTokenType.Number => reader.GetInt32(),
                JsonTokenType.String => -1,
                _ => throw new JsonException("A dimension is a number or a name."),
            });
        }

        return shape;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<int> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartArray();
        foreach (int dimension in value)
        {
            if (dimension < 0)
            {
                writer.WriteStringValue("dynamic");
            }
            else
            {
                writer.WriteNumberValue(dimension);
            }
        }

        writer.WriteEndArray();
    }
}
