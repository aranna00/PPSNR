using System.Security.Claims;

namespace PPSNR.Server.Services;

/// <summary>
/// Base class for external identity provider implementations.
/// Provides common functionality for extracting claims and provider info.
/// </summary>
public abstract class ExternalIdentityProviderBase : IExternalIdentityProvider
{
    public abstract string ProviderName { get; }
    public abstract string SchemeName { get; }

    public virtual async Task<ExternalProviderInfo?> ExtractProviderInfoAsync(HttpContext httpContext)
    {
        var user = httpContext.User;
        if (!user.Identity?.IsAuthenticated ?? true)
            return null;

        var providerUserId = ExtractProviderUserId(user);
        if (string.IsNullOrEmpty(providerUserId))
            return null;

        return new ExternalProviderInfo
        {
            ProviderUserId = providerUserId,
            DisplayName = ExtractDisplayName(user),
            Email = ExtractEmail(user),
            AvatarUrl = ExtractAvatarUrl(user),
            RefreshToken = ExtractRefreshToken(user)
        };
    }

    public abstract bool IsConfigured();

    /// <summary>
    /// Extracts the provider's unique user ID from claims.
    /// Override this for provider-specific claim types.
    /// </summary>
    protected virtual string? ExtractProviderUserId(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Extracts the display name from claims.
    /// Override this for provider-specific claim types.
    /// </summary>
    protected virtual string? ExtractDisplayName(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value;
    }

    /// <summary>
    /// Extracts the email from claims.
    /// Override this for provider-specific claim types.
    /// </summary>
    protected virtual string? ExtractEmail(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Extracts the avatar URL from claims.
    /// Override this for provider-specific claim types.
    /// </summary>
    protected virtual string? ExtractAvatarUrl(ClaimsPrincipal user)
    {
        // Not all providers include picture claims; override as needed
        return null;
    }

    /// <summary>
    /// Extracts refresh token from claims.
    /// Override this for providers that support refresh tokens.
    /// </summary>
    protected virtual string? ExtractRefreshToken(ClaimsPrincipal user)
    {
        // Not all providers include refresh tokens in claims; may need to access other sources
        return null;
    }

    /// <summary>
    /// Extracts provider information from a ClaimsPrincipal directly.
    /// </summary>
    public virtual ExternalProviderInfo? ExtractProviderInfoFromPrincipal(ClaimsPrincipal principal)
    {
        if (!principal.Identity?.IsAuthenticated ?? true)
            return null;

        var providerUserId = ExtractProviderUserId(principal);
        if (string.IsNullOrEmpty(providerUserId))
            return null;

        return new ExternalProviderInfo
        {
            ProviderUserId = providerUserId,
            DisplayName = ExtractDisplayName(principal),
            Email = ExtractEmail(principal),
            AvatarUrl = ExtractAvatarUrl(principal),
            RefreshToken = ExtractRefreshToken(principal)
        };
    }
}
