namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool DispatcherEnabled { get; set; } = true;

    public int BatchSize { get; set; } = 50;

    public int PollIntervalSeconds { get; set; } = 10;

    public int MaxAttempts { get; set; } = 10;

    /// <summary>
    /// How long a claimed message stays invisible to other dispatchers. It doubles as the
    /// backoff on a failed publish, and as the recovery delay when the instance that claimed
    /// the message dies before it can finish.
    /// </summary>
    public int ClaimLeaseSeconds { get; set; } = 60;

    public int RetentionHours { get; set; } = 24;
}
