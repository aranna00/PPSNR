namespace PPSNR.Server.Services;

/// <summary>
/// Represents information about an external identity provider account.
/// </summary>
public class ExternalProviderInfo
{
    /// <summary>
    /// The unique identifier from the provider.
    /// </summary>
    public required string ProviderUserId { get; set; }

    /// <summary>
    /// The provider's display name for the user.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The provider's email for the user (if available).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The provider's avatar URL for the user (if available).
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Optional: Refresh token from provider (e.g., Google, YouTube).
    /// </summary>
    public string? RefreshToken { get; set; }
}

