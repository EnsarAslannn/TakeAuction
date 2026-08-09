using MediatR;
using TakeAuction.Api.Features.Auctions.CreateAuction;

namespace TakeAuction.Api.Features.Auctions;

public sealed class InvalidateAuctionCacheOnAuctionCreated : INotificationHandler<AuctionCreatedEvent>
{
    private readonly AuctionCache _auctionCache;
    private readonly ILogger<InvalidateAuctionCacheOnAuctionCreated> _logger;

    public InvalidateAuctionCacheOnAuctionCreated(
        AuctionCache auctionCache,
        ILogger<InvalidateAuctionCacheOnAuctionCreated> logger)
    {
        _auctionCache = auctionCache;
        _logger = logger;
    }

    public async Task Handle(AuctionCreatedEvent notification, CancellationToken cancellationToken)
    {
        await _auctionCache.InvalidateListsAsync(cancellationToken);

        _logger.LogInformation(
            "Auction list cache generation rotated after auction {AuctionId} was created",
            notification.AuctionId);
    }
}
