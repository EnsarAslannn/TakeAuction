namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public DateTimeOffset? ClaimedUntilUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Queue(string type, string payload, DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Message type is required.", nameof(type));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Message payload is required.", nameof(payload));
        }

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Payload = payload,
            OccurredAtUtc = occurredAtUtc.ToUniversalTime()
        };
    }
}
