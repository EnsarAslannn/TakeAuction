namespace TakeAuction.Api.Common.Jobs;

public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    public bool ServerEnabled { get; set; } = true;

    /// <summary>
    /// Serves the job dashboard. It is gated on an admin principal wherever it runs, so this
    /// switch is about whether the route exists at all — the answer for most deployments is
    /// no, and for the one where somebody needs to requeue a stuck job, yes.
    /// </summary>
    public bool DashboardEnabled { get; set; }

    public string SchemaName { get; set; } = "hangfire";

    public int WorkerCount { get; set; } = 4;

    public int QueuePollIntervalSeconds { get; set; } = 5;

    public string ExpireAuctionsCron { get; set; } = "* * * * *";

    public int ExpireAuctionsBatchSize { get; set; } = 200;

    public string PurgeRefreshTokensCron { get; set; } = "0 3 * * *";

    public string PurgeOutboxCron { get; set; } = "15 3 * * *";

    /// <summary>
    /// How long an expired refresh token is kept before it is deleted. Reuse of a stolen
    /// token is only detectable while its row still exists, so the sweep does not run tight
    /// against expiry.
    /// </summary>
    public int RefreshTokenRetentionDays { get; set; } = 7;
}
