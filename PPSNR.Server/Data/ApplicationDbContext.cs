using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server.Data.Entities;

namespace PPSNR.Server.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<Streamer> Streamers => Set<Streamer>();
    public DbSet<StreamerPair> Pairs => Set<StreamerPair>();
    public DbSet<Layout> Layouts => Set<Layout>();
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<SlotPlacement> SlotPlacements => Set<SlotPlacement>();
    public DbSet<PairLink> PairLinks => Set<PairLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ExternalIdentity relationships
        modelBuilder.Entity<ExternalIdentity>(e =>
        {
            // Foreign key to ApplicationUser
            e.HasOne(ei => ei.ApplicationUser)
                .WithMany(u => u.ExternalIdentities)
                .HasForeignKey(ei => ei.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure provider + providerUserId combination is unique per user (no duplicate links)
            e.HasIndex(ei => new { ei.ApplicationUserId, ei.ProviderName, ei.ProviderUserId })
                .IsUnique();

            // Index on ProviderName + ProviderUserId for quick lookups (e.g., "find user by Twitch ID")
            e.HasIndex(ei => new { ei.ProviderName, ei.ProviderUserId });
        });

        // Configure Streamer relationships
        modelBuilder.Entity<Streamer>(e =>
        {
            // Foreign key to ApplicationUser
            e.HasOne(s => s.ApplicationUser)
                .WithMany(u => u.Streamers)
                .HasForeignKey(s => s.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StreamerPair>(e =>
        {
            // Use OwnerUserId as the foreign key for Owner navigation
            e.HasOne(p => p.Owner)
                .WithMany()
                .HasForeignKey(p => p.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Use PartnerUserId as the foreign key for Partner navigation
            e.HasOne(p => p.Partner)
                .WithMany()
                .HasForeignKey(p => p.PartnerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(p => p.Layouts).WithOne(l => l.Pair!).HasForeignKey(l => l.PairId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Links).WithOne(l => l.Pair!).HasForeignKey(l => l.PairId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Layout>(e =>
        {
            e.HasOne(l => l.Streamer).WithMany(s => s.Layouts).HasForeignKey(l => l.StreamerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(l => l.Slots).WithOne(s => s.Layout!).HasForeignKey(s => s.LayoutId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Slot>(e =>
        {
            e.HasIndex(s => new { s.LayoutId, s.SlotType, s.Index, s.Profile }).IsUnique();
        });

        modelBuilder.Entity<SlotPlacement>(e =>
        {
            e.HasOne(sp => sp.Slot).WithMany().HasForeignKey(sp => sp.SlotId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(sp => new { sp.SlotId, sp.Profile }).IsUnique();
        });
    }
}

