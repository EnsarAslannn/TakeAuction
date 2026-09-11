using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Observability;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Common.Messaging.Outbox;

public sealed class OutboxBacklogProbe
{
    private const string BacklogSql = """
        SELECT
            count(*) FILTER (WHERE "Attempts" < @maxAttempts),
            count(*) FILTER (WHERE "Attempts" >= @maxAttempts),
            min("OccurredAtUtc") FILTER (WHERE "Attempts" < @maxAttempts)
        FROM outbox_messages
        WHERE "ProcessedAtUtc" IS NULL
        """;

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<OutboxOptions> _options;

    public OutboxBacklogProbe(AppDbContext dbContext, TimeProvider timeProvider, IOptions<OutboxOptions> options)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options;
    }

    public async Task<OutboxBacklog> MeasureAsync(CancellationToken cancellationToken)
    {
        await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = BacklogSql;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "maxAttempts";
            parameter.Value = _options.Value.MaxAttempts;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            return new OutboxBacklog(
                reader.GetInt64(0),
                reader.GetInt64(1),
                AgeOf(reader, 2));
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }

    private double AgeOf(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        var oldest = reader.GetFieldValue<DateTimeOffset>(ordinal);

        return Math.Max(0, (_timeProvider.GetUtcNow() - oldest).TotalSeconds);
    }
}
