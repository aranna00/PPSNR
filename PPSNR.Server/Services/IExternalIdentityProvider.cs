namespace PPSNR.Server.Services;

/// <summary>
/// Interface for managing external identity provider integrations.
/// Implementations handle provider-specific logic for Twitch, Google, YouTube, etc.
/// </summary>
public interface IExternalIdentityProvider
{
    /// <summary>
    /// The name of the provider (e.g., "Twitch", "Google", "YouTube").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// The authentication scheme name for this provider.
    /// </summary>
    string SchemeName { get; }

    /// <summary>
    /// Extracts provider information from the current HTTP context's claims.
    /// </summary>
    /// <param name="httpContext">The current HTTP context after external login.</param>
    /// <returns>Provider information if successfully extracted, null otherwise.</returns>
    Task<ExternalProviderInfo?> ExtractProviderInfoAsync(HttpContext httpContext);

    /// <summary>
    /// Validates that the provider is properly configured.
    /// </summary>
    /// <returns>True if the provider can be used, false otherwise.</returns>
    bool IsConfigured();
}

