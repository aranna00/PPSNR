using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server.Data;
using PPSNR.Server.Data.Entities;
using PPSNR.Server.Hubs;
using PPSNR.Shared.SignalR;
using System.Text.Json;

namespace PPSNR.Server.Services;

public class LayoutService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<LayoutHub> _hub;
    private readonly IConfiguration _config;
    private readonly ILogger<LayoutService> _log;
    private readonly bool _loggingEnabled;

    public LayoutService(ApplicationDbContext db, IHubContext<LayoutHub> hub, IConfiguration config, ILogger<LayoutService> log)
    {
        _db = db;
        _hub = hub;
        _config = config;
        _log = log;
        var v = _config["ENABLE_SIGNALR_LOGGING"] ?? _config["EnableSignalRLogging"]; // accept either casing
        _loggingEnabled = !string.IsNullOrEmpty(v) && (string.Equals(v, "1") || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
    }

    public async Task BroadcastToggleBordersAsync(Guid pairId, bool show, CancellationToken ct = default)
    {
        try
        {
            var envelope = new SignalRMessageEnvelope
            {
                Type = "ToggleBorders",
                Data = JsonSerializer.SerializeToElement(new { show })
            };
            if (_loggingEnabled) _log.LogInformation("Broadcasting SignalR Message: {Type} for Pair {PairId}", envelope.Type, pairId);
            await _hub.Clients.Group(pairId.ToString()).SendAsync("Message", envelope, ct);
        }
        catch (Exception ex)
        {
            if (_loggingEnabled) _log.LogWarning(ex, "Failed to broadcast ToggleBorders for Pair {PairId}", pairId);
        }
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
            // Rotate all tokens so both owner and partner get fresh links
            link.ViewToken = Guid.NewGuid();              // Partner view
            link.EditToken = Guid.NewGuid();              // Owner edit (back-compat)
            link.OwnerViewToken = Guid.NewGuid();         // Owner view
            link.PartnerEditToken = Guid.NewGuid();       // Partner edit
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
        // Do not alter geometry on the Slot row; treat X/Y/ZIndex as immutable defaults.
        // Only update shared content fields (visible/image/additional/type/index).
        existing.Visible = slot.Visible;
        existing.ImageUrl = slot.ImageUrl;
        existing.AdditionalProperties = slot.AdditionalProperties;
        existing.SlotType = slot.SlotType;
        existing.Index = slot.Index;
        await _db.SaveChangesAsync(ct);

        // Upsert normalized geometry into SlotPlacements (position + size per profile)
        try
        {
            int? width = null;
            int? height = null;
            if (!string.IsNullOrWhiteSpace(existing.AdditionalProperties))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(existing.AdditionalProperties);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("w", out var wProp) && wProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        var wv = (int)wProp.GetDouble();
                        if (wv > 0) width = wv;
                    }
                    if (root.TryGetProperty("h", out var hProp) && hProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        var hv = (int)hProp.GetDouble();
                        if (hv > 0) height = hv;
                    }
                }
                catch { /* ignore malformed additional json */ }
            }

            // IMPORTANT: Use the incoming slot.Profile to decide which profile's placement
            // is being updated (editor/viewer profile), not the persisted Slot row's profile.
            var targetProfile = slot.Profile;
            var placement = await _db.SlotPlacements.FirstOrDefaultAsync(p => p.SlotId == existing.Id && p.Profile == targetProfile, ct);
            if (placement == null)
            {
                placement = new SlotPlacement
                {
                    SlotId = existing.Id,
                    Profile = targetProfile,
                    X = slot.X,
                    Y = slot.Y,
                    ZIndex = slot.ZIndex,
                    Visible = slot.Visible,
                    Width = width,
                    Height = height
                };
                _db.SlotPlacements.Add(placement);
            }
            else
            {
                placement.X = slot.X;
                placement.Y = slot.Y;
                placement.ZIndex = slot.ZIndex;
                placement.Visible = slot.Visible;
                placement.Width = width;
                placement.Height = height;
            }
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Non-fatal: failure to persist normalized geometry should not block slot update/broadcast
        }

        // Broadcast to the pair group with the UPDATED geometry (from the incoming change),
        // not the immutable defaults stored on the Slot row.
        // Derive size to include in broadcast from AdditionalProperties
        var (bw, bh) = TryGetSizeFromAdditional(existing.AdditionalProperties);

        // Build a typed DTO for the update
        var slotDto = new SlotUpdatedDto
        {
            Id = existing.Id,
            LayoutId = existing.LayoutId,
            Profile = (int?)slot.Profile,
            X = slot.X,
            Y = slot.Y,
            ZIndex = slot.ZIndex,
            Visible = slot.Visible,
            ImageUrl = existing.ImageUrl,
            AdditionalProperties = existing.AdditionalProperties,
            Width = bw,
            Height = bh
        };

        try
        {
            // Send typed envelope on "Message"
            var envelope = new SignalRMessageEnvelope
            {
                Type = "SlotUpdated",
                Data = System.Text.Json.JsonSerializer.SerializeToElement(slotDto)
            };

            if (_loggingEnabled) _log.LogInformation("Broadcasting SignalR Message: {Type} for Pair {PairId}", envelope.Type, pairId);
            await _hub.Clients.Group(pairId.ToString()).SendAsync("Message", envelope);
        }
        catch (Exception ex)
        {
            if (_loggingEnabled) _log.LogWarning(ex, "Failed to broadcast SignalR Message for Pair {PairId}", pairId);
        }

        // Return the updated slot entity
        return existing;
    }

    private static (int? w, int? h) TryGetSizeFromAdditional(string? additional)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(additional)) return (null, null);
            using var doc = System.Text.Json.JsonDocument.Parse(additional);
            var root = doc.RootElement;
            int? w = null, h = null;
            if (root.TryGetProperty("w", out var wProp) && wProp.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                var v = (int)wProp.GetDouble();
                if (v > 0) w = v;
            }
            if (root.TryGetProperty("h", out var hProp) && hProp.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                var v = (int)hProp.GetDouble();
                if (v > 0) h = v;
            }
            return (w, h);
        }
        catch
        {
            return (null, null);
        }
    }

    private static void ApplyDefaultPlacementFromSlot(Slot slot, SlotPlacement placement)
    {
        placement.X = slot.X;
        placement.Y = slot.Y;
        placement.ZIndex = slot.ZIndex;
        placement.Visible = slot.Visible;
        var (w, h) = TryGetSizeFromAdditional(slot.AdditionalProperties);
        placement.Width = w;
        placement.Height = h;
    }

    public async Task<int> ResetPlacementsToDefaultsAsync(Guid pairId, SlotProfile profile, CancellationToken ct = default)
    {
        // Find layouts for the pair
        var layoutIds = await _db.Layouts.Where(l => l.PairId == pairId).Select(l => l.Id).ToListAsync(ct);
        if (layoutIds.Count == 0) return 0;

        var slots = await _db.Slots.Where(s => layoutIds.Contains(s.LayoutId)).ToListAsync(ct);
        if (slots.Count == 0) return 0;

        var slotIds = slots.Select(s => s.Id).ToList();
        var placements = await _db.SlotPlacements.Where(p => slotIds.Contains(p.SlotId) && p.Profile == profile).ToListAsync(ct);
        var bySlot = placements.ToDictionary(p => p.SlotId, p => p);

        int changed = 0;
        foreach (var s in slots)
        {
            if (!bySlot.TryGetValue(s.Id, out var pl))
            {
                pl = new SlotPlacement { SlotId = s.Id, Profile = profile };
                _db.SlotPlacements.Add(pl);
            }
            ApplyDefaultPlacementFromSlot(s, pl);
            changed++;
        }
        await _db.SaveChangesAsync(ct);

        // Optional: broadcast a bulk reset event so connected editors can refresh quickly
        try
        {
            // Send new envelope for typed messaging
            var resetEnvelope = new SignalRMessageEnvelope
            {
                Type = "PlacementsReset",
                Data = null
            };
            if (_loggingEnabled) _log.LogInformation("Broadcasting SignalR Message: PlacementsReset for Pair {PairId}", pairId);
            await _hub.Clients.Group(pairId.ToString()).SendAsync("Message", resetEnvelope);
        }
        catch (Exception ex)
        {
            if (_loggingEnabled) _log.LogWarning(ex, "Failed to broadcast PlacementsReset for Pair {PairId}", pairId);
        }

        return changed;
    }
}
