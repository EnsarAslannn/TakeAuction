using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Observability;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Common.Messaging;

[Collection(IntegrationTestCollection.Name)]
public sealed class OutboxBacklogTests : IAsyncLifetime
{
    private const string InsertSql =
        """
        INSERT INTO outbox_messages
            ("Id", "Type", "Payload", "OccurredAtUtc", "ProcessedAtUtc", "ClaimedUntilUtc", "Attempts", "LastError")
        VALUES (@id, 'BidPlacedIntegrationEvent', CAST(@payload AS jsonb), @occurredAt, @processedAt, NULL, @attempts, NULL)
        """;

    private readonly IntegrationTestFixture _fixture;

    public OutboxBacklogTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Reads_an_empty_outbox_as_no_backlog()
    {
        var backlog = await MeasureAsync();

        Assert.Equal(0, backlog.Pending);
        Assert.Equal(0, backlog.DeadLetters);
        Assert.Equal(0, backlog.OldestPendingAgeSeconds);
    }

    [Fact]
    public async Task Tells_rows_still_in_play_apart_from_rows_that_burned_their_attempts()
    {
        var maxAttempts = MaxAttempts();

        await InsertAsync(attempts: 0);
        await InsertAsync(attempts: maxAttempts - 1);
        await InsertAsync(attempts: maxAttempts);
        await InsertAsync(attempts: maxAttempts + 3);

        var backlog = await MeasureAsync();

        Assert.Equal(2, backlog.Pending);
        Assert.Equal(2, backlog.DeadLetters);
    }

    [Fact]
    public async Task Ignores_rows_that_already_reached_the_broker()
    {
        await InsertAsync(attempts: 1, processedAtUtc: DateTimeOffset.UtcNow);
        await InsertAsync(attempts: MaxAttempts(), processedAtUtc: DateTimeOffset.UtcNow);

        var backlog = await MeasureAsync();

        Assert.Equal(0, backlog.Pending);
        Assert.Equal(0, backlog.DeadLetters);
    }

    [Fact]
    public async Task Ages_the_backlog_by_its_oldest_row_still_in_play()
    {
        await InsertAsync(attempts: 0, occurredAtUtc: DateTimeOffset.UtcNow.AddMinutes(-5));
        await InsertAsync(attempts: 0, occurredAtUtc: DateTimeOffset.UtcNow.AddSeconds(-10));
        await InsertAsync(attempts: MaxAttempts(), occurredAtUtc: DateTimeOffset.UtcNow.AddDays(-3));

        var backlog = await MeasureAsync();

        Assert.InRange(backlog.OldestPendingAgeSeconds, 295, 330);
    }

    [Fact]
    public async Task The_sampler_publishes_what_the_probe_measured()
    {
        await InsertAsync(attempts: MaxAttempts());

        var sampler = _fixture.Services.GetRequiredService<OutboxBacklogSampler>();
        await sampler.SampleAsync(CancellationToken.None);

        var telemetry = _fixture.Services.GetRequiredService<TakeAuctionTelemetry>();
        double? deadLetters = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (ReferenceEquals(instrument.Meter, telemetry.Meter)
                && instrument.Name == "takeauction.outbox.dead_letters")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => deadLetters = value);
        listener.Start();
        listener.RecordObservableInstruments();

        Assert.Equal(1, deadLetters);
    }

    private int MaxAttempts() =>
        _fixture.Services.GetRequiredService<IOptions<OutboxOptions>>().Value.MaxAttempts;

    private async Task<OutboxBacklog> MeasureAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<OutboxBacklogProbe>()
            .MeasureAsync(CancellationToken.None);
    }

    private async Task InsertAsync(
        int attempts,
        DateTimeOffset? occurredAtUtc = null,
        DateTimeOffset? processedAtUtc = null)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            InsertSql,
            new NpgsqlParameter("id", Guid.CreateVersion7()),
            new NpgsqlParameter("payload", "{}"),
            new NpgsqlParameter("occurredAt", occurredAtUtc ?? DateTimeOffset.UtcNow),
            new NpgsqlParameter("processedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz)
            {
                Value = (object?)processedAtUtc ?? DBNull.Value
            },
            new NpgsqlParameter("attempts", attempts));
    }
}
