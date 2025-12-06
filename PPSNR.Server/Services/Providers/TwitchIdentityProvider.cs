using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace PPSNR.Server.Services;

/// <summary>
/// External identity provider for Twitch.
/// </summary>
public class TwitchIdentityProvider : ExternalIdentityProviderBase
{
    private readonly IConfiguration _config;

    public override string ProviderName => "Twitch";
    public override string SchemeName => "Twitch";

    public TwitchIdentityProvider(IConfiguration config)
    {
        _config = config;
    }

    public override bool IsConfigured()
    {
        var clientId = _config["TWITCH_CLIENT_ID"] ?? _config["Authentication:Twitch:ClientId"];
        var clientSecret = _config["TWITCH_CLIENT_SECRET"] ?? _config["Authentication:Twitch:ClientSecret"];
        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
    }

    /// <summary>
    /// Twitch uses NameIdentifier for the user ID.
    /// </summary>
    protected override string? ExtractProviderUserId(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Twitch provides display name in the Name claim.
    /// </summary>
    protected override string? ExtractDisplayName(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value;
    }

    /// <summary>
    /// Twitch provides email in the Email claim.
    /// </summary>
    protected override string? ExtractEmail(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Twitch provides picture URL in a custom claim.
    /// </summary>
    protected override string? ExtractAvatarUrl(ClaimsPrincipal user)
    {
        // Twitch uses "picture" claim for avatar
        return user.FindFirst("picture")?.Value;
    }
}

