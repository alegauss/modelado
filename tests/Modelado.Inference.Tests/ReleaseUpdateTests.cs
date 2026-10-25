using System.Net;
using System.Security.Cryptography;
using Modelado.Inference;

namespace Modelado.Inference.Tests;

/// <summary>MOD31: an update's installer runs only when it matches its line in SHA256SUMS.</summary>
public sealed class ReleaseUpdateTests : IDisposable
{
    private static readonly byte[] Installer = [.. Enumerable.Range(0, 4096).Select(i => (byte)(i * 7))];
    private static readonly string Digest = Convert.ToHexStringLower(SHA256.HashData(Installer));
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"modelado-update-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static AvailableRelease Release(long bytes) => new(
        new Version(0, 3, 0), "v0.3.0", "Modelado-Setup-0.3.0.exe",
        "https://example.test/Modelado-Setup-0.3.0.exe", "https://example.test/SHA256SUMS", null, bytes);

    private sealed class Serving(string sums, byte[] installer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS", StringComparison.Ordinal)
                    ? new StringContent(sums)
                    : new ByteArrayContent(installer),
            });
    }

    [Theory]
    [InlineData("{0}  Modelado-Setup-0.3.0.exe\n")]
    [InlineData("{0} *Modelado-Setup-0.3.0.exe\r\n")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000  other.exe\n{0}  Modelado-Setup-0.3.0.exe")]
    public void A_sums_line_names_the_digest(string sums)
    {
        Assert.Equal(Digest, ReleaseUpdate.SumFor(string.Format(System.Globalization.CultureInfo.InvariantCulture, sums, Digest.ToUpperInvariant()), "Modelado-Setup-0.3.0.exe"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc  Modelado-Setup-0.3.0.exe")]
    [InlineData("{0}  Modelado-Setup-0.3.1.exe")]
    [InlineData("{0}Modelado-Setup-0.3.0.exe")]
    public void A_sums_text_without_a_well_formed_line_names_nothing(string sums)
    {
        Assert.Null(ReleaseUpdate.SumFor(string.Format(System.Globalization.CultureInfo.InvariantCulture, sums, Digest), "Modelado-Setup-0.3.0.exe"));
    }

    [Fact]
    public async Task An_installer_matching_its_sums_line_is_kept()
    {
        using HttpClient http = new(new Serving($"{Digest}  Modelado-Setup-0.3.0.exe\n", Installer));

        string path = await ReleaseUpdate.DownloadAsync(http, Release(Installer.Length), folder, null, TestContext.Current.CancellationToken);

        Assert.Equal(Installer, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_tampered_installer_is_refused_and_deleted()
    {
        byte[] tampered = [.. Installer];
        tampered[100] ^= 0xFF;
        using HttpClient http = new(new Serving($"{Digest}  Modelado-Setup-0.3.0.exe\n", tampered));

        await Assert.ThrowsAsync<InvalidDataException>(() => ReleaseUpdate.DownloadAsync(http, Release(Installer.Length), folder, null, TestContext.Current.CancellationToken));

        Assert.Empty(Directory.Exists(folder) ? Directory.GetFiles(folder) : []);
    }

    [Fact]
    public async Task A_release_whose_sums_do_not_name_the_installer_downloads_nothing()
    {
        using HttpClient http = new(new Serving($"{Digest}  something-else.exe\n", Installer));

        InvalidDataException refused = await Assert.ThrowsAsync<InvalidDataException>(() => ReleaseUpdate.DownloadAsync(http, Release(Installer.Length), folder, null, TestContext.Current.CancellationToken));

        Assert.Contains("no line for Modelado-Setup-0.3.0.exe", refused.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void The_installer_runs_silently_and_relaunches()
    {
        Assert.Equal("/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /relaunch=1", ReleaseUpdate.InstallerStart("setup.exe").Arguments);
    }
}
