using System.Runtime.Versioning;
using System.Text;

namespace Modelado.Cloud.Tests;

/// <summary>
/// MOD36: a stored key is DPAPI-encrypted for this Windows user. Another user cannot be signed in
/// from a test, so what is held here is what that rests on: the file never holds the key, this user
/// reads it back, and the bytes do not decrypt under another provider's entropy.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CloudKeysTests : IDisposable
{
    private const string Secret = "msy_live_0123456789abcdef";
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"modelado-keys-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_key_is_stored_encrypted_and_read_back_by_this_user()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI is Windows'");
        CloudKeys keys = new(folder);

        keys.Save("meshy", $"  {Secret} ");

        Assert.True(keys.Has("meshy"));
        Assert.Equal(Secret, keys.Load("meshy"));
        byte[] stored = File.ReadAllBytes(Path.Combine(folder, "meshy.key"));
        Assert.DoesNotContain(Secret, Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, Encoding.Unicode.GetString(stored), StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_file_does_not_decrypt_as_another_providers()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI is Windows'");
        CloudKeys keys = new(folder);
        keys.Save("meshy", Secret);

        File.Copy(Path.Combine(folder, "meshy.key"), Path.Combine(folder, "ideogram.key"));

        Assert.Null(keys.Load("ideogram"));
    }

    [Fact]
    public void A_removed_key_is_gone()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI is Windows'");
        CloudKeys keys = new(folder);
        keys.Save("meshy", Secret);

        keys.Remove("meshy");

        Assert.False(keys.Has("meshy"));
        Assert.Null(keys.Load("meshy"));
    }

    [Fact]
    public void A_key_in_the_environment_wins_over_a_stored_one_and_is_never_stored()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI is Windows'");
        // A provider id of this test's own, so its variable is set nowhere else.
        string id = $"t{Guid.NewGuid():N}"[..12];
        CloudKeys keys = new(folder);
        Assert.Equal(KeySource.None, keys.Source(id));
        Assert.Null(keys.Hint(id));

        keys.Save(id, Secret);
        Assert.Equal(KeySource.Stored, keys.Source(id));
        Assert.Equal("…cdef", keys.Hint(id));

        Environment.SetEnvironmentVariable(CloudKeys.Variable(id), " env_key_000000000a1b2 ");
        try
        {
            Assert.Equal(KeySource.Environment, keys.Source(id));
            Assert.Equal("env_key_000000000a1b2", keys.Load(id));
            Assert.Equal("…a1b2", keys.Hint(id));

            keys.Remove(id);
            Assert.True(keys.Has(id));
            Assert.False(File.Exists(Path.Combine(folder, id + ".key")));
        }
        finally
        {
            Environment.SetEnvironmentVariable(CloudKeys.Variable(id), null);
        }

        Assert.Equal(KeySource.None, keys.Source(id));
    }

    [Theory]
    [InlineData("meshy", "MESHY_API_KEY")]
    [InlineData("ideogram", "IDEOGRAM_API_KEY")]
    public void The_variable_is_the_providers_id_in_capitals(string id, string variable) =>
        Assert.Equal(variable, CloudKeys.Variable(id));

    [Theory]
    [InlineData("../meshy")]
    [InlineData("meshy.key")]
    [InlineData("")]
    public void A_provider_id_cannot_name_another_file(string id)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI is Windows'");
        Assert.ThrowsAny<ArgumentException>(() => new CloudKeys(folder).Has(id));
    }
}
