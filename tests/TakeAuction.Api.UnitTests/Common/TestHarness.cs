using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Observability;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Features.Auctions;

namespace TakeAuction.Api.UnitTests.Common;

public static class TestHarness
{
    public static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    public static AppDbContext CreateDbContext(
        string? databaseName = null,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"takeauction-tests-{Guid.CreateVersion7()}")
            .AddInterceptors(interceptors)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    public static ICacheService CreateCacheService(
        IDistributedCache? backingCache = null,
        TakeAuctionTelemetry? telemetry = null)
    {
        backingCache ??= CreateDistributedCache();

        return new DistributedCacheService(
            backingCache,
            telemetry ?? CreateTelemetry(),
            NullLogger<DistributedCacheService>.Instance);
    }

    public static TakeAuctionTelemetry CreateTelemetry() =>
        new(new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider()
            .GetRequiredService<IMeterFactory>());

    public static IDistributedCache CreateDistributedCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    public static AuctionCache CreateAuctionCache(ICacheService cacheService) =>
        new(cacheService, Options.Create(new CacheOptions()));

    public static IntegrationEventTypeRegistry CreateIntegrationEventTypeRegistry() =>
        new(typeof(AppDbContext).Assembly);

    public static IOutbox CreateOutbox(AppDbContext dbContext) =>
        new Outbox(dbContext, CreateIntegrationEventTypeRegistry());
}
