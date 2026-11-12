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

/// <summary>
/// The language the window's text is in (MOD86): the one chosen in About, or Windows' display
/// language where none is. Only the interface follows it; numbers keep one format (MOD49).
/// </summary>
public static class LanguageSetting
{
    private const string Key = "language";

    /// <summary>The variable that overrides every other choice for one run, as the UI cases set it to "en".</summary>
    public const string Variable = "MODELADO_LANGUAGE";

    /// <summary>English, the text the app is written in and the fallback for any other.</summary>
    public const string English = "en";

    /// <summary>Every language the interface is meant to be translated into, English first (MOD87 to MOD91).</summary>
    public static IReadOnlyList<string> Planned { get; } = [English, "pt-BR", "de", "fr", "es", "zh-Hans"];

    /// <summary>The language chosen in About.</summary>
    /// <param name="path">The settings file.</param>
    /// <returns>Its name, e.g. pt-BR, or null to follow Windows.</returns>
    public static string? Load(string path)
    {
        try
        {
            return SettingsFile.Read(path, Key)?.GetValue<string>() is { Length: > 0 } name ? name : null;
        }
        catch (Exception error) when (error is InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Keeps the choice, or forgets it to follow Windows again.</summary>
    /// <param name="path">The settings file.</param>
    /// <param name="language">A language's name, or null for Windows' own.</param>
    public static void Save(string path, string? language)
    {
        if (language is null)
        {
            SettingsFile.Remove(path, Key);
        }
        else
        {
            SettingsFile.Write(path, Key, JsonValue.Create(language));
        }
    }

    /// <summary>
    /// The language the interface shows: the variable's, then the one chosen, then Windows' display
    /// language, each only where a translation into it is available, matched by name and then by
    /// its parents (zh-CN finds zh-Hans, pt-PT finds pt-BR); English when none is.
    /// </summary>
    /// <param name="pinned">The variable's value, or null.</param>
    /// <param name="chosen">The language chosen in About, or null.</param>
    /// <param name="windows">Windows' display language.</param>
    /// <param name="available">The languages a translation exists for; English is always one.</param>
    /// <returns>The language's name.</returns>
    public static string Resolve(string? pinned, string? chosen, System.Globalization.CultureInfo windows, IReadOnlyCollection<string> available)
    {
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(available);
        foreach (string? asked in (string?[])[pinned, chosen])
        {
            if (asked is not null && available.FirstOrDefault(name => string.Equals(name, asked, StringComparison.OrdinalIgnoreCase)) is { } exact)
            {
                return exact;
            }
        }

        for (System.Globalization.CultureInfo culture = windows; culture.Name.Length > 0; culture = culture.Parent)
        {
            if (available.FirstOrDefault(name => Related(name, culture.Name)) is { } match)
            {
                return match;
            }
        }

        return English;
    }

    // pt-PT and pt-BR are one language to a reader who has no other; zh-Hans and zh-CN too.
    private static bool Related(string available, string windows) =>
        string.Equals(available, windows, StringComparison.OrdinalIgnoreCase)
        || string.Equals(System.Globalization.CultureInfo.GetCultureInfo(available).TwoLetterISOLanguageName, windows, StringComparison.OrdinalIgnoreCase);
}
