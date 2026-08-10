namespace TakeAuction.Api.Common.Jobs;

public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    public bool ServerEnabled { get; set; } = true;

    public string SchemaName { get; set; } = "hangfire";

    public int WorkerCount { get; set; } = 4;

    public int QueuePollIntervalSeconds { get; set; } = 5;

    public string ExpireAuctionsCron { get; set; } = "* * * * *";

    public int ExpireAuctionsBatchSize { get; set; } = 200;
}
