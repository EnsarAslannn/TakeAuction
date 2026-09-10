using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Auctions.OpenAuctions;

public sealed class AuctionOpener
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IPublisher _publisher;
    private readonly IOutbox _outbox;
    private readonly ILogger<AuctionOpener> _logger;

    public AuctionOpener(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IPublisher publisher,
        IOutbox outbox,
        ILogger<AuctionOpener> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _publisher = publisher;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<bool> TryOpenAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();

        var nowUtc = _timeProvider.GetUtcNow();

        var auction = await _dbContext.Auctions
            .FirstOrDefaultAsync(entity => entity.Id == auctionId, cancellationToken);

        if (auction is null || !auction.Open(nowUtc))
        {
            return false;
        }

        _outbox.Enqueue(
            new AuctionOpenedIntegrationEvent(
                auction.Id,
                auction.SellerId,
                auction.CurrentPrice,
                auction.StartsAtUtc,
                auction.EndsAtUtc,
                nowUtc),
            nowUtc);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning(
                "Auction {AuctionId} changed while it was being opened; leaving it for the next attempt",
                auctionId);

            return false;
        }

        _logger.LogInformation(
            "Auction {AuctionId} opened for bidding at {StartsAtUtc}",
            auction.Id,
            auction.StartsAtUtc);

        await _publisher.Publish(
            new AuctionOpenedEvent(
                auction.Id,
                auction.SellerId,
                auction.CurrentPrice,
                auction.StartsAtUtc,
                auction.EndsAtUtc,
                nowUtc),
            cancellationToken);

        return true;
    }
}
