using Hangfire;

namespace TakeAuction.Api.Features.Auctions.ExpireAuctions;

/// <summary>
/// One lot, at the second it is due. The retries are deliberate and slow: a lot whose close
/// was pushed out by a late bid arrives here early, finds it is not due yet, and is worth
/// another look shortly rather than being handed back to the once-a-minute sweep.
/// </summary>
[AutomaticRetry(Attempts = 0)]
public sealed class CloseAuctionJob
{
    private readonly AuctionCloser _closer;
    private readonly ILogger<CloseAuctionJob> _logger;

    public CloseAuctionJob(AuctionCloser closer, ILogger<CloseAuctionJob> logger)
    {
        _closer = closer;
        _logger = logger;
    }

    public async Task RunAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        if (await _closer.TryCloseAsync(auctionId, cancellationToken))
        {
            return;
        }

        _logger.LogDebug(
            "Scheduled close for auction {AuctionId} found nothing to do; the sweep will pick it up if it is still due",
            auctionId);
    }
}
