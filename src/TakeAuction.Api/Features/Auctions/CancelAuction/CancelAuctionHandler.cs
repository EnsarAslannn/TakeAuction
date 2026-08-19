using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.CancelAuction;

public sealed class CancelAuctionHandler : IRequestHandler<CancelAuctionCommand, CancelAuctionResult>
{
    public const int MaxAttempts = 3;

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IPublisher _publisher;
    private readonly IOutbox _outbox;
    private readonly ILogger<CancelAuctionHandler> _logger;

    public CancelAuctionHandler(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IPublisher publisher,
        IOutbox outbox,
        ILogger<CancelAuctionHandler> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _publisher = publisher;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<CancelAuctionResult> Handle(
        CancelAuctionCommand command,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            _dbContext.ChangeTracker.Clear();

            var auction = await _dbContext.Auctions
                .FirstOrDefaultAsync(entity => entity.Id == command.AuctionId, cancellationToken);

            if (auction is null)
            {
                return CancelAuctionResult.Rejected(CancelRejection.AuctionNotFound);
            }

            var now = _timeProvider.GetUtcNow();
            var outcome = auction.Cancel(command.SellerId, now);

            if (!outcome.Succeeded)
            {
                return CancelAuctionResult.Rejected(outcome.Rejection);
            }

            _outbox.Enqueue(
                new AuctionCancelledIntegrationEvent(
                    auction.Id,
                    auction.SellerId,
                    auction.CurrentPrice,
                    auction.EndsAtUtc,
                    now),
                now);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning(
                    "Auction {AuctionId} changed while seller {SellerId} was withdrawing it (attempt {Attempt} of {MaxAttempts})",
                    command.AuctionId,
                    command.SellerId,
                    attempt,
                    MaxAttempts);

                continue;
            }

            _logger.LogInformation(
                "Auction {AuctionId} withdrawn by seller {SellerId}",
                auction.Id,
                auction.SellerId);

            await _publisher.Publish(
                new AuctionCancelledEvent(
                    auction.Id,
                    auction.SellerId,
                    auction.CurrentPrice,
                    auction.EndsAtUtc,
                    now),
                cancellationToken);

            return CancelAuctionResult.Accepted(
                new CancelAuctionResponse(auction.Id, auction.Status.ToString(), now));
        }

        _logger.LogWarning(
            "Withdrawal of auction {AuctionId} abandoned after {MaxAttempts} concurrency conflicts",
            command.AuctionId,
            MaxAttempts);

        return CancelAuctionResult.Rejected(CancelRejection.ConcurrencyConflict);
    }
}
