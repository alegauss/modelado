using System.Text.Json.Nodes;

namespace Modelado.Inference;

/// <summary>
/// Where inference runs as the window last chose it (MOD18), kept per user in settings.json beside
/// the downloads. The names are the CLI's own: auto, dml and cpu.
/// </summary>
public static class ProviderSetting
{
    private const string Key = "provider";

    /// <summary>%LOCALAPPDATA%\Modelado\settings.json, <see cref="SettingsFile.DefaultPath"/>.</summary>
    public static string DefaultPath => SettingsFile.DefaultPath;

    /// <summary>The choice the file holds.</summary>
    /// <param name="path">The settings file.</param>
    /// <returns>The choice, or null when there is no file, no choice in it, or it does not read.</returns>
    public static ProviderChoice? Load(string path)
    {
        try
        {
            return SettingsFile.Read(path, Key) is { } value ? Parse(value.GetValue<string>()) : null;
        }
        catch (Exception error) when (error is InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Writes the choice, keeping whatever else the file holds.</summary>
    /// <param name="path">The settings file; its folder is created.</param>
    /// <param name="choice">Where inference should run.</param>
    public static void Save(string path, ProviderChoice choice) => SettingsFile.Write(path, Key, JsonValue.Create(Name(choice)));

    /// <summary>The CLI's name for a choice.</summary>
    /// <param name="choice">The choice.</param>
    /// <returns>auto, dml or cpu.</returns>
    public static string Name(ProviderChoice choice) => choice switch
    {
        ProviderChoice.DirectML => "dml",
        ProviderChoice.Cpu => "cpu",
        _ => "auto",
    };

    /// <summary>A choice from the CLI's name for it.</summary>
    /// <param name="name">auto, dml or cpu.</param>
    /// <returns>The choice, or null for any other name.</returns>
    public static ProviderChoice? Parse(string? name) => name switch
    {
        "auto" => ProviderChoice.Auto,
        "dml" => ProviderChoice.DirectML,
        "cpu" => ProviderChoice.Cpu,
        _ => null,
    };
}
