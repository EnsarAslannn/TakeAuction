using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Auth.PurgeExpiredRefreshTokens;

/// <summary>
/// Rotation leaves one retired row behind per refresh, so the table would grow without bound.
/// Rows are kept for a grace period past expiry: a reused token is only detectable while the
/// row it points at still exists.
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 300)]
[AutomaticRetry(Attempts = 0)]
public sealed class PurgeExpiredRefreshTokensJob
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<PurgeExpiredRefreshTokensJob> _logger;

    public PurgeExpiredRefreshTokensJob(
        AppDbContext dbContext,
        TimeProvider timeProvider,
        IOptions<JobOptions> options,
        ILogger<PurgeExpiredRefreshTokensJob> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var cutoff = _timeProvider
            .GetUtcNow()
            .AddDays(-_options.Value.RefreshTokenRetentionDays);

        var removed = await _dbContext.RefreshTokens
            .Where(token => token.ExpiresAtUtc <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
        {
            _logger.LogInformation("Purged {Removed} refresh token(s) that expired before {Cutoff}", removed, cutoff);
        }

        return removed;
    }
}
