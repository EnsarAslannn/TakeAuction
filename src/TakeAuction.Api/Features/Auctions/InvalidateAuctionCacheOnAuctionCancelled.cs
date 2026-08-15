using MediatR;
using TakeAuction.Api.Features.Auctions.CancelAuction;

namespace TakeAuction.Api.Features.Auctions;

public sealed class InvalidateAuctionCacheOnAuctionCancelled : INotificationHandler<AuctionCancelledEvent>
{
    private readonly AuctionCache _auctionCache;
    private readonly ILogger<InvalidateAuctionCacheOnAuctionCancelled> _logger;

    public InvalidateAuctionCacheOnAuctionCancelled(
        AuctionCache auctionCache,
        ILogger<InvalidateAuctionCacheOnAuctionCancelled> logger)
    {
        _auctionCache = auctionCache;
        _logger = logger;
    }

    public async Task Handle(AuctionCancelledEvent notification, CancellationToken cancellationToken)
    {
        await _auctionCache.InvalidateDetailAsync(notification.AuctionId, cancellationToken);
        await _auctionCache.InvalidateListsAsync(cancellationToken);

        _logger.LogInformation(
            "Auction {AuctionId} cache entries invalidated after it was withdrawn",
            notification.AuctionId);
    }
}
