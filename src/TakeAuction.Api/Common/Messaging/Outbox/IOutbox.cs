namespace TakeAuction.Api.Common.Messaging.Outbox;

public interface IOutbox
{
    void Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, DateTimeOffset occurredAtUtc)
        where TIntegrationEvent : class;
}
