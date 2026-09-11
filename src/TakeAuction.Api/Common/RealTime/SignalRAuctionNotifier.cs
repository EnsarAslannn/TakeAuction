using Microsoft.AspNetCore.SignalR;

namespace TakeAuction.Api.Common.RealTime;

public sealed class SignalRAuctionNotifier : IAuctionNotifier
{
    private readonly IHubContext<AuctionHub, IAuctionClient> _hubContext;
    private readonly ILogger<SignalRAuctionNotifier> _logger;

    public SignalRAuctionNotifier(
        IHubContext<AuctionHub, IAuctionClient> hubContext,
        ILogger<SignalRAuctionNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BidPlacedAsync(
        BidPlacedNotification notification,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string[] groups = [AuctionHub.AuctionGroup(notification.AuctionId), AuctionHub.LobbyGroup];

        await _hubContext.Clients.Groups(groups).BidPlaced(notification);

        _logger.LogDebug(
            "Broadcast bid {BidId} of {Amount} to watchers of auction {AuctionId} and the lobby",
            notification.BidId,
            notification.Amount,
            notification.AuctionId);
    }

    public async Task OutbidAsync(
        Guid bidderId,
        OutbidNotification notification,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _hubContext.Clients.User(bidderId.ToString()).Outbid(notification);

        _logger.LogDebug(
            "Told bidder {BidderId} they were outbid on auction {AuctionId}",
            bidderId,
            notification.AuctionId);
    }

    public async Task NotifyUserAsync(
        Guid userId,
        UserNotification notification,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _hubContext.Clients.User(userId.ToString()).NotificationReceived(notification);

        _logger.LogDebug(
            "Pushed a {Kind} notification about auction {AuctionId} to user {UserId}",
            notification.Kind,
            notification.AuctionId,
            userId);
    }

    public async Task AuctionStatusChangedAsync(
        AuctionStatusChangedNotification notification,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string[] groups = [AuctionHub.AuctionGroup(notification.AuctionId), AuctionHub.LobbyGroup];

        await _hubContext.Clients.Groups(groups).AuctionStatusChanged(notification);

        _logger.LogDebug(
            "Broadcast status {Status} for auction {AuctionId}",
            notification.Status,
            notification.AuctionId);
    }
}
