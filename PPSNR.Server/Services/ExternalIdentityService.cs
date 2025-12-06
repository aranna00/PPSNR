using Microsoft.EntityFrameworkCore;
using PPSNR.Server.Data;
using PPSNR.Server.Data.Entities;

namespace PPSNR.Server.Services;

/// <summary>
/// Service for managing external identity links for ApplicationUsers.
/// Handles linking/unlinking external providers without modifying ApplicationUser directly.
/// </summary>
public class ExternalIdentityService
{
    private readonly ApplicationDbContext _db;

    public ExternalIdentityService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Links an external identity provider to an ApplicationUser.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID to link to.</param>
    /// <param name="providerName">The provider name (e.g., "Twitch", "Google").</param>
    /// <param name="providerInfo">The provider information to store.</param>
    /// <returns>The created or updated ExternalIdentity.</returns>
    public async Task<ExternalIdentity> LinkExternalIdentityAsync(
        string userId,
        string providerName,
        ExternalProviderInfo providerInfo)
    {
        // Check if this provider is already linked to this user
        var existing = await _db.ExternalIdentities
            .FirstOrDefaultAsync(ei =>
                ei.ApplicationUserId == userId &&
                ei.ProviderName == providerName);

        if (existing != null)
        {
            // Update existing link
            existing.ProviderUserId = providerInfo.ProviderUserId;
            existing.ProviderDisplayName = providerInfo.DisplayName;
            existing.ProviderEmail = providerInfo.Email;
            existing.ProviderAvatarUrl = providerInfo.AvatarUrl;
            existing.RefreshToken = providerInfo.RefreshToken;
            existing.LastVerifiedAt = DateTime.UtcNow;
        }
        else
        {
            // Create new link
            existing = new ExternalIdentity
            {
                ApplicationUserId = userId,
                ProviderName = providerName,
                ProviderUserId = providerInfo.ProviderUserId,
                ProviderDisplayName = providerInfo.DisplayName,
                ProviderEmail = providerInfo.Email,
                ProviderAvatarUrl = providerInfo.AvatarUrl,
                RefreshToken = providerInfo.RefreshToken,
                LinkedAt = DateTime.UtcNow,
                LastVerifiedAt = DateTime.UtcNow
            };
            _db.ExternalIdentities.Add(existing);
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Unlinks an external identity provider from an ApplicationUser.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="providerName">The provider name to unlink.</param>
    /// <returns>True if the provider was unlinked, false if it wasn't linked.</returns>
    public async Task<bool> UnlinkExternalIdentityAsync(string userId, string providerName)
    {
        var existing = await _db.ExternalIdentities
            .FirstOrDefaultAsync(ei =>
                ei.ApplicationUserId == userId &&
                ei.ProviderName == providerName);

        if (existing == null) return false;

        _db.ExternalIdentities.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Gets all external identities linked to a user.
    /// </summary>
    public async Task<List<ExternalIdentity>> GetLinkedIdentitiesAsync(string userId)
    {
        return await _db.ExternalIdentities
            .Where(ei => ei.ApplicationUserId == userId)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets a specific external identity for a user.
    /// </summary>
    public async Task<ExternalIdentity?> GetExternalIdentityAsync(string userId, string providerName)
    {
        return await _db.ExternalIdentities
            .FirstOrDefaultAsync(ei =>
                ei.ApplicationUserId == userId &&
                ei.ProviderName == providerName);
    }

    /// <summary>
    /// Finds a user by their external provider identity (e.g., Twitch ID).
    /// Useful for finding or auto-creating accounts during external login.
    /// </summary>
    /// <param name="providerName">The provider name.</param>
    /// <param name="providerUserId">The provider's user ID.</param>
    /// <returns>The ApplicationUser if found, null otherwise.</returns>
    public async Task<ApplicationUser?> FindUserByExternalIdentityAsync(string providerName, string providerUserId)
    {
        var externalId = await _db.ExternalIdentities
            .Where(ei => ei.ProviderName == providerName && ei.ProviderUserId == providerUserId)
            .FirstOrDefaultAsync();

        if (externalId == null) return null;

        return await _db.Users.FirstOrDefaultAsync(u => u.Id == externalId.ApplicationUserId);
    }

    /// <summary>
    /// Updates the verification timestamp for an external identity.
    /// Useful for tracking when provider info was last confirmed to be valid.
    /// </summary>
    public async Task<bool> UpdateLastVerifiedAsync(string userId, string providerName)
    {
        var existing = await _db.ExternalIdentities
            .FirstOrDefaultAsync(ei =>
                ei.ApplicationUserId == userId &&
                ei.ProviderName == providerName);

        if (existing == null) return false;

        existing.LastVerifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}

