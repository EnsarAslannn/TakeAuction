using Hangfire;

namespace TakeAuction.Api.Features.Auctions.OpenAuctions;

[AutomaticRetry(Attempts = 0)]
public sealed class OpenAuctionJob
{
    private readonly AuctionOpener _opener;
    private readonly ILogger<OpenAuctionJob> _logger;

    public OpenAuctionJob(AuctionOpener opener, ILogger<OpenAuctionJob> logger)
    {
        _opener = opener;
        _logger = logger;
    }

    public async Task RunAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        if (await _opener.TryOpenAsync(auctionId, cancellationToken))
        {
            return;
        }

        _logger.LogDebug(
            "Scheduled open for auction {AuctionId} found nothing to do; the sweep will pick it up if it is still due",
            auctionId);
    }
}
