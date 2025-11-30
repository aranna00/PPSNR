using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using PPSNR.Server.Data.Entities;
using PPSNR.Server.Hubs;

namespace PPSNR.Tests;

public class LayoutServiceTests
{
    [Test]
    public async Task UpdateSlotAsync_UpdatesDbAndBroadcasts()
    {
        // Use in-memory Sqlite for realistic EF behavior
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<Server.Data.ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        // Create schema
        using (var ctx = new Server.Data.ApplicationDbContext(options))
        {
            ctx.Database.EnsureCreated();
            var pairId = Guid.NewGuid();
            var pair = new StreamerPair { Id = pairId, Name = "P" };
            ctx.Pairs.Add(pair);
            var streamer = new Streamer { Id = Guid.NewGuid(), DisplayName = "S" };
            ctx.Streamers.Add(streamer);

            var layout = new Layout { Name = "L", PairId = pairId, StreamerId = streamer.Id };
            ctx.Layouts.Add(layout);
            var slot = new Slot { LayoutId = layout.Id, Index = 0, SlotType = SlotType.Pokemon };
            ctx.Slots.Add(slot);
            ctx.SaveChanges();
        }

        // Mock IHubContext
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        var mockHub = new Mock<IHubContext<LayoutHub>>();
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        // Mock IConfiguration and ILogger
        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        mockConfig.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<Server.Services.LayoutService>>();

        using (var ctx = new Server.Data.ApplicationDbContext(options))
        {
            var service = new Server.Services.LayoutService(ctx, mockHub.Object, mockConfig.Object, mockLogger.Object);
            var existing = await ctx.Slots.Include(s => s.Layout).FirstAsync();
            existing.X = 10;
            existing.Y = 20;
            existing.ZIndex = 5;
            existing.Visible = true;
            existing.ImageUrl = "/resources/test.png";
            existing.AdditionalProperties = "{}";
            existing.SlotType = SlotType.Badge;
            existing.Index = 2;

            var updatedSlot = await service.UpdateSlotAsync(existing.Layout!.PairId, existing);
        }

        // Verify DB updated
        using var scope = new AssertionScope();

        await using (var ctx = new Server.Data.ApplicationDbContext(options))
        {
            var s = await ctx.Slots.FirstAsync();
            s.X.Should().Be(10);
            s.Y.Should().Be(20);
            s.ZIndex.Should().Be(5);
            s.Visible.Should().BeTrue();
            s.ImageUrl.Should().Be("/resources/test.png");
            s.SlotType.Should().Be(SlotType.Badge);
            s.Index.Should().Be(2);
        }

        // Verify broadcast occurred (Message envelope)
        mockClientProxy.Verify(c => c.SendCoreAsync("Message", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);

        connection.Close();
    }
}
