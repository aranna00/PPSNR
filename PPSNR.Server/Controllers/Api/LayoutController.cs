using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server2.Data;
using PPSNR.Server2.Data.Entities;
using PPSNR.Server2.Services;

namespace PPSNR.Server2.Controllers.Api;

[ApiController]
[Route("api")] 
public class LayoutController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly LayoutService _layoutService;

    public LayoutController(ApplicationDbContext db, LayoutService layoutService)
    {
        _db = db;
        _layoutService = layoutService;
    }

    [HttpPost("pairs/{pairId:guid}/links")]
    public async Task<IActionResult> CreateOrRotateLinks(Guid pairId)
    {
        var pair = await _db.Pairs.FindAsync(pairId);
        if (pair == null) return NotFound();
        var link = await _layoutService.CreateOrRotatePairLinkAsync(pairId);
        return Ok(new { link.ViewToken, link.EditToken });
    }

    // Endpoint to issue and return an antiforgery request token and set the antiforgery cookie in the browser.
    // The browser must call this (via fetch) so the cookie is stored client-side.
    [HttpGet("antiforgery/token")]
    [AllowAnonymous]
    public IActionResult GetAntiforgeryToken([FromServices] Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var headerName = tokens.HeaderName ?? "RequestVerificationToken";
        return Ok(new { token = tokens.RequestToken, headerName });
    }

    [HttpPost("pairs/{pairId:guid}/layouts/{layoutId:guid}/slots/{slotId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateSlot(Guid pairId, Guid layoutId, Guid slotId, [FromBody] Slot incoming)
    {
        if (slotId != incoming.Id) return BadRequest();
        var layout = await _db.Layouts.FirstOrDefaultAsync(l => l.Id == layoutId && l.PairId == pairId);
        if (layout == null) return NotFound();

        var updated = await _layoutService.UpdateSlotAsync(pairId, incoming);
        if (updated == null) return NotFound();
        return Ok(updated);
    }
}
