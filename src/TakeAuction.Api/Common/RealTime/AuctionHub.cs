using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TakeAuction.Api.Common.RealTime;

[AllowAnonymous]
public sealed class AuctionHub : Hub<IAuctionClient>
{
    public const string Route = "/hubs/auctions";

    public const string LobbyGroup = "auctions:lobby";

    private readonly ILogger<AuctionHub> _logger;

    public AuctionHub(ILogger<AuctionHub> logger) => _logger = logger;

    public static string AuctionGroup(Guid auctionId) => $"auction:{auctionId}";

    public async Task SubscribeToAuction(Guid auctionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AuctionGroup(auctionId));

        _logger.LogDebug(
            "Connection {ConnectionId} subscribed to auction {AuctionId}",
            Context.ConnectionId,
            auctionId);
    }

    public async Task UnsubscribeFromAuction(Guid auctionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AuctionGroup(auctionId));

        _logger.LogDebug(
            "Connection {ConnectionId} unsubscribed from auction {AuctionId}",
            Context.ConnectionId,
            auctionId);
    }

    public Task SubscribeToLobby() => Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroup);

    public Task UnsubscribeFromLobby() => Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroup);
}
