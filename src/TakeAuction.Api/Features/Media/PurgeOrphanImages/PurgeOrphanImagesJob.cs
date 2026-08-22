using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Media.PurgeOrphanImages;

[DisableConcurrentExecution(timeoutInSeconds: 600)]
[AutomaticRetry(Attempts = 0)]
public sealed class PurgeOrphanImagesJob
{
    private readonly AppDbContext _dbContext;
    private readonly MediaStorage _storage;
    private readonly MediaOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PurgeOrphanImagesJob> _logger;

    public PurgeOrphanImagesJob(
        AppDbContext dbContext,
        MediaStorage storage,
        IOptions<MediaOptions> options,
        TimeProvider timeProvider,
        ILogger<PurgeOrphanImagesJob> logger)
    {
        _dbContext = dbContext;
        _storage = storage;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_storage.ImageRoot))
        {
            return 0;
        }

        var cutoff = _timeProvider.GetUtcNow().AddHours(-_options.OrphanRetentionHours);

        var referenced = await _dbContext.Auctions
            .AsNoTracking()
            .Where(auction => auction.ImageUrl != null)
            .Select(auction => auction.ImageUrl!)
            .ToListAsync(cancellationToken);

        var claimed = referenced
            .Select(url => url[(url.LastIndexOf('/') + 1)..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removed = 0;

        foreach (var path in Directory.EnumerateFiles(_storage.ImageRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(path);

            if (claimed.Contains(fileName))
            {
                continue;
            }

            if (File.GetLastWriteTimeUtc(path) > cutoff.UtcDateTime)
            {
                continue;
            }

            try
            {
                File.Delete(path);
                removed++;
            }
            catch (IOException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not delete orphaned upload {FileName}; it stays for the next sweep",
                    fileName);
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Not allowed to delete orphaned upload {FileName}; it stays for the next sweep",
                    fileName);
            }
        }

        if (removed > 0)
        {
            _logger.LogInformation(
                "Purged {Removed} uploaded image(s) that no auction claimed and were last written before {Cutoff}",
                removed,
                cutoff);
        }

        return removed;
    }
}
