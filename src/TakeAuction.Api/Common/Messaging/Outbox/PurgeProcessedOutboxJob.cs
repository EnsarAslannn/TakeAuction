using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Common.Messaging.Outbox;

/// <summary>
/// Every published message leaves its row behind, so a busy salon would grow the table by one
/// row per bid forever. Processed rows are kept for a window first: they are the only record
/// of what was actually handed to the broker when a downstream consumer starts disagreeing.
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 300)]
[AutomaticRetry(Attempts = 0)]
public sealed class PurgeProcessedOutboxJob
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<OutboxOptions> _options;
    private readonly ILogger<PurgeProcessedOutboxJob> _logger;

    public PurgeProcessedOutboxJob(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<OutboxOptions> options,
        ILogger<PurgeProcessedOutboxJob> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var cutoff = _timeProvider.GetUtcNow().AddHours(-_options.Value.RetentionHours);

        var removed = await _dbContext.OutboxMessages
            .Where(message => message.ProcessedAtUtc != null && message.ProcessedAtUtc <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
        {
            _logger.LogInformation(
                "Purged {Removed} outbox message(s) published before {Cutoff}",
                removed,
                cutoff);
        }

        return removed;
    }
}
