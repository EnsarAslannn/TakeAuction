using Hangfire;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;

namespace TakeAuction.Api.Features.Auctions.OpenAuctions;

public sealed class ActivateAuctionsRecurringJob : IRecurringJobRegistration
{
    public const string JobId = "auctions:activate";

    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<ActivateAuctionsRecurringJob> _logger;

    public ActivateAuctionsRecurringJob(
        IOptions<JobOptions> options,
        ILogger<ActivateAuctionsRecurringJob> logger)
    {
        _options = options;
        _logger = logger;
    }

    public void Register(IRecurringJobManager manager)
    {
        var cron = _options.Value.ActivateAuctionsCron;

        manager.AddOrUpdate<ActivateAuctionsJob>(
            JobId,
            job => job.RunAsync(CancellationToken.None),
            cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        _logger.LogInformation("Recurring job {JobId} registered with cron '{Cron}' (UTC)", JobId, cron);
    }
}
