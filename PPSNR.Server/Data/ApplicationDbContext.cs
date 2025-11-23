using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PPSNR.Server.Data.Entities;

namespace PPSNR.Server.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Streamer> Streamers => Set<Streamer>();
    public DbSet<StreamerPair> Pairs => Set<StreamerPair>();
    public DbSet<Layout> Layouts => Set<Layout>();
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<PairLink> PairLinks => Set<PairLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Streamer>(e =>
        {
            e.HasIndex(x => x.TwitchId);
        });

        modelBuilder.Entity<StreamerPair>(e =>
        {
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
            e.HasIndex(s => new { s.LayoutId, s.SlotType, s.Index }).IsUnique();
        });
    }
}