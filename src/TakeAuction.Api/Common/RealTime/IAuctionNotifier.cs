namespace TakeAuction.Api.Common.RealTime;

public interface IAuctionNotifier
{
    Task BidPlacedAsync(BidPlacedNotification notification, CancellationToken cancellationToken = default);

    Task AuctionStatusChangedAsync(
        AuctionStatusChangedNotification notification,
        CancellationToken cancellationToken = default);
}
