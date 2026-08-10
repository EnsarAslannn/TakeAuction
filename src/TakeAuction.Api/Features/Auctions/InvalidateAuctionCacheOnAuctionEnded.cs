using MediatR;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class InvalidateAuctionCacheOnAuctionEnded : INotificationHandler<AuctionEndedEvent>
{
    private readonly AuctionCache _auctionCache;
    private readonly ILogger<InvalidateAuctionCacheOnAuctionEnded> _logger;

    public InvalidateAuctionCacheOnAuctionEnded(
        AuctionCache auctionCache,
        ILogger<InvalidateAuctionCacheOnAuctionEnded> logger)
    {
        _auctionCache = auctionCache;
        _logger = logger;
    }

    public async Task Handle(AuctionEndedEvent notification, CancellationToken cancellationToken)
    {
        await _auctionCache.InvalidateDetailAsync(notification.AuctionId, cancellationToken);
        await _auctionCache.InvalidateListsAsync(cancellationToken);

        _logger.LogInformation(
            "Auction {AuctionId} cache entries invalidated after it ended",
            notification.AuctionId);
    }
}
