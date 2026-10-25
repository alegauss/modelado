using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>MOD30: what a release description offers, read without the network.</summary>
public sealed class ReleaseCheckTests
{
    private static readonly Version Running = new(0, 2, 0);

    private static string Feed(string tag, params string[] assets) =>
        $$"""
        {
          "tag_name": "{{tag}}",
          "html_url": "https://github.com/alegauss/modelado/releases/tag/{{tag}}",
          "assets": [{{string.Join(",", assets.Select(a => $$"""{"name": "{{a}}", "size": 1000, "browser_download_url": "https://example.test/{{a}}"}"""))}}]
        }
        """;

    private sealed class Answering(string body) : IReleaseFeed
    {
        public Task<string> LatestAsync(CancellationToken cancellation) => Task.FromResult(body);
    }

    private sealed class Failing : IReleaseFeed
    {
        public Task<string> LatestAsync(CancellationToken cancellation) => throw new HttpRequestException("offline");
    }

    [Fact]
    public void A_newer_release_offers_its_installer_and_sums()
    {
        AvailableRelease? release = ReleaseCheck.NewerThan(Feed("v0.3.0", "Modelado-Setup-0.3.0.exe", "SHA256SUMS"), Running);

        Assert.NotNull(release);
        Assert.Equal(new Version(0, 3, 0), release.Version);
        Assert.Equal("Modelado-Setup-0.3.0.exe", release.InstallerName);
        Assert.Equal("https://example.test/Modelado-Setup-0.3.0.exe", release.InstallerUrl);
        Assert.Equal("https://example.test/SHA256SUMS", release.SumsUrl);
        Assert.Equal("https://github.com/alegauss/modelado/releases/tag/v0.3.0", release.PageUrl);
    }

    [Theory]
    [InlineData("v0.2.0")]
    [InlineData("v0.1.9")]
    public void The_same_or_an_older_release_offers_nothing(string tag)
    {
        Assert.Null(ReleaseCheck.NewerThan(Feed(tag, $"Modelado-Setup-{tag[1..]}.exe", "SHA256SUMS"), Running));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"message": "Not Found"}""")]
    [InlineData("""{"tag_name": "nightly", "assets": []}""")]
    [InlineData("""{"tag_name": "v9.0.0", "assets": "none"}""")]
    public void A_malformed_feed_offers_nothing(string json)
    {
        Assert.Null(ReleaseCheck.NewerThan(json, Running));
    }

    [Fact]
    public void Two_installers_offer_nothing_rather_than_a_guess()
    {
        Assert.Null(ReleaseCheck.NewerThan(Feed("v0.3.0", "Modelado-Setup-0.3.0.exe", "Modelado-Setup-0.3.1.exe", "SHA256SUMS"), Running));
    }

    [Fact]
    public void An_installer_without_its_sums_offers_nothing()
    {
        Assert.Null(ReleaseCheck.NewerThan(Feed("v0.3.0", "Modelado-Setup-0.3.0.exe"), Running));
    }

    [Fact]
    public void An_installer_of_no_stated_size_offers_nothing()
    {
        // MOD31: the download is held to the size the release states.
        string json = Feed("v0.3.0", "Modelado-Setup-0.3.0.exe", "SHA256SUMS").Replace("\"size\": 1000, ", "", StringComparison.Ordinal);

        Assert.Null(ReleaseCheck.NewerThan(json, Running));
    }

    [Fact]
    public void Other_assets_are_ignored()
    {
        Assert.NotNull(ReleaseCheck.NewerThan(Feed("v0.3.0", "notes.txt", "Modelado-Setup-notes.exe", "Modelado-Setup-0.3.0.exe", "SHA256SUMS"), Running));
    }

    [Fact]
    public async Task A_feed_that_fails_is_no_newer_release_and_not_reached()
    {
        ReleaseAnswer answer = await new ReleaseCheck(new Failing(), Running).AskAsync(TestContext.Current.CancellationToken);

        Assert.Null(answer.Newer);
        Assert.False(answer.Reached);
    }

    [Fact]
    public async Task A_feed_that_answers_with_the_same_release_is_reached_with_nothing_newer()
    {
        ReleaseAnswer answer = await new ReleaseCheck(new Answering(Feed("v0.2.0", "Modelado-Setup-0.2.0.exe", "SHA256SUMS")), Running).AskAsync(TestContext.Current.CancellationToken);

        Assert.Null(answer.Newer);
        Assert.True(answer.Reached);
    }

    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData(" V2 ", 2, 0, 0)]
    public void A_tag_reads_as_three_parts(string tag, int major, int minor, int build)
    {
        Assert.True(ReleaseCheck.TryParseVersion(tag, out Version? version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Fact]
    public void The_automatic_check_is_on_until_turned_off_and_keeps_the_provider()
    {
        string path = Path.Combine(Path.GetTempPath(), $"modelado-settings-{Guid.NewGuid():N}.json");
        try
        {
            Assert.True(UpdateSetting.Load(path));
            ProviderSetting.Save(path, ProviderChoice.Cpu);
            UpdateSetting.Save(path, false);

            Assert.False(UpdateSetting.Load(path));
            Assert.Equal(ProviderChoice.Cpu, ProviderSetting.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
