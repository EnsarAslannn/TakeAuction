using Hangfire;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;

namespace TakeAuction.Api.Features.Auctions.ExpireAuctions;

public sealed class ExpireAuctionsRecurringJob : IRecurringJobRegistration
{
    public const string JobId = "auctions:expire";

    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<ExpireAuctionsRecurringJob> _logger;

    public ExpireAuctionsRecurringJob(
        IOptions<JobOptions> options,
        ILogger<ExpireAuctionsRecurringJob> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Register(IRecurringJobManager manager)
    {
        var cron = _options.Value.ExpireAuctionsCron;

        manager.AddOrUpdate<ExpireAuctionsJob>(
            JobId,
            job => job.RunAsync(CancellationToken.None),
            cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        _logger.LogInformation("Recurring job {JobId} registered with cron '{Cron}' (UTC)", JobId, cron);
    }
}
