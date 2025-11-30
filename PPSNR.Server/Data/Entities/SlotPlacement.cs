using System.ComponentModel.DataAnnotations;

namespace PPSNR.Server.Data.Entities;

/// <summary>
/// Normalized per-profile geometry for a Slot.
/// Extracted from Slot (X, Y, ZIndex, Visible) and AdditionalProperties (size) to avoid slot duplication issues.
/// </summary>
public class SlotPlacement
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SlotId { get; set; }
    public Slot? Slot { get; set; }

    public SlotProfile Profile { get; set; } = SlotProfile.Owner;

    public float X { get; set; }
    public float Y { get; set; }
    public int ZIndex { get; set; }
    public bool Visible { get; set; } = true;

    // Optional persisted size (pixels). If null, UI may fall back to defaults.
    public int? Width { get; set; }
    public int? Height { get; set; }
}
