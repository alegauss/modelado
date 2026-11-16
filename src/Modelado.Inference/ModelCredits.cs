namespace Modelado.Inference;

/// <summary>MOD126: a model that made a result, and the notice its license requires wherever it is used.</summary>
/// <param name="Model">The model's name.</param>
/// <param name="Notice">The notice, e.g. "Powered by Stability AI".</param>
public sealed record ModelCredit(string Model, string Notice);

/// <summary>
/// MOD126: the notices a result owes. A license with an attribution condition (the Stability AI
/// Community License's "Powered by Stability AI") requires it wherever its model is used, so the
/// window shows it beside the result and the export writes it into the file's metadata. The terms
/// are read from the model's own meta.json first, then from the manifest, which carries a
/// conditional license's terms even where the meta.json does not; a permissive model owes none.
/// </summary>
public static class ModelCredits
{
    /// <summary>The notices the models owe, one per model that has one, in the order given.</summary>
    /// <param name="models">The models a run used.</param>
    /// <param name="library">Where installed models' meta.json files are read.</param>
    /// <param name="manifest">The manifest, or null when it is not loaded.</param>
    /// <returns>The credits; none for permissive models.</returns>
    public static IReadOnlyList<ModelCredit> For(IEnumerable<string> models, ModelLibrary library, ModelManifest? manifest)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(library);
        List<ModelCredit> credits = [];
        foreach (string model in models.Distinct(StringComparer.Ordinal))
        {
            ModelConditions? terms = null;
            try
            {
                terms = library.Meta(model).Upstream.Conditions;
            }
            catch (Exception error) when (error is FileNotFoundException or InvalidDataException or IOException)
            {
                // Not installed, or its meta.json does not read: the manifest may still say.
            }

            terms ??= manifest?.Model(model)?.Conditions;
            if (terms?.Attribution is { Length: > 0 } notice)
            {
                credits.Add(new ModelCredit(model, notice));
            }
        }

        return credits;
    }
}
