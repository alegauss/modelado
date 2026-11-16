using System.Globalization;
using System.Text.Json.Nodes;

namespace Modelado.Inference;

/// <summary>
/// MOD123: the licenses a person agreed to before a model with conditions was downloaded, kept in
/// settings.json under "modelConsents": each model's name, the license text it was agreed to by its
/// URL, and when. A model whose license URL changes is asked about again, since that is a different
/// text. Nothing leaves the PC: this is the app's own record that the terms were shown and accepted.
/// </summary>
public static class ModelConsents
{
    private const string Key = "modelConsents";

    /// <summary>When the person agreed to this model's license, or null when they have not (or agreed to another text).</summary>
    /// <param name="settings">The settings file.</param>
    /// <param name="model">The model.</param>
    /// <returns>The time agreed, in UTC, or null.</returns>
    public static DateTimeOffset? Agreed(string settings, ManifestModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return SettingsFile.ReadObject(settings, Key)?[model.Name] is JsonObject entry
            && (string?)entry["license_url"] == model.LicenseUrl
            && DateTimeOffset.TryParse((string?)entry["agreed"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset at)
            ? at
            : null;
    }

    /// <summary>Records that the person agreed to these models' licenses now.</summary>
    /// <param name="settings">The settings file.</param>
    /// <param name="models">The models agreed to.</param>
    /// <param name="now">The time.</param>
    public static void Record(string settings, IEnumerable<ManifestModel> models, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(models);
        JsonObject consents = SettingsFile.ReadObject(settings, Key) is { } kept ? (JsonObject)kept.DeepClone() : [];
        foreach (ManifestModel model in models)
        {
            consents[model.Name] = new JsonObject
            {
                ["license"] = model.License,
                ["license_url"] = model.LicenseUrl,
                ["agreed"] = now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            };
        }

        SettingsFile.Write(settings, Key, consents);
    }
}
