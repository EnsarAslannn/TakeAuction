using Hangfire;

namespace TakeAuction.Api.Features.Auctions.ExpireAuctions;

public interface IAuctionCloseSchedule
{
    void ScheduleClose(Guid auctionId, DateTimeOffset endsAtUtc, DateTimeOffset nowUtc);
}

/// <summary>
/// Books a lot's close for the second it is due, so a lot does not sit sold-but-open waiting
/// for the next sweep. Nothing is ever cancelled: when a late bid moves the close, a second
/// job is simply booked for the new time, and the one already in the queue arrives early,
/// finds the lot not due, and costs a single indexed read. Cancelling would mean tracking a
/// job id per auction and getting it right under contention, to save that read.
/// </summary>
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
