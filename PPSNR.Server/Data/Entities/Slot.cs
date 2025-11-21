using System.ComponentModel.DataAnnotations;

namespace PPSNR.Server2.Data.Entities;

public enum SlotType
{
    Pokemon = 0,
    Badge = 1
}

public class Slot
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid LayoutId { get; set; }
    public Layout? Layout { get; set; }

    public SlotType SlotType { get; set; }
    public int Index { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    public float X { get; set; }
    public float Y { get; set; }
    public int ZIndex { get; set; }
    public bool Visible { get; set; } = true;

    // JSON for future flags (e.g., greyscale)
    public string? AdditionalProperties { get; set; }
}
