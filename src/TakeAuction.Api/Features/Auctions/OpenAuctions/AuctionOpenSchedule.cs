using Hangfire;

namespace TakeAuction.Api.Features.Auctions.OpenAuctions;

public interface IAuctionOpenSchedule
{
    void ScheduleOpen(Guid auctionId, DateTimeOffset startsAtUtc, DateTimeOffset nowUtc);
}

public sealed class AuctionOpenSchedule : IAuctionOpenSchedule
{
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<AuctionOpenSchedule> _logger;

    public AuctionOpenSchedule(IBackgroundJobClient jobs, ILogger<AuctionOpenSchedule> logger)
    {
        _jobs = jobs;
        _logger = logger;
    }

    public void ScheduleOpen(Guid auctionId, DateTimeOffset startsAtUtc, DateTimeOffset nowUtc)
    {
        var delay = startsAtUtc - nowUtc;

        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        _jobs.Schedule<OpenAuctionJob>(job => job.RunAsync(auctionId, CancellationToken.None), delay);

        _logger.LogDebug(
            "Auction {AuctionId} booked to open in {Delay} at {StartsAtUtc}",
            auctionId,
            delay,
            startsAtUtc);
    }
}
