using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server.Data;
using PPSNR.Server.Data.Entities;
using PPSNR.Server.Services;
using System.Security.Claims;

namespace PPSNR.Server.Controllers.Api;

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
    public async Task<IActionResult> CreateOrRotateLinks(Guid pairId, [FromServices] Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
    {
        // Validate antiforgery token explicitly for this POST endpoint
        await antiforgery.ValidateRequestAsync(HttpContext);
        var pair = await _db.Pairs.FindAsync(pairId);
        if (pair == null) return NotFound();
        var link = await _layoutService.CreateOrRotatePairLinkAsync(pairId);
        return Ok(new { link.ViewToken, link.EditToken, ownerViewToken = link.OwnerViewToken, partnerEditToken = link.PartnerEditToken });
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
    public async Task<IActionResult> UpdateSlot(Guid pairId, Guid layoutId, Guid slotId, [FromBody] Slot incoming, [FromServices] Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
     {
        if (slotId != incoming.Id) return BadRequest();
         var layout = await _db.Layouts.FirstOrDefaultAsync(l => l.Id == layoutId && l.PairId == pairId);
         if (layout == null) return NotFound();

         // Owner-only authorization: only the owner of the pair can edit
         var pair = await _db.Pairs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pairId);
         var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
         if (pair == null) return NotFound();
         if (string.IsNullOrEmpty(userId) || !string.Equals(pair.OwnerUserId, userId, StringComparison.Ordinal))
             return Forbid();

        // Validate antiforgery only after confirming the user is allowed to perform the action
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch
        {
            return BadRequest();
        }

        // Fetch the target slot by id within the layout (any profile)
        var existing = await _db.Slots.FirstOrDefaultAsync(s => s.Id == slotId && s.LayoutId == layoutId);
        if (existing == null) return NotFound();

        // Do NOT change X/Y/ZIndex here — those are treated as immutable defaults.
        // Only update shared content fields on the Slot row.
        existing.Visible = incoming.Visible;                 // shared
        existing.ImageUrl = incoming.ImageUrl;               // shared
        existing.AdditionalProperties = incoming.AdditionalProperties; // shared
        existing.SlotType = incoming.SlotType;
        existing.Index = incoming.Index;
        await _db.SaveChangesAsync();

        // Propagate shared fields to partner-profile counterpart (same Layout/Type/Index)
        var partnerCounterpart = await _db.Slots.FirstOrDefaultAsync(s => s.LayoutId == layoutId
                                                                          && s.SlotType == existing.SlotType
                                                                          && s.Index == existing.Index
                                                                          && s.Profile == SlotProfile.Partner);
        if (partnerCounterpart != null)
        {
            partnerCounterpart.Visible = existing.Visible;
            partnerCounterpart.ImageUrl = existing.ImageUrl;
            partnerCounterpart.AdditionalProperties = existing.AdditionalProperties;
            await _db.SaveChangesAsync();
        }

        // Broadcast to viewers of this pair (both affected slots)
        // Mark this change as coming from the Owner profile so placements update accordingly
        var ownerGeom = new Slot
        {
            Id = existing.Id,
            LayoutId = existing.LayoutId,
            SlotType = existing.SlotType,
            Index = existing.Index,
            Profile = SlotProfile.Owner,
            ImageUrl = existing.ImageUrl,
            AdditionalProperties = existing.AdditionalProperties,
            // Geometry comes from the incoming payload (editor changes)
            X = incoming.X,
            Y = incoming.Y,
            ZIndex = incoming.ZIndex,
            Visible = incoming.Visible,
        };
        await _layoutService.UpdateSlotAsync(pairId, ownerGeom);
        if (partnerCounterpart != null)
        {
            // Also broadcast/update placement for the partner counterpart using the same geometry for the Owner profile
            var partnerGeom = new Slot
            {
                Id = partnerCounterpart.Id,
                LayoutId = partnerCounterpart.LayoutId,
                SlotType = partnerCounterpart.SlotType,
                Index = partnerCounterpart.Index,
                Profile = SlotProfile.Owner,
                ImageUrl = partnerCounterpart.ImageUrl,
                AdditionalProperties = partnerCounterpart.AdditionalProperties,
                X = incoming.X,
                Y = incoming.Y,
                ZIndex = incoming.ZIndex,
                Visible = incoming.Visible,
            };
            await _layoutService.UpdateSlotAsync(pairId, partnerGeom);
        }

         // Return a DTO to avoid potential JSON cycles on EF navigation properties
         return Ok(new
         {
             existing.Id,
             existing.LayoutId,
             existing.X,
             existing.Y,
             existing.ZIndex,
             existing.Visible,
             existing.ImageUrl,
             existing.SlotType,
             existing.Index,
             existing.AdditionalProperties
         });
     }

    // Partner editing endpoint: token-authenticated, anonymous, antiforgery required
    [HttpPost("partner/{token:guid}/pairs/{pairId:guid}/layouts/{layoutId:guid}/slots/{slotId:guid}")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateSlotAsPartner(Guid token, Guid pairId, Guid layoutId, Guid slotId, [FromBody] Slot incoming, [FromServices] Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery)
    {
        if (slotId != incoming.Id) return BadRequest();

        // Validate antiforgery
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch
        {
            return BadRequest();
        }

        // Validate link token for partner edit
        var link = await _db.PairLinks.AsNoTracking().FirstOrDefaultAsync(l => l.PairId == pairId);
        if (link == null || link.PartnerEditToken != token || (link.ExpiresAt != null && link.ExpiresAt <= DateTime.UtcNow))
            return Forbid();

        var layout = await _db.Layouts.AsNoTracking().FirstOrDefaultAsync(l => l.Id == layoutId && l.PairId == pairId);
        if (layout == null) return NotFound();

        // Fetch the target slot by id within the layout (any profile)
        var existing = await _db.Slots.FirstOrDefaultAsync(s => s.Id == slotId && s.LayoutId == layoutId);
        if (existing == null) return NotFound();

        // Do NOT change X/Y/ZIndex here — those are treated as immutable defaults.
        // Only update shared content fields on the Slot row.
        existing.Visible = incoming.Visible;                 // shared
        existing.ImageUrl = incoming.ImageUrl;               // shared
        existing.AdditionalProperties = incoming.AdditionalProperties; // shared

        await _db.SaveChangesAsync();

        // Propagate shared fields to owner-profile counterpart (same Layout/Type/Index)
        var ownerCounterpart = await _db.Slots.FirstOrDefaultAsync(s => s.LayoutId == layoutId
                                                                         && s.SlotType == existing.SlotType
                                                                         && s.Index == existing.Index
                                                                         && s.Profile == SlotProfile.Owner);
        if (ownerCounterpart != null)
        {
            ownerCounterpart.Visible = existing.Visible;
            ownerCounterpart.ImageUrl = existing.ImageUrl;
            ownerCounterpart.AdditionalProperties = existing.AdditionalProperties;
            await _db.SaveChangesAsync();
        }

        // Broadcast to viewers of this pair (both affected slots)
        // Mark this change as coming from the Partner profile so placements update accordingly
        var partnerGeom = new Slot
        {
            Id = existing.Id,
            LayoutId = existing.LayoutId,
            SlotType = existing.SlotType,
            Index = existing.Index,
            Profile = SlotProfile.Partner,
            ImageUrl = existing.ImageUrl,
            AdditionalProperties = existing.AdditionalProperties,
            // Geometry comes from the incoming payload (editor changes)
            X = incoming.X,
            Y = incoming.Y,
            ZIndex = incoming.ZIndex,
            Visible = incoming.Visible,
        };
        await _layoutService.UpdateSlotAsync(pairId, partnerGeom);
        if (ownerCounterpart != null)
        {
            var ownerGeomFromPartner = new Slot
            {
                Id = ownerCounterpart.Id,
                LayoutId = ownerCounterpart.LayoutId,
                SlotType = ownerCounterpart.SlotType,
                Index = ownerCounterpart.Index,
                Profile = SlotProfile.Partner,
                ImageUrl = ownerCounterpart.ImageUrl,
                AdditionalProperties = ownerCounterpart.AdditionalProperties,
                X = incoming.X,
                Y = incoming.Y,
                ZIndex = incoming.ZIndex,
                Visible = incoming.Visible,
            };
            await _layoutService.UpdateSlotAsync(pairId, ownerGeomFromPartner);
        }

        return Ok(new
        {
            existing.Id,
            existing.LayoutId,
            existing.X,
            existing.Y,
            existing.ZIndex,
            existing.Visible,
            existing.ImageUrl,
            existing.SlotType,
            existing.Index,
            existing.AdditionalProperties
        });
    }
 }
