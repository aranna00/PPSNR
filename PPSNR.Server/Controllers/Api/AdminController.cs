using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server.Data;
using PPSNR.Server.Data.Entities;
using System.Security.Claims;

namespace PPSNR.Server.Controllers.Api;

[ApiController]
[Route("api/admin")] 
[Authorize]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AdminController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpDelete("pairs/{pairId:guid}")]
    // See comment on CreateSamplePair about AllowAnonymous; we manually enforce auth to avoid redirects on XHR
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeletePair(Guid pairId, [FromServices] Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch
        {
            return BadRequest();
        }

        var pair = await _db.Pairs.FirstOrDefaultAsync(p => p.Id == pairId);
        if (pair == null) return NotFound();
        if (!string.Equals(pair.OwnerUserId, userId, StringComparison.Ordinal)) return Forbid();

        _db.Pairs.Remove(pair);
        await _db.SaveChangesAsync();
        return Ok(new { deleted = pairId });
    }

    [HttpGet("pairs")] 
    public async Task<IActionResult> GetPairs()
    {
        var pairs = await _db.Pairs.AsNoTracking().ToListAsync();
        var links = await _db.PairLinks.AsNoTracking().ToListAsync();
        var result = pairs.Select(p => new
        {
            p.Id,
            p.Name,
            p.CreatedAt,
            Link = links.FirstOrDefault(l => l.PairId == p.Id)
        });
        return Ok(result);
    }

    [HttpPost("pairs")]
    // AllowAnonymous here to avoid the default authentication challenge (redirect to Twitch)
    // for XHR/fetch requests. We still manually check the authenticated user and return 401
    // instead of causing a cross-site redirect that results in "TypeError: Failed to fetch" in the browser.
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreateSamplePair([FromServices] Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch
        {
            return BadRequest();
        }

        var pair = new StreamerPair { Name = $"Pair {DateTime.UtcNow:HHmmss}", OwnerUserId = userId, };
        _db.Pairs.Add(pair);
        var s1 = new Streamer { DisplayName = "Streamer A", };
        var s2 = new Streamer { DisplayName = "Streamer B", };
        _db.Streamers.AddRange(s1, s2);
        var l1 = new Layout { Name = "Layout A", PairId = pair.Id, StreamerId = s1.Id, };
        var l2 = new Layout { Name = "Layout B", PairId = pair.Id, StreamerId = s2.Id, };
        _db.Layouts.AddRange(l1, l2);
        // Create a single set of slots per layout (profile-agnostic). Positions are sample defaults.
        // Pokemon: 6 per layout => 12 per pair
        var createdSlots = new List<Slot>();
        for (var i = 0; i < 6; i++)
        {
            var s = new Slot
            {
                LayoutId = l1.Id,
                SlotType = SlotType.Pokemon,
                Index = i,
                Visible = true,
                X = 50,
                Y = 50 + i * 75,
                ZIndex = 1,
                Profile = SlotProfile.Owner,
                Width = 150,
                Height = 150,
            };
            _db.Slots.Add(s);
            createdSlots.Add(s);
        }
        for (var i = 0; i < 6; i++)
        {
            var s = new Slot
            {
                LayoutId = l2.Id,
                SlotType = SlotType.Pokemon,
                Index = i,
                Visible = true,
                X = 250,
                Y = 50 + i * 75,
                ZIndex = 1,
                Profile = SlotProfile.Partner,
                Width = 150,
                Height = 150,
            };
            _db.Slots.Add(s);
            createdSlots.Add(s);
        }

        // Badges: 8 per layout => 16 per pair (8 for Owner layout, 8 for Partner layout)
        for (var i = 0; i < 8; i++)
        {
            var s = new Slot
            {
                LayoutId = l1.Id,
                SlotType = SlotType.Badge,
                Index = i,
                Visible = true,
                X = 50 + i * 30,
                Y = 150,
                ZIndex = 1,
                Profile = SlotProfile.Owner
            };
            _db.Slots.Add(s);
            createdSlots.Add(s);
        }
        for (var i = 0; i < 8; i++)
        {
            var s = new Slot
            {
                LayoutId = l2.Id,
                SlotType = SlotType.Badge,
                Index = i,
                Visible = true,
                X = 50 + i * 30,
                Y = 150,
                ZIndex = 1,
                Profile = SlotProfile.Partner
            };
            _db.Slots.Add(s);
            createdSlots.Add(s);
        }

        // Couple profile-specific placement via SlotPlacement (Owner + Partner per slot)
        foreach (var slot in createdSlots)
        {
            _db.SlotPlacements.Add(new SlotPlacement
            {
                SlotId = slot.Id,
                Profile = SlotProfile.Owner,
                X = slot.X,
                Y = slot.Y,
                ZIndex = slot.ZIndex,
                Visible = true,
            });
            _db.SlotPlacements.Add(new SlotPlacement
            {
                SlotId = slot.Id,
                Profile = SlotProfile.Partner,
                X = slot.X,
                Y = slot.Y,
                ZIndex = slot.ZIndex,
                Visible = true,
            });
        }
        await _db.SaveChangesAsync();
        return Ok(new { pair.Id });
    }
}
