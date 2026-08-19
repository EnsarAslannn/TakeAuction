using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Auctions.ExpireAuctions;

public sealed class AuctionCloser
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IPublisher _publisher;
    private readonly IOutbox _outbox;
    private readonly ILogger<AuctionCloser> _logger;

    public AuctionCloser(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IPublisher publisher,
        IOutbox outbox,
        ILogger<AuctionCloser> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _publisher = publisher;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<bool> TryCloseAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();

        var nowUtc = _timeProvider.GetUtcNow();

        var auction = await _dbContext.Auctions
            .FirstOrDefaultAsync(entity => entity.Id == auctionId, cancellationToken);

        if (auction is null || !auction.End(nowUtc))
        {
            return false;
        }

        _outbox.Enqueue(
            new AuctionEndedIntegrationEvent(
                auction.Id,
                auction.SellerId,
                auction.LeadingBidderId,
                auction.CurrentPrice,
                auction.BidCount,
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
                "Auction {AuctionId} changed while it was being closed; leaving it for the next attempt",
                auctionId);

            return false;
        }

        _logger.LogInformation(
            "Auction {AuctionId} ended at {FinalPrice} after {BidCount} bid(s)",
            auction.Id,
            auction.CurrentPrice,
            auction.BidCount);

        await _publisher.Publish(
            new AuctionEndedEvent(
                auction.Id,
                auction.SellerId,
                auction.LeadingBidderId,
                auction.CurrentPrice,
                auction.BidCount,
                auction.EndsAtUtc,
                nowUtc),
            cancellationToken);

        return true;
    }
}
