using System.ComponentModel.DataAnnotations;

namespace PPSNR.Server2.Data.Entities;

public class StreamerPair
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(200)]
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Layout> Layouts { get; set; } = new List<Layout>();
    public ICollection<PairLink> Links { get; set; } = new List<PairLink>();
}
