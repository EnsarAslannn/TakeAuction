namespace TakeAuction.Api.Common.Jobs;

public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    public bool ServerEnabled { get; set; } = true;

    public bool DashboardEnabled { get; set; }

    public string SchemaName { get; set; } = "hangfire";

    public int WorkerCount { get; set; } = 4;

    public int QueuePollIntervalSeconds { get; set; } = 5;

    public string ExpireAuctionsCron { get; set; } = "* * * * *";

    public int ExpireAuctionsBatchSize { get; set; } = 200;

    public string PurgeRefreshTokensCron { get; set; } = "0 3 * * *";

    public string PurgeOutboxCron { get; set; } = "15 3 * * *";

    public int RefreshTokenRetentionDays { get; set; } = 7;
}
