using Microsoft.AspNetCore.SignalR;

namespace PPSNR.Server2.Hubs;

public class LayoutHub : Hub
{
    // Clients join a group per pairId to receive updates
    public async Task SubscribeToPair(string pairId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, pairId);
    }

    public async Task UnsubscribeFromPair(string pairId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, pairId);
    }
}
