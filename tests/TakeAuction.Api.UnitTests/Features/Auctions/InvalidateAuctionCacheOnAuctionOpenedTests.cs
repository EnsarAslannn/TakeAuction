using Microsoft.Extensions.Logging.Abstractions;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.Features.Auctions.OpenAuctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class InvalidateAuctionCacheOnAuctionOpenedTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly ICacheService _cache = TestHarness.CreateCacheService();
    private readonly AuctionCache _auctionCache;
    private readonly InvalidateAuctionCacheOnAuctionOpened _handler;

    public InvalidateAuctionCacheOnAuctionOpenedTests()
    {
        _auctionCache = TestHarness.CreateAuctionCache(_cache);
        _handler = new InvalidateAuctionCacheOnAuctionOpened(
            _auctionCache,
            NullLogger<InvalidateAuctionCacheOnAuctionOpened>.Instance);
    }

    [Fact]
    public async Task Drops_the_stale_detail_that_still_says_the_lot_is_scheduled()
    {
        var stale = AuctionCache.DetailKey(
            AuctionId,
            await _auctionCache.GetDetailGenerationAsync(AuctionId, CancellationToken.None));

        await _cache.SetAsync(stale, StaleDetail(), TimeSpan.FromMinutes(5), CancellationToken.None);

        await _handler.Handle(Event(), CancellationToken.None);

        var current = AuctionCache.DetailKey(
            AuctionId,
            await _auctionCache.GetDetailGenerationAsync(AuctionId, CancellationToken.None));

        Assert.NotEqual(stale, current);
        Assert.Null(await _cache.GetAsync<AuctionDetailResponse>(current, CancellationToken.None));
    }

    [Fact]
    public async Task Rotates_the_list_generation_so_the_status_filter_sees_the_lot()
    {
        var before = await _auctionCache.GetListGenerationAsync(CancellationToken.None);

        await _handler.Handle(Event(), CancellationToken.None);

        Assert.NotEqual(before, await _auctionCache.GetListGenerationAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Leaves_other_auctions_cached()
    {
        var otherId = Guid.CreateVersion7();
        var otherKey = AuctionCache.DetailKey(
            otherId,
            await _auctionCache.GetDetailGenerationAsync(otherId, CancellationToken.None));
        await _cache.SetAsync(otherKey, StaleDetail(), TimeSpan.FromMinutes(5), CancellationToken.None);

        await _handler.Handle(Event(), CancellationToken.None);

        Assert.NotNull(await _cache.GetAsync<AuctionDetailResponse>(otherKey, CancellationToken.None));
    }

    private static AuctionDetailResponse StaleDetail() => new(
        AuctionId,
        "Rare stamp collection",
        "A detailed description of the lot on offer.",
        null,
        100m,
        100m,
        5m,
        100m,
        0,
        "Scheduled",
        TestHarness.Now,
        TestHarness.Now.AddDays(1),
        TestHarness.Now.AddDays(-2),
        Guid.CreateVersion7(),
        "Demo Seller");

    private static AuctionOpenedEvent Event() => new(
        AuctionId,
        Guid.CreateVersion7(),
        100m,
        TestHarness.Now,
        TestHarness.Now.AddDays(1),
        TestHarness.Now);
}
