﻿using System.ComponentModel.DataAnnotations;

namespace PPSNR.Server.Data.Entities;

public class Streamer
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// The ApplicationUser who owns this streamer profile.
    /// </summary>
    [MaxLength(128)]
    public string? ApplicationUserId { get; set; }

    public ApplicationUser? ApplicationUser { get; set; }

    /// <summary>
    /// Avatar URL - can come from a linked external provider or be custom.
    /// </summary>
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public ICollection<Layout> Layouts { get; set; } = new List<Layout>();
}
