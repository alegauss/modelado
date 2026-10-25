namespace Modelado.Cloud;

/// <summary>
/// The services this build knows (MOD36): Meshy AI first (MOD37); each later one is a line here
/// and a class behind <see cref="ICloudProvider"/>. With none holding a stored key, nothing in the
/// app offers a cloud action or reaches the network.
/// </summary>
public static class CloudProviders
{
    // One client for every service, as HttpClient is meant to be shared; a job polls, so no single
    // request needs long.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(100) };

    /// <summary>Every registered service.</summary>
    public static IReadOnlyList<ICloudProvider> All { get; } = Build(null);

    /// <summary>
    /// Every registered service, each over the API root an override names: the app's
    /// --cloud-api id=url, a stand-in the way --manifest and --releases are.
    /// </summary>
    /// <param name="apis">API roots by provider id; the services' own where none is named.</param>
    /// <returns>The services.</returns>
    public static IReadOnlyList<ICloudProvider> Build(IReadOnlyDictionary<string, Uri>? apis) =>
        [new Providers.MeshyProvider(Http, apis?.GetValueOrDefault("meshy"))];

    /// <summary>The registered services that can do an action.</summary>
    /// <param name="action">The action.</param>
    /// <param name="among">The services to look in; <see cref="All"/> when null.</param>
    /// <returns>Those that can.</returns>
    public static IReadOnlyList<ICloudProvider> For(CloudCapability action, IEnumerable<ICloudProvider>? among = null) =>
        [.. (among ?? All).Where(p => action != CloudCapability.None && (p.Capabilities & action) == action)];
}
