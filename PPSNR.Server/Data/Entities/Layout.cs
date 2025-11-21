using System.ComponentModel.DataAnnotations;

namespace PPSNR.Server2.Data.Entities;

public class Layout
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StreamerId { get; set; }
    public Streamer? Streamer { get; set; }

    [Required]
    public Guid PairId { get; set; }
    public StreamerPair? Pair { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public int? Width { get; set; }
    public int? Height { get; set; }
    [MaxLength(500)]
    public string? BackgroundImage { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    // UI options
    public bool ShowUnachievedBadgesGreyedOut { get; set; } = true;

    public ICollection<Slot> Slots { get; set; } = new List<Slot>();
}
