using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Features.Auctions;

namespace TakeAuction.Api.UnitTests.Common;

public static class TestHarness
{
    public static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    public static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"takeauction-tests-{Guid.CreateVersion7()}")
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    public static ICacheService CreateCacheService(IDistributedCache? backingCache = null)
    {
        backingCache ??= CreateDistributedCache();

        return new DistributedCacheService(backingCache, NullLogger<DistributedCacheService>.Instance);
    }

    public static IDistributedCache CreateDistributedCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    public static AuctionCache CreateAuctionCache(ICacheService cacheService) =>
        new(cacheService, Options.Create(new CacheOptions()));
}
