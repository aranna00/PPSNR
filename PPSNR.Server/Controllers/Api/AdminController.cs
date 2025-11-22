using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server2.Data;
using PPSNR.Server2.Data.Entities;
using System.Security.Claims;

namespace PPSNR.Server2.Controllers.Api;

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

        var pair = new StreamerPair { Name = $"Pair {DateTime.UtcNow:HHmmss}", OwnerUserId = userId };
        _db.Pairs.Add(pair);
        var s1 = new Streamer { DisplayName = "Streamer A" };
        var s2 = new Streamer { DisplayName = "Streamer B" };
        _db.Streamers.AddRange(s1, s2);
        var l1 = new Layout { Name = "Layout A", PairId = pair.Id, StreamerId = s1.Id };
        var l2 = new Layout { Name = "Layout B", PairId = pair.Id, StreamerId = s2.Id };
        _db.Layouts.AddRange(l1, l2);
        for (int i = 0; i < 6; i++) _db.Slots.Add(new Slot { LayoutId = l1.Id, SlotType = SlotType.Pokemon, Index = i, Visible = false, X = 50 + i * 60, Y = 50, ZIndex = 1 });
        for (int i = 0; i < 16; i++) _db.Slots.Add(new Slot { LayoutId = l1.Id, SlotType = SlotType.Badge, Index = i, Visible = i < 8, X = 50 + i * 30, Y = 150, ZIndex = 1 });
        for (int i = 0; i < 6; i++) _db.Slots.Add(new Slot { LayoutId = l2.Id, SlotType = SlotType.Pokemon, Index = i, Visible = false, X = 50 + i * 60, Y = 250, ZIndex = 1 });
        for (int i = 0; i < 16; i++) _db.Slots.Add(new Slot { LayoutId = l2.Id, SlotType = SlotType.Badge, Index = i, Visible = i < 8, X = 50 + i * 30, Y = 350, ZIndex = 1 });
        await _db.SaveChangesAsync();
        return Ok(new { pair.Id });
    }
}
