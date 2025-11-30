using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using PPSNR.Server.Data.Entities;
using PPSNR.Server.Hubs;

namespace PPSNR.Tests;

public class SlotPlacementSeparationTests
{
    [Test]
    public async Task UpdateSlotAsync_ChangesOnlyPlacement_ForEditedProfile()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<Server.Data.ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        // Mock hub context
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        var mockHub = new Mock<IHubContext<LayoutHub>>();
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        Guid pairId;
        Guid slotId;

        // Seed data: pair, layout, one slot, and two placements (Owner + Partner)
        using (var ctx = new Server.Data.ApplicationDbContext(options))
        {
            ctx.Database.EnsureCreated();
            var pair = new StreamerPair { Id = Guid.NewGuid(), Name = "P" };
            pairId = pair.Id;
            ctx.Pairs.Add(pair);
            var streamer = new Streamer { Id = Guid.NewGuid(), DisplayName = "S" };
            ctx.Streamers.Add(streamer);
            var layout = new Layout { Id = Guid.NewGuid(), Name = "L", PairId = pair.Id, StreamerId = streamer.Id };
            ctx.Layouts.Add(layout);
            var slot = new Slot { Id = Guid.NewGuid(), LayoutId = layout.Id, Index = 0, SlotType = SlotType.Pokemon, Profile = SlotProfile.Owner };
            slotId = slot.Id;
            ctx.Slots.Add(slot);
            ctx.SlotPlacements.Add(new Server.Data.Entities.SlotPlacement { SlotId = slot.Id, Profile = SlotProfile.Owner, X = 10, Y = 10, ZIndex = 1, Visible = true, Width = 100, Height = 100 });
            ctx.SlotPlacements.Add(new Server.Data.Entities.SlotPlacement { SlotId = slot.Id, Profile = SlotProfile.Partner, X = 200, Y = 200, ZIndex = 1, Visible = true, Width = 120, Height = 120 });
            ctx.SaveChanges();
        }

        // Act: update the slot as Owner with new geometry and additional size
        using (var ctx = new Server.Data.ApplicationDbContext(options))
        {
            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            mockConfig.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);
            var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<Server.Services.LayoutService>>();
            var service = new Server.Services.LayoutService(ctx, mockHub.Object, mockConfig.Object, mockLogger.Object);
            var s = await ctx.Slots.FirstAsync(x => x.Id == slotId);
            s.Profile = SlotProfile.Owner; // ensure we edit as Owner
            s.X = 50;
            s.Y = 60;
            s.ZIndex = 3;
            s.Visible = true;
            s.AdditionalProperties = "{\"w\":150,\"h\":160}";
            await service.UpdateSlotAsync(pairId, s);
        }

        // Assert: Owner placement updated, Partner placement unchanged
        await using (var ctx = new Server.Data.ApplicationDbContext(options))
        {
            var ownerPl = await ctx.SlotPlacements.FirstAsync(p => p.SlotId == slotId && p.Profile == SlotProfile.Owner);
            var partnerPl = await ctx.SlotPlacements.FirstAsync(p => p.SlotId == slotId && p.Profile == SlotProfile.Partner);

            ownerPl.X.Should().Be(50);
            ownerPl.Y.Should().Be(60);
            ownerPl.ZIndex.Should().Be(3);
            ownerPl.Width.Should().Be(150);
            ownerPl.Height.Should().Be(160);

            partnerPl.X.Should().Be(200);
            partnerPl.Y.Should().Be(200);
            partnerPl.ZIndex.Should().Be(1);
            partnerPl.Width.Should().Be(120);
            partnerPl.Height.Should().Be(120);
        }

        connection.Close();
    }
}
