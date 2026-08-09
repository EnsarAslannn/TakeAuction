using MediatR;
using TakeAuction.Api.Features.Auctions.PlaceBid;

namespace TakeAuction.Api.Features.Auctions;

public sealed class InvalidateAuctionCacheOnBidPlaced : INotificationHandler<BidPlacedEvent>
{
    private readonly AuctionCache _auctionCache;
    private readonly ILogger<InvalidateAuctionCacheOnBidPlaced> _logger;

    public InvalidateAuctionCacheOnBidPlaced(
        AuctionCache auctionCache,
        ILogger<InvalidateAuctionCacheOnBidPlaced> logger)
    {
        _auctionCache = auctionCache;
        _logger = logger;
    }

    public async Task Handle(BidPlacedEvent notification, CancellationToken cancellationToken)
    {
        await _auctionCache.InvalidateDetailAsync(notification.AuctionId, cancellationToken);
        await _auctionCache.InvalidateListsAsync(cancellationToken);

        _logger.LogInformation(
            "Auction {AuctionId} cache entries invalidated after bid {BidId}",
            notification.AuctionId,
            notification.BidId);
    }
}
