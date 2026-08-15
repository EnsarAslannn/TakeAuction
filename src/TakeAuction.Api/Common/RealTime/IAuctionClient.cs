namespace TakeAuction.Api.Common.RealTime;

public interface IAuctionClient
{
    Task BidPlaced(BidPlacedNotification notification);

    Task AuctionStatusChanged(AuctionStatusChangedNotification notification);

    Task Outbid(OutbidNotification notification);
}
