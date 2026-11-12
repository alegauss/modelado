using System.Globalization;

namespace Modelado.Inference.Tests;

/// <summary>MOD86: the interface language chosen in About, and the one Windows' display language gives.</summary>
public sealed class LanguageSettingTests : IDisposable
{
    private static readonly string[] All = [.. LanguageSetting.Planned];

    private readonly string folder = Path.Combine(Path.GetTempPath(), $"modelado-language-{Guid.NewGuid():N}");

    private string File => Path.Combine(folder, "Modelado", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_saved_language_is_read_back_and_forgetting_it_follows_Windows_again()
    {
        Assert.Null(LanguageSetting.Load(File));

        LanguageSetting.Save(File, "pt-BR");
        Assert.Equal("pt-BR", LanguageSetting.Load(File));

        LanguageSetting.Save(File, null);
        Assert.Null(LanguageSetting.Load(File));
    }

    [Theory]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("de-AT", "de")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("fr-CA", "fr")]
    [InlineData("en-GB", "en")]
    [InlineData("ja-JP", "en")]
    public void Windows_display_language_finds_its_translation_or_English(string windows, string shown) =>
        Assert.Equal(shown, LanguageSetting.Resolve(null, null, CultureInfo.GetCultureInfo(windows), All));

    [Fact]
    public void A_language_with_no_translation_yet_is_English()
    {
        Assert.Equal("en", LanguageSetting.Resolve(null, null, CultureInfo.GetCultureInfo("de-DE"), ["en"]));
        Assert.Equal("en", LanguageSetting.Resolve(null, "de", CultureInfo.GetCultureInfo("en-US"), ["en"]));
    }

    [Fact]
    public void The_choice_beats_Windows_and_the_variable_beats_both()
    {
        CultureInfo german = CultureInfo.GetCultureInfo("de-DE");

        Assert.Equal("fr", LanguageSetting.Resolve(null, "fr", german, All));
        Assert.Equal("en", LanguageSetting.Resolve("en", "fr", german, All));
    }

    [Fact]
    public void English_is_planned_first()
    {
        Assert.Equal(LanguageSetting.English, LanguageSetting.Planned[0]);
    }
}
