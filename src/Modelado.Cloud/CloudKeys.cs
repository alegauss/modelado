using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Modelado.Cloud;

/// <summary>
/// The users' API keys for cloud services (MOD36), one file per provider in a keys folder beside
/// settings.json. Each is encrypted with Windows DPAPI for the current user, with the provider's id
/// as extra entropy, so the file is unreadable as another Windows user, on another machine, or as
/// another provider's key. A key is never written in plain text, never logged and never put in an
/// exception message; what reaches the window is whether one is stored.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CloudKeys
{
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

    /// <summary>Whether a key is stored for a provider.</summary>
    /// <param name="providerId">The provider.</param>
    /// <returns>True when one is.</returns>
    public bool Has(string providerId) => File.Exists(PathOf(providerId));

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

    /// <summary>The key stored for a provider.</summary>
    /// <param name="providerId">The provider.</param>
    /// <returns>The key, or null where none is stored or the file cannot be decrypted by this user.</returns>
    public string? Load(string providerId)
    {
        string path = PathOf(providerId);
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
