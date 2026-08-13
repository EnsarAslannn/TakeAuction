using Hangfire;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;

namespace TakeAuction.Api.Features.Auth.PurgeExpiredRefreshTokens;

public sealed class PurgeExpiredRefreshTokensRecurringJob : IRecurringJobRegistration
{
    public const string JobId = "auth:purge-refresh-tokens";

    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<PurgeExpiredRefreshTokensRecurringJob> _logger;

    public PurgeExpiredRefreshTokensRecurringJob(
        IOptions<JobOptions> options,
        ILogger<PurgeExpiredRefreshTokensRecurringJob> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Register(IRecurringJobManager manager)
    {
        var cron = _options.Value.PurgeRefreshTokensCron;

        manager.AddOrUpdate<PurgeExpiredRefreshTokensJob>(
            JobId,
            job => job.RunAsync(CancellationToken.None),
            cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        _logger.LogInformation("Recurring job {JobId} registered with cron '{Cron}' (UTC)", JobId, cron);
    }
}
