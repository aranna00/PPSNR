using System.ComponentModel.DataAnnotations;

namespace PPSNR.Server.Data.Entities;

/// <summary>
/// Represents an external identity provider account linked to an ApplicationUser.
/// This design allows any number of external providers (Twitch, Google, YouTube, etc.)
/// to be linked without modifying the ApplicationUser entity.
/// </summary>
public class ExternalIdentity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The ApplicationUser who owns this external identity link.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string ApplicationUserId { get; set; } = null!;

    public ApplicationUser? ApplicationUser { get; set; }

    /// <summary>
    /// The name of the provider (e.g., "Twitch", "Google", "YouTube").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ProviderName { get; set; } = null!;

    /// <summary>
    /// The unique identifier from the external provider (e.g., Twitch user ID, Google sub, YouTube channel ID).
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string ProviderUserId { get; set; } = null!;

    /// <summary>
    /// Optional: The user's email from the provider (useful for matching during account linking).
    /// </summary>
    [MaxLength(256)]
    public string? ProviderEmail { get; set; }

    /// <summary>
    /// Optional: The user's display name from the provider.
    /// </summary>
    [MaxLength(256)]
    public string? ProviderDisplayName { get; set; }

    /// <summary>
    /// Optional: Avatar URL from the provider.
    /// </summary>
    [MaxLength(500)]
    public string? ProviderAvatarUrl { get; set; }

    /// <summary>
    /// When this external identity was linked.
    /// </summary>
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this external identity was last refreshed/verified.
    /// </summary>
    public DateTime? LastVerifiedAt { get; set; }

    /// <summary>
    /// Optional: Refresh token from provider (for providers that support it, like Google).
    /// Should be encrypted in a real application.
    /// </summary>
    [MaxLength(1000)]
    public string? RefreshToken { get; set; }
}

