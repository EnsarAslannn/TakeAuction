using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Common.Messaging;

[Collection(IntegrationTestCollection.Name)]
public sealed class OutboxDispatcherTests : IAsyncLifetime
{
    private const string InsertSql =
        """
        INSERT INTO outbox_messages
            ("Id", "Type", "Payload", "OccurredAtUtc", "ProcessedAtUtc", "ClaimedUntilUtc", "Attempts", "LastError")
        VALUES (@id, @type, CAST(@payload AS jsonb), @occurredAt, NULL, @claimedUntil, @attempts, NULL)
        """;

    private readonly IntegrationTestFixture _fixture;

    public OutboxDispatcherTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Publishes_a_queued_message_and_stamps_it_processed()
    {
        var id = await QueueAsync(Event());

        var sweep = await DispatchAsync();

        Assert.Equal(1, sweep.Claimed);
        Assert.Equal(1, sweep.Published);

        var message = await FindAsync(id);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.LastError);
    }

    [Fact]
    public async Task Never_hands_the_same_message_to_two_dispatchers()
    {
        const int MessageCount = 40;

        for (var i = 0; i < MessageCount; i++)
        {
            await QueueAsync(Event());
        }

        var sweeps = await Task.WhenAll(DispatchAsync(), DispatchAsync(), DispatchAsync());

        Assert.Equal(MessageCount, sweeps.Sum(sweep => sweep.Published));

        var messages = await AllAsync();

        Assert.Equal(MessageCount, messages.Count);
        Assert.All(messages, message => Assert.NotNull(message.ProcessedAtUtc));

        // One claim each is the whole point of SKIP LOCKED: a second dispatcher that saw a row
        // twice would have driven its attempt counter past one.
        Assert.All(messages, message => Assert.Equal(1, message.Attempts));
    }

    [Fact]
    public async Task Holds_off_a_message_another_dispatcher_still_has_a_lease_on()
    {
        var leased = await QueueRawAsync(
            nameof(BidPlacedIntegrationEvent),
            JsonSerializer.Serialize(Event(), Outbox.SerializerOptions),
            attempts: 1,
            claimedUntilUtc: DateTimeOffset.UtcNow.AddMinutes(5));

        var sweep = await DispatchAsync();

        Assert.Equal(0, sweep.Claimed);
        Assert.Equal(1, (await FindAsync(leased)).Attempts);
    }

    [Fact]
    public async Task Takes_back_a_message_whose_claimer_never_came_home()
    {
        var abandoned = await QueueRawAsync(
            nameof(BidPlacedIntegrationEvent),
            JsonSerializer.Serialize(Event(), Outbox.SerializerOptions),
            attempts: 1,
            claimedUntilUtc: DateTimeOffset.UtcNow.AddMinutes(-1));

        var sweep = await DispatchAsync();

        Assert.Equal(1, sweep.Published);

        var message = await FindAsync(abandoned);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.ClaimedUntilUtc);
        Assert.Equal(2, message.Attempts);
    }

    [Fact]
    public async Task Leaves_a_message_it_cannot_read_queued_and_records_why()
    {
        var id = await QueueRawAsync("AuctionVaporisedIntegrationEvent", """{"auctionId":"nonsense"}""");

        var sweep = await DispatchAsync();

        Assert.Equal(1, sweep.Claimed);
        Assert.Equal(0, sweep.Published);

        var message = await FindAsync(id);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Equal(1, message.Attempts);
        Assert.NotNull(message.LastError);

        // The lease is deliberately not released on failure: it is what stops a broker outage
        // from burning the whole attempt budget in the space of a few sweeps.
        Assert.NotNull(message.ClaimedUntilUtc);
    }

    [Fact]
    public async Task Stops_claiming_a_message_that_has_burned_its_attempts()
    {
        var maxAttempts = _fixture.Services.GetRequiredService<IOptions<OutboxOptions>>().Value.MaxAttempts;

        var poisoned = await QueueRawAsync("AuctionVaporisedIntegrationEvent", "{}", attempts: maxAttempts);
        var healthy = await QueueAsync(Event());

        var sweep = await DispatchAsync();

        Assert.Equal(1, sweep.Claimed);
        Assert.Equal(1, sweep.Published);

        Assert.NotNull((await FindAsync(healthy)).ProcessedAtUtc);

        var stuck = await FindAsync(poisoned);
        Assert.Null(stuck.ProcessedAtUtc);
        Assert.Equal(maxAttempts, stuck.Attempts);
    }

    [Fact]
    public async Task Reports_a_full_batch_so_the_caller_keeps_draining()
    {
        var batchSize = _fixture.Services.GetRequiredService<IOptions<OutboxOptions>>().Value.BatchSize;

        for (var i = 0; i < batchSize + 1; i++)
        {
            await QueueAsync(Event());
        }

        var first = await DispatchAsync();
        Assert.Equal(batchSize, first.Claimed);
        Assert.True(first.MoreLikely);

        var second = await DispatchAsync();
        Assert.Equal(1, second.Claimed);
        Assert.False(second.MoreLikely);
    }

    [Fact]
    public async Task Sweeps_an_empty_outbox_without_touching_the_broker()
    {
        var sweep = await DispatchAsync();

        Assert.Equal(0, sweep.Claimed);
        Assert.Equal(0, sweep.Published);
        Assert.False(sweep.MoreLikely);
    }

    private async Task<OutboxSweep> DispatchAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();

        return await dispatcher.DispatchBatchAsync(CancellationToken.None);
    }

    private Task<Guid> QueueAsync(BidPlacedIntegrationEvent integrationEvent) =>
        QueueRawAsync(
            nameof(BidPlacedIntegrationEvent),
            JsonSerializer.Serialize(integrationEvent, Outbox.SerializerOptions));

    private async Task<Guid> QueueRawAsync(
        string type,
        string payload,
        int attempts = 0,
        DateTimeOffset? claimedUntilUtc = null)
    {
        var id = Guid.CreateVersion7();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Inserted around EF rather than through it on purpose: SaveChanges would fire the
        // commit signal and let the hosted dispatcher race the test for the row.
        await dbContext.Database.ExecuteSqlRawAsync(
            InsertSql,
            new NpgsqlParameter("id", id),
            new NpgsqlParameter("type", type),
            new NpgsqlParameter("payload", payload),
            new NpgsqlParameter("occurredAt", DateTimeOffset.UtcNow),
            new NpgsqlParameter("claimedUntil", NpgsqlDbType.TimestampTz)
            {
                Value = (object?)claimedUntilUtc ?? DBNull.Value
            },
            new NpgsqlParameter("attempts", attempts));

        return id;
    }

    private Task<OutboxMessage> FindAsync(Guid id) =>
        _fixture.ExecuteDbContextAsync(dbContext => dbContext.OutboxMessages
            .AsNoTracking()
            .SingleAsync(message => message.Id == id));

    private Task<List<OutboxMessage>> AllAsync() =>
        _fixture.ExecuteDbContextAsync(dbContext => dbContext.OutboxMessages
            .AsNoTracking()
            .ToListAsync());

    private static BidPlacedIntegrationEvent Event() => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        150m,
        false,
        100m,
        null,
        DateTimeOffset.UtcNow.AddDays(2),
        false,
        DateTimeOffset.UtcNow);
}
