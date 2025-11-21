using System.ComponentModel.DataAnnotations;

namespace PPSNR.Server2.Data.Entities;

public class PairLink
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required]
    public Guid PairId { get; set; }
    public StreamerPair? Pair { get; set; }

    public Guid ViewToken { get; set; } = Guid.NewGuid();
    public Guid EditToken { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}
