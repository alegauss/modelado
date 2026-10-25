using System.Text.Json;
using System.Text.Json.Nodes;

namespace Modelado.Inference;

/// <summary>
/// The per-user settings.json the window's choices are kept in (MOD18, MOD30): one JSON object, each
/// setting under its own key, so writing one keeps the others.
/// </summary>
public static class SettingsFile
{
    /// <summary>
    /// %LOCALAPPDATA%\Modelado\settings.json. The variable is read first, so a launch that points it
    /// elsewhere finds its own file; Windows' known folder answers when it is not set.
    /// </summary>
    public static string DefaultPath =>
        Path.Combine(
            Environment.GetEnvironmentVariable("LOCALAPPDATA") is { Length: > 0 } local ? local : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Modelado",
            "settings.json");

    /// <summary>One setting's value.</summary>
    /// <param name="path">The settings file.</param>
    /// <param name="key">The setting.</param>
    /// <returns>Its value, or null when there is no file, no such key, or it does not read.</returns>
    public static JsonValue? Read(string path, string key)
    {
        try
        {
            return File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject settings ? settings[key] as JsonValue : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>One setting's object, e.g. MOD37's cloud job kept across a restart.</summary>
    /// <param name="path">The settings file.</param>
    /// <param name="key">The setting.</param>
    /// <returns>The object, or null when there is none or the file does not read.</returns>
    public static JsonObject? ReadObject(string path, string key)
    {
        try
        {
            return File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject settings ? settings[key] as JsonObject : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Forgets one setting, keeping the others.</summary>
    /// <param name="path">The settings file.</param>
    /// <param name="key">The setting.</param>
    public static void Remove(string path, string key)
    {
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject settings && settings.Remove(key))
            {
                File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (JsonException)
        {
            // A file that does not read holds nothing to forget.
        }
    }

    /// <summary>Writes one setting, keeping whatever else the file holds.</summary>
    /// <param name="path">The settings file; its folder is created.</param>
    /// <param name="key">The setting.</param>
    /// <param name="value">Its value.</param>
    public static void Write(string path, string key, JsonNode value)
    {
        JsonObject settings = new();
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject existing)
            {
                settings = existing;
            }
        }
        catch (JsonException)
        {
            // A file that does not read is replaced rather than kept.
        }

        settings[key] = value;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}

/// <summary>Whether the window looks for a newer release by itself (MOD30); on unless turned off in About.</summary>
public static class UpdateSetting
{
    private const string Key = "checkForUpdates";

    /// <summary>Whether the automatic check is on.</summary>
    /// <param name="path">The settings file.</param>
    /// <returns>False only where the file says so.</returns>
    public static bool Load(string path)
    {
        try
        {
            return SettingsFile.Read(path, Key)?.GetValue<bool>() ?? true;
        }
        catch (Exception error) when (error is InvalidOperationException or FormatException)
        {
            return true;
        }
    }

    /// <summary>Turns the automatic check on or off.</summary>
    /// <param name="path">The settings file.</param>
    /// <param name="on">On.</param>
    public static void Save(string path, bool on) => SettingsFile.Write(path, Key, JsonValue.Create(on));
}
