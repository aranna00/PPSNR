using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using NUnit.Framework;
using PPSNR.Server.Hubs;

namespace PPSNR.Tests.Hubs;

[TestFixture]
public class LayoutHubTests
{
    [Test]
    public async Task SubscribeToPair_AddsConnectionToGroup()
    {
        // Arrange
        var groupManager = new Mock<IGroupManager>(MockBehavior.Strict);
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns("conn-1");

        var tcs = new TaskCompletionSource<bool>();
        groupManager
            .Setup(g => g.AddToGroupAsync("conn-1", "pair-123", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => tcs.TrySetResult(true));

        var hub = new LayoutHub();
        SetHubState(hub, context.Object, groupManager.Object);

        // Act
        await hub.SubscribeToPair("pair-123");

        // Assert
        (await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();
        groupManager.VerifyAll();
    }

    [Test]
    public async Task UnsubscribeFromPair_RemovesConnectionFromGroup()
    {
        // Arrange
        var groupManager = new Mock<IGroupManager>(MockBehavior.Strict);
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns("conn-2");

        var tcs = new TaskCompletionSource<bool>();
        groupManager
            .Setup(g => g.RemoveFromGroupAsync("conn-2", "pair-XYZ", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => tcs.TrySetResult(true));

        var hub = new LayoutHub();
        SetHubState(hub, context.Object, groupManager.Object);

        // Act
        await hub.UnsubscribeFromPair("pair-XYZ");

        // Assert
        (await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2))).Should().BeTrue();
        groupManager.VerifyAll();
    }

    private static void SetHubState(Hub hub, HubCallerContext context, IGroupManager groups)
    {
        var hubType = typeof(Hub);
        var contextProp = hubType.GetProperty("Context", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var groupsProp = hubType.GetProperty("Groups", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        contextProp!.SetValue(hub, context);
        groupsProp!.SetValue(hub, groups);
    }
}
