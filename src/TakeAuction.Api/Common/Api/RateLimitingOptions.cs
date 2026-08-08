namespace TakeAuction.Api.Common.Api;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; init; } = 100;

    public int WindowSeconds { get; init; } = 60;

    public int QueueLimit { get; init; } = 0;

    public int AuthPermitLimit { get; init; } = 5;

    public int AuthWindowSeconds { get; init; } = 60;
}
