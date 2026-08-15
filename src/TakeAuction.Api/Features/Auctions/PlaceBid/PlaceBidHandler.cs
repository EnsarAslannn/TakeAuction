using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Observability;
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
    private readonly TakeAuctionTelemetry _telemetry;
    private readonly ILogger<PlaceBidHandler> _logger;

    public PlaceBidHandler(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IPublisher publisher,
        IOutbox outbox,
        TakeAuctionTelemetry telemetry,
        ILogger<PlaceBidHandler> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _publisher = publisher;
        _outbox = outbox;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<PlaceBidResult> Handle(PlaceBidCommand command, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var attempts = 0;

        using var activity = TakeAuctionTelemetry.Source.StartActivity("PlaceBid");
        activity?.SetTag("takeauction.auction.id", command.AuctionId);
        activity?.SetTag("takeauction.bidder.id", command.BidderId);

        var result = await SettleAsync(command, count => attempts = count, cancellationToken);

        activity?.SetTag("takeauction.bid.outcome", Describe(result));

        _telemetry.BidSettled(
            Describe(result),
            attempts,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return result;
    }

    private static string Describe(PlaceBidResult result) => result switch
    {
        { Replayed: true } => "replayed",
        { Succeeded: true } => "accepted",
        _ => result.Rejection.ToString()
    };

    private async Task<PlaceBidResult> SettleAsync(
        PlaceBidCommand command,
        Action<int> reportAttempts,
        CancellationToken cancellationToken)
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
            reportAttempts(attempt);

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
                return PlaceBidResult.Rejected(
                    outcome.Rejection,
                    auction.MinimumAcceptableBidFor(command.BidderId));
            }

            var bid = outcome.Bid!;
            await _dbContext.Bids.AddAsync(bid, cancellationToken);

            if (outcome.AutomaticBid is { } automaticBid)
            {
                await _dbContext.Bids.AddAsync(automaticBid, cancellationToken);
            }

            // The event reports the lot, not the submission: when a proxy answered, the price
            // on every watching screen is the leader's automatic bid, not the one just sent.
            var priceSetter = outcome.PriceSetter!;
            var leaderChanged = auction.LeadingBidderId != outbidBidderId;

            _outbox.Enqueue(
                new BidPlacedIntegrationEvent(
                    auction.Id,
                    auction.Title,
                    priceSetter.Id,
                    priceSetter.BidderId,
                    priceSetter.Amount,
                    priceSetter.IsAutomatic,
                    previousPrice,
                    leaderChanged ? outbidBidderId : null,
                    auction.EndsAtUtc,
                    outcome.Extended,
                    priceSetter.PlacedAtUtc),
                priceSetter.PlacedAtUtc);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _telemetry.BidConflicted();

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

            if (outcome.AutomaticBid is not null)
            {
                _telemetry.ProxyAnswered();
            }

            if (outcome.Extended)
            {
                _telemetry.AuctionExtended();
            }

            _logger.LogInformation(
                "Bid {BidId} up to {MaxAmount} accepted on auction {AuctionId} after {Attempt} attempt(s); the lot stands at {CurrentPrice} to {LeaderId}",
                bid.Id,
                bid.MaxAmount,
                auction.Id,
                attempt,
                auction.CurrentPrice,
                auction.LeadingBidderId);

            await _publisher.Publish(
                new BidPlacedEvent(
                    auction.Id,
                    priceSetter.Id,
                    priceSetter.BidderId,
                    priceSetter.Amount,
                    priceSetter.IsAutomatic,
                    previousPrice,
                    leaderChanged ? outbidBidderId : null,
                    auction.EndsAtUtc,
                    outcome.Extended,
                    priceSetter.PlacedAtUtc),
                cancellationToken);

            return PlaceBidResult.Accepted(new PlaceBidResponse(
                bid.Id,
                auction.Id,
                bid.Amount,
                bid.MaxAmount,
                auction.CurrentPrice,
                auction.MinimumAcceptableBidFor(command.BidderId),
                auction.BidCount,
                auction.LeadingBidderId == command.BidderId,
                outcome.AutomaticBid is not null,
                bid.PlacedAtUtc,
                auction.EndsAtUtc,
                outcome.Extended));
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
            bid.MaxAmount,
            auction.CurrentPrice,
            auction.MinimumAcceptableBidFor(bidderId),
            auction.BidCount,
            auction.LeadingBidderId == bidderId,
            AnsweredByProxy: false,
            bid.PlacedAtUtc,
            auction.EndsAtUtc,
            AuctionExtended: false));
    }

    private static string? Normalize(string? idempotencyKey) =>
        string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
