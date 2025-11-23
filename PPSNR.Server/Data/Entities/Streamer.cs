using System.ComponentModel.DataAnnotations;

namespace PPSNR.Server.Data.Entities;

public class Streamer
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(200)]
    public required string DisplayName { get; set; }
    [MaxLength(100)]
    public string? TwitchId { get; set; }
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public ICollection<Layout> Layouts { get; set; } = new List<Layout>();
}
