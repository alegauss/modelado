namespace Modelado.Inference.Tests;

/// <summary>MOD18: the provider About chooses is read back on the next start.</summary>
public sealed class ProviderSettingTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"modelado-settings-{Guid.NewGuid():N}");

    private string File => Path.Combine(folder, "Modelado", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_saved_choice_is_read_back_and_no_file_is_no_choice()
    {
        Assert.Null(ProviderSetting.Load(File));

        ProviderSetting.Save(File, ProviderChoice.Cpu);
        Assert.Equal(ProviderChoice.Cpu, ProviderSetting.Load(File));

        ProviderSetting.Save(File, ProviderChoice.DirectML);
        Assert.Equal(ProviderChoice.DirectML, ProviderSetting.Load(File));
    }

    [Fact]
    public void Saving_keeps_what_else_the_file_holds()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, """{ "theme": "dark", "provider": "auto" }""");

        ProviderSetting.Save(File, ProviderChoice.Cpu);

        string text = System.IO.File.ReadAllText(File);
        Assert.Contains("\"theme\": \"dark\"", text, StringComparison.Ordinal);
        Assert.Equal(ProviderChoice.Cpu, ProviderSetting.Load(File));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "provider": "gpu" }""")]
    [InlineData("""{ "provider": 3 }""")]
    [InlineData("[]")]
    public void A_file_that_does_not_read_is_no_choice(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, text);

        Assert.Null(ProviderSetting.Load(File));
    }

    [Fact]
    public void The_names_are_the_command_lines()
    {
        foreach (ProviderChoice choice in Enum.GetValues<ProviderChoice>())
        {
            Assert.Equal(choice, ProviderSetting.Parse(ProviderSetting.Name(choice)));
        }

        Assert.Equal(["auto", "dml", "cpu"], Enum.GetValues<ProviderChoice>().Select(ProviderSetting.Name));
    }
}
