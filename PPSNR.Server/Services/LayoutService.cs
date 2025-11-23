using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server.Data;
using PPSNR.Server.Data.Entities;
using PPSNR.Server.Hubs;

namespace PPSNR.Server.Services;

public class LayoutService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<LayoutHub> _hub;

    public LayoutService(ApplicationDbContext db, IHubContext<LayoutHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<PairLink> CreateOrRotatePairLinkAsync(Guid pairId, CancellationToken ct = default)
    {
        var link = await _db.PairLinks.FirstOrDefaultAsync(x => x.PairId == pairId, ct);
        if (link == null)
        {
            link = new PairLink { PairId = pairId };
            _db.PairLinks.Add(link);
        }
        else
        {
            link.ViewToken = Guid.NewGuid();
            link.EditToken = Guid.NewGuid();
            link.CreatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return link;
    }

    public async Task<bool> ValidateViewTokenAsync(Guid pairId, Guid token, CancellationToken ct = default)
    {
        var link = await _db.PairLinks.AsNoTracking().FirstOrDefaultAsync(x => x.PairId == pairId, ct);
        return link != null && link.ViewToken == token && (link.ExpiresAt == null || link.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<bool> ValidateEditTokenAsync(Guid pairId, Guid token, CancellationToken ct = default)
    {
        var link = await _db.PairLinks.AsNoTracking().FirstOrDefaultAsync(x => x.PairId == pairId, ct);
        return link != null && link.EditToken == token && (link.ExpiresAt == null || link.ExpiresAt > DateTime.UtcNow);
    }

    public Task<List<Slot>> GetSlotsAsync(Guid layoutId, CancellationToken ct = default)
        => _db.Slots.Where(s => s.LayoutId == layoutId).OrderBy(s => s.ZIndex).ThenBy(s => s.Index).ToListAsync(ct);

    public async Task<Slot?> UpdateSlotAsync(Guid pairId, Slot slot, CancellationToken ct = default)
    {
        var existing = await _db.Slots.FirstOrDefaultAsync(s => s.Id == slot.Id, ct);
        if (existing == null) return null;
        existing.X = slot.X;
        existing.Y = slot.Y;
        existing.ZIndex = slot.ZIndex;
        existing.Visible = slot.Visible;
        existing.ImageUrl = slot.ImageUrl;
        existing.AdditionalProperties = slot.AdditionalProperties;
        existing.SlotType = slot.SlotType;
        existing.Index = slot.Index;
        await _db.SaveChangesAsync(ct);

        // Broadcast to the pair group
        await _hub.Clients.Group(pairId.ToString()).SendAsync("SlotUpdated", new
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
        }, ct);
        return existing;
    }
}
