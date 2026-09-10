using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.OpenAuctions;

[DisableConcurrentExecution(timeoutInSeconds: 300)]
[AutomaticRetry(Attempts = 0)]
public sealed class ActivateAuctionsJob
{
    private readonly AppDbContext _dbContext;
    private readonly AuctionOpener _opener;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<ActivateAuctionsJob> _logger;

    public ActivateAuctionsJob(
        AppDbContext dbContext,
        AuctionOpener opener,
        TimeProvider timeProvider,
        IOptions<JobOptions> options,
        ILogger<ActivateAuctionsJob> logger)
    {
        _dbContext = dbContext;
        _opener = opener;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var dueAuctionIds = await _dbContext.Auctions
            .Where(auction =>
                auction.Status == AuctionStatus.Scheduled
                && auction.StartsAtUtc <= now
                && auction.EndsAtUtc > now)
            .OrderBy(auction => auction.StartsAtUtc)
            .Take(_options.Value.ActivateAuctionsBatchSize)
            .Select(auction => auction.Id)
            .ToListAsync(cancellationToken);

        if (dueAuctionIds.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation(
            "Auction opening sweep found {DueCount} auction(s) past their start time",
            dueAuctionIds.Count);

        var openedCount = 0;

        foreach (var auctionId in dueAuctionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await _opener.TryOpenAsync(auctionId, cancellationToken))
            {
                openedCount++;
            }
        }

        _logger.LogInformation(
            "Auction opening sweep opened {OpenedCount} of {DueCount} auction(s)",
            openedCount,
            dueAuctionIds.Count);

        return openedCount;
    }
}
