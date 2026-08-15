using System.Text.Json;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class Outbox : IOutbox
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;
    private readonly IntegrationEventTypeRegistry _registry;

    public Outbox(AppDbContext dbContext, IntegrationEventTypeRegistry registry)
    {
        _dbContext = dbContext;
        _registry = registry;
    }

    public void Enqueue<TIntegrationEvent>(TIntegrationEvent integrationEvent, DateTimeOffset occurredAtUtc)
        where TIntegrationEvent : class
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var type = typeof(TIntegrationEvent);
        var name = IntegrationEventTypeRegistry.NameOf(type);

        _registry.Resolve(name);

        var payload = JsonSerializer.Serialize(integrationEvent, type, SerializerOptions);

        _dbContext.OutboxMessages.Add(OutboxMessage.Queue(name, payload, occurredAtUtc));
    }
}
