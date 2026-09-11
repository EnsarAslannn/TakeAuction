using System.Data.Common;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Watchlist.RemindWatchers;

[DisableConcurrentExecution(timeoutInSeconds: 300)]
[AutomaticRetry(Attempts = 0)]
public sealed class RemindWatchersJob
{
    private const string ClaimDueSql = """
        WITH due AS (
            SELECT w."UserId", w."AuctionId"
            FROM auction_watches AS w
            JOIN auctions AS a ON a."Id" = w."AuctionId"
            WHERE w."ClosingSoonNotifiedAtUtc" IS NULL
              AND a."Status" = @activeStatus
              AND a."EndsAtUtc" > @now
              AND a."EndsAtUtc" <= @horizon
            ORDER BY a."EndsAtUtc"
            LIMIT @batchSize
            FOR UPDATE OF w SKIP LOCKED
        )
        UPDATE auction_watches AS w
        SET "ClosingSoonNotifiedAtUtc" = @now
        FROM due
        JOIN auctions AS a ON a."Id" = due."AuctionId"
        WHERE w."UserId" = due."UserId"
          AND w."AuctionId" = due."AuctionId"
        RETURNING w."UserId", w."AuctionId", a."Title", a."EndsAtUtc"
        """;

    private readonly AppDbContext _dbContext;
    private readonly IOutbox _outbox;
    private readonly OutboxSignal _outboxSignal;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<RemindWatchersJob> _logger;

    public RemindWatchersJob(
        AppDbContext dbContext,
        IOutbox outbox,
        OutboxSignal outboxSignal,
        TimeProvider timeProvider,
        IOptions<JobOptions> options,
        ILogger<RemindWatchersJob> logger)
    {
        _dbContext = dbContext;
        _outbox = outbox;
        _outboxSignal = outboxSignal;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        var reminded = await strategy.ExecuteAsync(ClaimAndQueueAsync, cancellationToken);

        if (reminded > 0)
        {
            _outboxSignal.Notify();

            _logger.LogInformation("Queued {Reminded} closing-soon reminder(s) for watched lots", reminded);
        }

        return reminded;
    }

    private async Task<int> ClaimAndQueueAsync(CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();

        var now = _timeProvider.GetUtcNow();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var due = await ClaimDueAsync(now, transaction.GetDbTransaction(), cancellationToken);

        foreach (var watch in due)
        {
            _outbox.Enqueue(
                new WatchedAuctionClosingSoonIntegrationEvent(
                    watch.UserId,
                    watch.AuctionId,
                    watch.Title,
                    watch.EndsAtUtc,
                    now),
                now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return due.Count;
    }

    private async Task<IReadOnlyList<DueWatch>> ClaimDueAsync(
        DateTimeOffset now,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var options = _options.Value;

        await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = ClaimDueSql;
        command.Transaction = transaction;

        AddParameter(command, "activeStatus", nameof(AuctionStatus.Active));
        AddParameter(command, "now", now);
        AddParameter(command, "horizon", now.AddMinutes(options.RemindWatchersLeadMinutes));
        AddParameter(command, "batchSize", options.RemindWatchersBatchSize);

        var due = new List<DueWatch>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            due.Add(new DueWatch(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return due;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;

        command.Parameters.Add(parameter);
    }

    private sealed record DueWatch(Guid UserId, Guid AuctionId, string Title, DateTimeOffset EndsAtUtc);
}
