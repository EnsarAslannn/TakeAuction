using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.ExpireAuctions;

[DisableConcurrentExecution(timeoutInSeconds: 300)]
[AutomaticRetry(Attempts = 0)]
public sealed class ExpireAuctionsJob
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IPublisher _publisher;
    private readonly IOutbox _outbox;
    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<ExpireAuctionsJob> _logger;

    public ExpireAuctionsJob(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IPublisher publisher,
        IOutbox outbox,
        IOptions<JobOptions> options,
        ILogger<ExpireAuctionsJob> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _publisher = publisher;
        _outbox = outbox;
        _options = options;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var dueAuctionIds = await _dbContext.Auctions
            .Where(auction =>
                auction.EndsAtUtc <= now
                && (auction.Status == AuctionStatus.Scheduled || auction.Status == AuctionStatus.Active))
            .OrderBy(auction => auction.EndsAtUtc)
            .Take(_options.Value.ExpireAuctionsBatchSize)
            .Select(auction => auction.Id)
            .ToListAsync(cancellationToken);

        if (dueAuctionIds.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Auction expiry sweep found {DueCount} auction(s) past their end time", dueAuctionIds.Count);

        var endedCount = 0;

        foreach (var auctionId in dueAuctionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await TryEndAsync(auctionId, now, cancellationToken))
            {
                endedCount++;
            }
        }

        _logger.LogInformation("Auction expiry sweep closed {EndedCount} of {DueCount} auction(s)", endedCount, dueAuctionIds.Count);

        return endedCount;
    }

    private async Task<bool> TryEndAsync(Guid auctionId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();

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
                "Auction {AuctionId} changed while it was being closed; leaving it for the next sweep",
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
