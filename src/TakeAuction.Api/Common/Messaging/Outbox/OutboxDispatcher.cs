using System.Data.Common;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class OutboxDispatcher
{
    /// <summary>
    /// Claiming takes a lease rather than only a row lock. SKIP LOCKED alone stops two
    /// dispatchers colliding inside the same statement, but the lock dies with the statement,
    /// and the row stays unprocessed for as long as the publish takes — long enough for the
    /// next instance to pick it up and send it a second time. The lease keeps the row
    /// invisible until the claimer has had its turn, and expires on its own so a dispatcher
    /// that dies mid-publish does not strand the message.
    ///
    /// The attempt is counted at claim time, not after a failure: a process that dies without
    /// reporting anything still burns one, so a message that reliably kills its handler
    /// eventually falls out of the batch instead of blocking the queue forever.
    /// </summary>
    private const string ClaimSql = """
        WITH claimed AS (
            SELECT "Id"
            FROM outbox_messages
            WHERE "ProcessedAtUtc" IS NULL
              AND "Attempts" < @maxAttempts
              AND ("ClaimedUntilUtc" IS NULL OR "ClaimedUntilUtc" <= @now)
            ORDER BY "OccurredAtUtc"
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
        )
        UPDATE outbox_messages AS m
        SET "Attempts" = m."Attempts" + 1, "ClaimedUntilUtc" = @leaseUntil
        FROM claimed
        WHERE m."Id" = claimed."Id"
        RETURNING m."Id", m."Type", m."Payload", m."Attempts"
        """;

    private const string MarkProcessedSql =
        """
        UPDATE outbox_messages
        SET "ProcessedAtUtc" = {0}, "ClaimedUntilUtc" = NULL, "LastError" = NULL
        WHERE "Id" = ANY({1})
        """;

    private const string RecordFailureSql =
        """UPDATE outbox_messages SET "LastError" = {0} WHERE "Id" = {1}""";

    private readonly AppDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IntegrationEventTypeRegistry _registry;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        AppDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        IntegrationEventTypeRegistry registry,
        TimeProvider timeProvider,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _registry = registry;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<OutboxSweep> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var batchSize = _options.Value.BatchSize;
        var claimed = await ClaimAsync(batchSize, cancellationToken);

        if (claimed.Count == 0)
        {
            return new OutboxSweep(0, 0, false);
        }

        var published = new List<Guid>(claimed.Count);

        foreach (var message in claimed)
        {
            if (await TryPublishAsync(message, cancellationToken))
            {
                published.Add(message.Id);
            }
        }

        if (published.Count > 0)
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
                MarkProcessedSql,
                [_timeProvider.GetUtcNow(), published.ToArray()],
                cancellationToken);
        }

        _logger.LogInformation(
            "Outbox sweep published {Published} of {Claimed} claimed message(s)",
            published.Count,
            claimed.Count);

        return new OutboxSweep(claimed.Count, published.Count, claimed.Count == batchSize);
    }

    private async Task<bool> TryPublishAsync(ClaimedMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var contract = _registry.Resolve(message.Type);
            var integrationEvent = JsonSerializer.Deserialize(message.Payload, contract, Outbox.SerializerOptions)
                ?? throw new InvalidOperationException($"Outbox payload for '{message.Type}' deserialised to null.");

            await _publishEndpoint.Publish(integrationEvent, contract, cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordFailureAsync(message, ex, cancellationToken);

            return false;
        }
    }

    private async Task RecordFailureAsync(
        ClaimedMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var exhausted = message.Attempts >= _options.Value.MaxAttempts;

        _logger.Log(
            exhausted ? LogLevel.Error : LogLevel.Warning,
            exception,
            exhausted
                ? "Outbox message {MessageId} of type {MessageType} failed on its final attempt {Attempts} and will no longer be retried"
                : "Outbox message {MessageId} of type {MessageType} failed on attempt {Attempts}; it stays queued until its claim lease runs out",
            message.Id,
            message.Type,
            message.Attempts);

        var reason = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message;

        await _dbContext.Database.ExecuteSqlRawAsync(
            RecordFailureSql,
            [reason, message.Id],
            cancellationToken);
    }

    private async Task<IReadOnlyList<ClaimedMessage>> ClaimAsync(int batchSize, CancellationToken cancellationToken)
    {
        var claimed = new List<ClaimedMessage>(batchSize);

        await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = ClaimSql;
            command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

            var now = _timeProvider.GetUtcNow();

            AddParameter(command, "maxAttempts", _options.Value.MaxAttempts);
            AddParameter(command, "batchSize", batchSize);
            AddParameter(command, "now", now);
            AddParameter(command, "leaseUntil", now.AddSeconds(_options.Value.ClaimLeaseSeconds));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                claimed.Add(new ClaimedMessage(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3)));
            }
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }

        return claimed;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;

        command.Parameters.Add(parameter);
    }

    private sealed record ClaimedMessage(Guid Id, string Type, string Payload, int Attempts);
}

public readonly record struct OutboxSweep(int Claimed, int Published, bool MoreLikely);
