namespace Modelado.Inference.Tests;

/// <summary>MOD94: the files opened or saved lately, newest first, kept in settings.json.</summary>
public sealed class RecentFilesTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"modelado-recent-{Guid.NewGuid():N}");

    private string File => Path.Combine(folder, "Modelado", "settings.json");

    private string Named(string name) => Path.Combine(folder, name);

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void The_newest_comes_first_and_one_opened_again_moves_up_rather_than_twice()
    {
        Assert.Empty(RecentFiles.Load(File));

        RecentFiles.Add(File, Named("a.png"));
        RecentFiles.Add(File, Named("b.obj"));
        RecentFiles.Add(File, Named("A.PNG"));

        Assert.Equal([Named("A.PNG"), Named("b.obj")], RecentFiles.Load(File));
    }

    [Fact]
    public void Only_the_last_ten_are_kept()
    {
        for (int i = 0; i < 12; i++)
        {
            RecentFiles.Add(File, Named($"{i}.png"));
        }

        IReadOnlyList<string> kept = RecentFiles.Load(File);
        Assert.Equal(RecentFiles.Kept, kept.Count);
        Assert.Equal(Named("11.png"), kept[0]);
        Assert.Equal(Named("2.png"), kept[^1]);
    }

    [Fact]
    public void Removing_one_keeps_the_rest_and_the_other_settings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, """{ "provider": "cpu" }""");
        RecentFiles.Add(File, Named("a.png"));
        RecentFiles.Add(File, Named("b.obj"));

        RecentFiles.Remove(File, Named("a.png"));

        Assert.Equal([Named("b.obj")], RecentFiles.Load(File));
        Assert.Equal(ProviderChoice.Cpu, ProviderSetting.Load(File));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "recentFiles": "a.png" }""")]
    [InlineData("""{ "recentFiles": [3, "", null] }""")]
    public void A_list_that_does_not_read_is_empty(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, text);

        Assert.Empty(RecentFiles.Load(File));
    }
}
