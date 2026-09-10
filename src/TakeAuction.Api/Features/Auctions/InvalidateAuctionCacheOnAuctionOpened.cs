using MediatR;
using TakeAuction.Api.Features.Auctions.OpenAuctions;

namespace TakeAuction.Api.Features.Auctions;

public sealed class InvalidateAuctionCacheOnAuctionOpened : INotificationHandler<AuctionOpenedEvent>
{
    private readonly AuctionCache _auctionCache;
    private readonly ILogger<InvalidateAuctionCacheOnAuctionOpened> _logger;

    public InvalidateAuctionCacheOnAuctionOpened(
        AuctionCache auctionCache,
        ILogger<InvalidateAuctionCacheOnAuctionOpened> logger)
    {
        _auctionCache = auctionCache;
        _logger = logger;
    }

    public async Task Handle(AuctionOpenedEvent notification, CancellationToken cancellationToken)
    {
        await _auctionCache.InvalidateDetailAsync(notification.AuctionId, cancellationToken);
        await _auctionCache.InvalidateListsAsync(cancellationToken);

        _logger.LogInformation(
            "Auction {AuctionId} cache entries invalidated after it opened",
            notification.AuctionId);
    }
}
