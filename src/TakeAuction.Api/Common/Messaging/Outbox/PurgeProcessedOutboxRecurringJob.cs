using Hangfire;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class PurgeProcessedOutboxRecurringJob : IRecurringJobRegistration
{
    public const string JobId = "messaging:purge-outbox";

    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<PurgeProcessedOutboxRecurringJob> _logger;

    public PurgeProcessedOutboxRecurringJob(
        IOptions<JobOptions> options,
        ILogger<PurgeProcessedOutboxRecurringJob> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Register(IRecurringJobManager manager)
    {
        var cron = _options.Value.PurgeOutboxCron;

        manager.AddOrUpdate<PurgeProcessedOutboxJob>(
            JobId,
            job => job.RunAsync(CancellationToken.None),
            cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        _logger.LogInformation("Recurring job {JobId} registered with cron '{Cron}' (UTC)", JobId, cron);
    }
}
