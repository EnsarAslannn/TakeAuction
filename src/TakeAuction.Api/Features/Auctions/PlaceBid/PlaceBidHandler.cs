using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        var idempotencyKey = Normalize(command.IdempotencyKey);

        if (idempotencyKey is not null)
        {
            var replay = await TryReplayAsync(command.BidderId, idempotencyKey, cancellationToken);

            if (replay is not null)
            {
                return replay;
            }
        }

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

            var outcome = auction.PlaceBid(
                command.BidderId,
                command.Amount,
                _timeProvider.GetUtcNow(),
                idempotencyKey);

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
            catch (DbUpdateException ex) when (idempotencyKey is not null && IsDuplicateKey(ex))
            {
                // The other copy of this request got there first. Its bid is the real one, so
                // this attempt hands back the same answer rather than raising the price twice.
                _dbContext.ChangeTracker.Clear();

                _logger.LogInformation(
                    "Bidder {BidderId} sent idempotency key {IdempotencyKey} twice at once; replaying the bid that won",
                    command.BidderId,
                    idempotencyKey);

                return await TryReplayAsync(command.BidderId, idempotencyKey, cancellationToken)
                    ?? PlaceBidResult.Rejected(BidRejection.ConcurrencyConflict);
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

    /// <summary>
    /// A replay answers with the bid that was actually recorded, but reads the auction as it
    /// stands now: the caller is retrying because it never saw the first answer, and telling it
    /// the price from a minute ago would be worse than useless on a live lot.
    /// </summary>
    private async Task<PlaceBidResult?> TryReplayAsync(
        Guid bidderId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var bid = await _dbContext.Bids
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.BidderId == bidderId && candidate.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (bid is null)
        {
            return null;
        }

        var auction = await _dbContext.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == bid.AuctionId, cancellationToken);

        if (auction is null)
        {
            return null;
        }

        _logger.LogInformation(
            "Replaying bid {BidId} for bidder {BidderId} under idempotency key {IdempotencyKey}",
            bid.Id,
            bidderId,
            idempotencyKey);

        return PlaceBidResult.Replay(new PlaceBidResponse(
            bid.Id,
            auction.Id,
            bid.Amount,
            auction.CurrentPrice,
            auction.MinimumAcceptableBid,
            auction.BidCount,
            bid.PlacedAtUtc));
    }

    private static string? Normalize(string? idempotencyKey) =>
        string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
