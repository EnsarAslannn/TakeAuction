using Hangfire;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;

namespace TakeAuction.Api.Features.Media.PurgeOrphanImages;

public sealed class PurgeOrphanImagesRecurringJob : IRecurringJobRegistration
{
    public const string JobId = "media:purge-orphan-images";

    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<PurgeOrphanImagesRecurringJob> _logger;

    public PurgeOrphanImagesRecurringJob(
        IOptions<JobOptions> options,
        ILogger<PurgeOrphanImagesRecurringJob> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Register(IRecurringJobManager manager)
    {
        var cron = _options.Value.PurgeOrphanImagesCron;

        manager.AddOrUpdate<PurgeOrphanImagesJob>(
            JobId,
            job => job.RunAsync(CancellationToken.None),
            cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        _logger.LogInformation("Recurring job {JobId} registered with cron '{Cron}' (UTC)", JobId, cron);
    }
}
