using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Common.Messaging.Outbox;

public sealed class OutboxTests : IDisposable
{
    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();
    private readonly IOutbox _outbox;

    public OutboxTests() => _outbox = TestHarness.CreateOutbox(_dbContext);

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Writes_the_message_as_part_of_the_caller_s_own_save()
    {
        _outbox.Enqueue(Event(), TestHarness.Now);

        Assert.Empty(await _dbContext.OutboxMessages.ToListAsync());

        await _dbContext.SaveChangesAsync();

        Assert.Single(await _dbContext.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Stores_the_contract_name_the_dispatcher_resolves_by()
    {
        _outbox.Enqueue(Event(), TestHarness.Now);
        await _dbContext.SaveChangesAsync();

        var message = await _dbContext.OutboxMessages.SingleAsync();

        Assert.Equal(nameof(BidPlacedIntegrationEvent), message.Type);
    }

    [Fact]
    public async Task Round_trips_the_payload_without_losing_a_field()
    {
        var original = Event();

        _outbox.Enqueue(original, TestHarness.Now);
        await _dbContext.SaveChangesAsync();

        var stored = await _dbContext.OutboxMessages.SingleAsync();
        var restored = JsonSerializer.Deserialize<BidPlacedIntegrationEvent>(
            stored.Payload,
            Api.Common.Messaging.Outbox.Outbox.SerializerOptions);

        Assert.Equal(original, restored);
    }

    [Fact]
    public async Task Records_when_the_event_happened_rather_than_when_it_is_published()
    {
        var occurredAt = TestHarness.Now.AddMinutes(-5);

        _outbox.Enqueue(Event(), occurredAt);
        await _dbContext.SaveChangesAsync();

        var message = await _dbContext.OutboxMessages.SingleAsync();

        Assert.Equal(occurredAt, message.OccurredAtUtc);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Equal(0, message.Attempts);
    }

    [Fact]
    public void Refuses_a_type_the_dispatcher_would_not_be_able_to_resolve()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _outbox.Enqueue(new NotAContract(Guid.CreateVersion7()), TestHarness.Now));
    }

    [Fact]
    public async Task Leaves_nothing_behind_when_the_caller_s_save_never_happens()
    {
        _outbox.Enqueue(Event(), TestHarness.Now);

        _dbContext.ChangeTracker.Clear();
        await _dbContext.SaveChangesAsync();

        Assert.Empty(await _dbContext.OutboxMessages.ToListAsync());
    }

    private static BidPlacedIntegrationEvent Event() => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        150m,
        100m,
        Guid.CreateVersion7(),
        TestHarness.Now.AddDays(2),
        false,
        TestHarness.Now);

    private sealed record NotAContract(Guid Id);
}
