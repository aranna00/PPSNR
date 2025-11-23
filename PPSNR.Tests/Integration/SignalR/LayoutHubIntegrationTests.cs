using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PPSNR.Server.Hubs;

namespace PPSNR.Tests.Integration.SignalR;

[TestFixture]
public class LayoutHubIntegrationTests
{
    private sealed class SignalRWebAppFactory : TwitchAuthWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, configBuilder) =>
            {
                var inMemory = new Dictionary<string, string>
                {
                    ["DisableHttpsRedirection"] = "true"
                };
                configBuilder.AddInMemoryCollection(inMemory);
            });
        }
    }

    [Test]
    public async Task Broadcast_To_Group_Is_Received_By_All_Subscribers_In_That_Group_Only()
    {
        await using var factory = new SignalRWebAppFactory();
        var server = factory.Server; // forces server creation

        var baseAddress = server.BaseAddress; // http://localhost
        var handler = server.CreateHandler();

        static HubConnection CreateClient(Uri baseAddress, HttpMessageHandler handler)
        {
            return new HubConnectionBuilder()
                .WithUrl(new Uri(baseAddress, "/hubs/layout"), options =>
                {
                    options.HttpMessageHandlerFactory = _ => handler;
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                })
                .WithAutomaticReconnect()
                .Build();
        }

        var clientA = CreateClient(baseAddress, handler);
        var clientB = CreateClient(baseAddress, handler);
        var clientOther = CreateClient(baseAddress, handler);

        var pairId = Guid.NewGuid().ToString();
        var otherPairId = Guid.NewGuid().ToString();

        var receivedA = new TaskCompletionSource<object>();
        var receivedB = new TaskCompletionSource<object>();
        var receivedOther = new TaskCompletionSource<object>();

        clientA.On<object>("SlotUpdated", payload => receivedA.TrySetResult(payload));
        clientB.On<object>("SlotUpdated", payload => receivedB.TrySetResult(payload));
        clientOther.On<object>("SlotUpdated", payload => receivedOther.TrySetResult(payload));

        await clientA.StartAsync();
        await clientB.StartAsync();
        await clientOther.StartAsync();

        await clientA.InvokeAsync("SubscribeToPair", pairId);
        await clientB.InvokeAsync("SubscribeToPair", pairId);
        await clientOther.InvokeAsync("SubscribeToPair", otherPairId);

        // Ensure hub group subscriptions are processed
        await Task.Delay(100);

        await using var scope = factory.Services.CreateAsyncScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<LayoutHub>>();

        // broadcast to target group
        var payloadObj = new { Message = "Hello", Pair = pairId };
        await hubContext.Clients.Group(pairId).SendCoreAsync("SlotUpdated", new object[] { payloadObj });

        var a = await WaitOrTimeout(receivedA.Task, TimeSpan.FromSeconds(10));
        var b = await WaitOrTimeout(receivedB.Task, TimeSpan.FromSeconds(10));
        var other = await WaitOrTimeout(receivedOther.Task, TimeSpan.FromSeconds(3));

        a.Should().NotBeNull();
        b.Should().NotBeNull();
        other.Should().BeNull("client subscribed to a different group should not receive the broadcast");

        await clientA.DisposeAsync();
        await clientB.DisposeAsync();
        await clientOther.DisposeAsync();
    }

    private static async Task<T?> WaitOrTimeout<T>(Task<T> task, TimeSpan timeout)
    {
        var delay = Task.Delay(timeout);
        var completed = await Task.WhenAny(task, delay);
        if (completed == task) return await task;
        return default;
    }
}
