using Hangfire;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;

namespace TakeAuction.Api.Features.Watchlist.RemindWatchers;

public sealed class RemindWatchersRecurringJob : IRecurringJobRegistration
{
    public const string JobId = "watchlist:remind";

    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<RemindWatchersRecurringJob> _logger;

    public RemindWatchersRecurringJob(
        IOptions<JobOptions> options,
        ILogger<RemindWatchersRecurringJob> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Register(IRecurringJobManager manager)
    {
        var cron = _options.Value.RemindWatchersCron;

        manager.AddOrUpdate<RemindWatchersJob>(
            JobId,
            job => job.RunAsync(CancellationToken.None),
            cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        _logger.LogInformation("Recurring job {JobId} registered with cron '{Cron}' (UTC)", JobId, cron);
    }
}
