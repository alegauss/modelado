using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Modelado.Cloud;

/// <summary>
/// The users' API keys for cloud services (MOD36), one file per provider in a keys folder beside
/// settings.json. Each is encrypted with Windows DPAPI for the current user, with the provider's id
/// as extra entropy, so the file is unreadable as another Windows user, on another machine, or as
/// another provider's key. A key is never written in plain text, never logged and never put in an
/// exception message; what reaches the window is where one comes from and its last four characters.
/// MOD96: a key set in the environment (MESHY_API_KEY, IDEOGRAM_API_KEY: the provider's id in
/// capitals) is used before a stored one, by the app and the CLI alike.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CloudKeys
{
    /// <summary>The environment variable a provider's key can be set in.</summary>
    /// <param name="providerId">The provider.</param>
    /// <returns>e.g. MESHY_API_KEY.</returns>
    public static string Variable(string providerId) => $"{providerId.ToUpperInvariant()}_API_KEY";

    /// <summary>Where the key used for a provider comes from.</summary>
    /// <param name="providerId">The provider.</param>
    /// <returns>The environment first, then the stored file, else none.</returns>
    public KeySource Source(string providerId)
    {
        if (FromEnvironment(providerId) is not null)
        {
            return KeySource.Environment;
        }

        return File.Exists(PathOf(providerId)) ? KeySource.Stored : KeySource.None;
    }

    /// <summary>The key's last four characters, enough to tell two keys apart and no more.</summary>
    /// <param name="providerId">The provider.</param>
    /// <returns>e.g. "…a1b2", "…" for a key too short to show any of, or null with no key.</returns>
    public string? Hint(string providerId)
    {
        if (Load(providerId) is not { } key)
        {
            return null;
        }

        // A short key would be half shown by its last four; it is only said to be there.
        return key.Length >= 12 ? $"…{key[^4..]}" : "…";
    }

    private static string? FromEnvironment(string providerId) =>
        Environment.GetEnvironmentVariable(Variable(providerId))?.Trim() is { Length: > 0 } key ? key : null;
    private readonly string folder;

    /// <summary>A store in a folder.</summary>
    /// <param name="folder">Where the encrypted keys are kept.</param>
    public CloudKeys(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        this.folder = folder;
    }

    /// <summary>%LOCALAPPDATA%\Modelado\keys, beside settings.json.</summary>
    public static string DefaultFolder =>
        Path.Combine(
            Environment.GetEnvironmentVariable("LOCALAPPDATA") is { Length: > 0 } local ? local : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Modelado",
            "keys");

    /// <summary>Whether a key is there for a provider, set in the environment or stored.</summary>
    /// <param name="providerId">The provider.</param>
    /// <returns>True when one is.</returns>
    public bool Has(string providerId) => Source(providerId) != KeySource.None;

    /// <summary>Stores a key, replacing any earlier one.</summary>
    /// <param name="providerId">The provider.</param>
    /// <param name="key">The key.</param>
    public void Save(string providerId, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Directory.CreateDirectory(folder);
        byte[] plain = Encoding.UTF8.GetBytes(key.Trim());
        try
        {
            File.WriteAllBytes(PathOf(providerId), ProtectedData.Protect(plain, Entropy(providerId), DataProtectionScope.CurrentUser));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>The key used for a provider: the environment's, else the stored one.</summary>
    /// <param name="providerId">The provider.</param>
    /// <returns>The key, or null where none is set or stored, or the file cannot be decrypted by this user.</returns>
    public string? Load(string providerId)
    {
        string path = PathOf(providerId);
        if (FromEnvironment(providerId) is { } set)
        {
            return set;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy(providerId), DataProtectionScope.CurrentUser));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Forgets a provider's key.</summary>
    /// <param name="providerId">The provider.</param>
    public void Remove(string providerId) => File.Delete(PathOf(providerId));

    private string PathOf(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        if (providerId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || providerId.Contains('.', StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{providerId}' is not a provider id.", nameof(providerId));
        }

        return Path.Combine(folder, providerId + ".key");
    }

    private static byte[] Entropy(string providerId) => Encoding.UTF8.GetBytes($"Modelado.Cloud:{providerId}");
}

/// <summary>Where a provider's key comes from (MOD96).</summary>
public enum KeySource
{
    /// <summary>No key: neither set in the environment nor stored.</summary>
    None,

    /// <summary>Set in the provider's environment variable, which wins over a stored key.</summary>
    Environment,

    /// <summary>Stored by Modelado, encrypted for this Windows user.</summary>
    Stored,
}
