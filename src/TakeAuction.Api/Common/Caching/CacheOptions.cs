namespace TakeAuction.Api.Common.Caching;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public string? RedisConnectionString { get; set; }

    public string InstanceName { get; set; } = "takeauction:";

    public int AuctionListTtlSeconds { get; set; } = 30;

    public int AuctionDetailTtlSeconds { get; set; } = 60;
}
