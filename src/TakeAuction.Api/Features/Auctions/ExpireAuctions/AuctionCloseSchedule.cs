using Hangfire;

namespace TakeAuction.Api.Features.Auctions.ExpireAuctions;

public interface IAuctionCloseSchedule
{
    void ScheduleClose(Guid auctionId, DateTimeOffset endsAtUtc, DateTimeOffset nowUtc);
}

public sealed class AuctionCloseSchedule : IAuctionCloseSchedule
{
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<AuctionCloseSchedule> _logger;

    public AuctionCloseSchedule(IBackgroundJobClient jobs, ILogger<AuctionCloseSchedule> logger)
    {
        _jobs = jobs;
        _logger = logger;
    }

    public void ScheduleClose(Guid auctionId, DateTimeOffset endsAtUtc, DateTimeOffset nowUtc)
    {
        var delay = endsAtUtc - nowUtc;

        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        _jobs.Schedule<CloseAuctionJob>(job => job.RunAsync(auctionId, CancellationToken.None), delay);

        _logger.LogDebug(
            "Auction {AuctionId} booked to close in {Delay} at {EndsAtUtc}",
            auctionId,
            delay,
            endsAtUtc);
    }
}
