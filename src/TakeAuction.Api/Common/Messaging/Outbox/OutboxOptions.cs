namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool DispatcherEnabled { get; set; } = true;

    public int BatchSize { get; set; } = 50;

    public int PollIntervalSeconds { get; set; } = 10;

    public int MaxAttempts { get; set; } = 10;

    public int ClaimLeaseSeconds { get; set; } = 60;

    public int RetentionHours { get; set; } = 24;
}
