namespace PPSNR.Server.Services;

/// <summary>
/// Registry for managing external identity providers.
/// Allows registration, lookup, and enumeration of available providers.
/// </summary>
public class ExternalIdentityProviderRegistry
{
    private readonly Dictionary<string, IExternalIdentityProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ExternalIdentityProviderRegistry(params IExternalIdentityProvider[] providers)
    {
        foreach (var provider in providers)
        {
            Register(provider);
        }
    }

    /// <summary>
    /// Registers a provider.
    /// </summary>
    public void Register(IExternalIdentityProvider provider)
    {
        _providers[provider.ProviderName] = provider;
    }

    /// <summary>
    /// Gets a provider by name.
    /// </summary>
    public IExternalIdentityProvider? GetProvider(string providerName)
    {
        return _providers.TryGetValue(providerName, out var provider) ? provider : null;
    }

    /// <summary>
    /// Gets a provider by scheme name.
    /// </summary>
    public IExternalIdentityProvider? GetProviderByScheme(string schemeName)
    {
        return _providers.Values.FirstOrDefault(p => p.SchemeName == schemeName);
    }

    /// <summary>
    /// Gets all available providers.
    /// </summary>
    public IEnumerable<IExternalIdentityProvider> GetAllProviders()
    {
        return _providers.Values;
    }

    /// <summary>
    /// Gets all configured providers (those ready to use).
    /// </summary>
    public IEnumerable<IExternalIdentityProvider> GetConfiguredProviders()
    {
        return _providers.Values.Where(p => p.IsConfigured());
    }

    /// <summary>
    /// Checks if a provider is registered.
    /// </summary>
    public bool Has(string providerName)
    {
        return _providers.ContainsKey(providerName);
    }
}

