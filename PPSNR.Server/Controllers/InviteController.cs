using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server.Data;

namespace PPSNR.Server.Controllers;

/// <summary>
/// Handles invite acceptance flow.
/// </summary>
[ApiController]
[Route("invite")]
public sealed class InviteController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public InviteController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("accept/{pairId:guid}/{token:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Accept(Guid pairId, Guid token)
    {
        // Require authentication; redirect to login if not
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            var returnUrl = $"/invite/accept/{pairId}/{token}";
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var link = await _db.PairLinks.AsNoTracking().FirstOrDefaultAsync(l => l.PairId == pairId);
        if (link == null || link.PartnerEditToken != token || (link.ExpiresAt != null && link.ExpiresAt <= DateTime.UtcNow))
        {
            return BadRequest(new { error = "Invalid or expired invite." });
        }

        var pair = await _db.Pairs.FirstOrDefaultAsync(p => p.Id == pairId);
        if (pair == null)
        {
            return NotFound();
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Bind the partner to the current user
        pair.PartnerUserId = userId;
        await _db.SaveChangesAsync();

        // Redirect to the partner edit page using the same token
        return Redirect($"/{pairId}/partner-edit/{token}");
    }
}
