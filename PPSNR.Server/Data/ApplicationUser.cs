using Microsoft.AspNetCore.Identity;
using PPSNR.Server.Data.Entities;

namespace PPSNR.Server.Data;

/// <summary>
/// ApplicationUser extended with relationships to external identities and streamer profiles.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// External identity providers (Twitch, Google, YouTube, etc.) linked to this user.
    /// </summary>
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; } = new List<ExternalIdentity>();

    /// <summary>
    /// Streamer profiles owned by this user.
    /// </summary>
    public ICollection<Streamer> Streamers { get; set; } = new List<Streamer>();
}
