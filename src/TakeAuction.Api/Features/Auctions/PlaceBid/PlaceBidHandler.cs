using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.PlaceBid;

public sealed class PlaceBidHandler : IRequestHandler<PlaceBidCommand, PlaceBidResult>
{
    public const int MaxAttempts = 3;

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IPublisher _publisher;
    private readonly IOutbox _outbox;
    private readonly ILogger<PlaceBidHandler> _logger;

    public PlaceBidHandler(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IPublisher publisher,
        IOutbox outbox,
        ILogger<PlaceBidHandler> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _publisher = publisher;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<PlaceBidResult> Handle(PlaceBidCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            _dbContext.ChangeTracker.Clear();

            var auction = await _dbContext.Auctions
                .FirstOrDefaultAsync(entity => entity.Id == command.AuctionId, cancellationToken);

            if (auction is null)
            {
                return PlaceBidResult.Rejected(BidRejection.AuctionNotFound);
            }

            var previousPrice = auction.CurrentPrice;
            var outbidBidderId = auction.LeadingBidderId;

            var outcome = auction.PlaceBid(command.BidderId, command.Amount, _timeProvider.GetUtcNow());

            if (!outcome.Succeeded)
            {
                return PlaceBidResult.Rejected(outcome.Rejection, auction.MinimumAcceptableBid);
            }

            var bid = outcome.Bid!;
            await _dbContext.Bids.AddAsync(bid, cancellationToken);

            _outbox.Enqueue(
                new BidPlacedIntegrationEvent(
                    auction.Id,
                    bid.Id,
                    bid.BidderId,
                    bid.Amount,
                    previousPrice,
                    outbidBidderId,
                    auction.EndsAtUtc,
                    bid.PlacedAtUtc),
                bid.PlacedAtUtc);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning(
                    "Concurrency conflict on auction {AuctionId} for bidder {BidderId} (attempt {Attempt} of {MaxAttempts})",
                    command.AuctionId,
                    command.BidderId,
                    attempt,
                    MaxAttempts);

                continue;
            }

            _logger.LogInformation(
                "Bid {BidId} of {Amount} accepted on auction {AuctionId} after {Attempt} attempt(s)",
                bid.Id,
                bid.Amount,
                auction.Id,
                attempt);

            await _publisher.Publish(
                new BidPlacedEvent(
                    auction.Id,
                    bid.Id,
                    bid.BidderId,
                    bid.Amount,
                    previousPrice,
                    outbidBidderId,
                    auction.EndsAtUtc,
                    bid.PlacedAtUtc),
                cancellationToken);

            return PlaceBidResult.Accepted(new PlaceBidResponse(
                bid.Id,
                auction.Id,
                bid.Amount,
                auction.CurrentPrice,
                auction.MinimumAcceptableBid,
                auction.BidCount,
                bid.PlacedAtUtc));
        }

        _logger.LogWarning(
            "Bid on auction {AuctionId} abandoned after {MaxAttempts} concurrency conflicts",
            command.AuctionId,
            MaxAttempts);

        return PlaceBidResult.Rejected(BidRejection.ConcurrencyConflict);
    }
}
