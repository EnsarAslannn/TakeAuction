using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.ExpireAuctions;

[DisableConcurrentExecution(timeoutInSeconds: 300)]
[AutomaticRetry(Attempts = 0)]
public sealed class ExpireAuctionsJob
{
    private readonly AppDbContext _dbContext;
    private readonly AuctionCloser _closer;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<ExpireAuctionsJob> _logger;

    public ExpireAuctionsJob(
        AppDbContext dbContext,
        AuctionCloser closer,
        TimeProvider timeProvider,
        IOptions<JobOptions> options,
        ILogger<ExpireAuctionsJob> logger)
    {
        _dbContext = dbContext;
        _closer = closer;
        _timeProvider = timeProvider;
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

            if (await _closer.TryCloseAsync(auctionId, cancellationToken))
            {
                endedCount++;
            }
        }

        _logger.LogInformation("Auction expiry sweep closed {EndedCount} of {DueCount} auction(s)", endedCount, dueAuctionIds.Count);

        return endedCount;
    }
}
