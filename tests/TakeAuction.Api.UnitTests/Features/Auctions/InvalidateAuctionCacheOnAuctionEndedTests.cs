using Microsoft.Extensions.Logging.Abstractions;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class InvalidateAuctionCacheOnAuctionEndedTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly ICacheService _cache = TestHarness.CreateCacheService();
    private readonly AuctionCache _auctionCache;
    private readonly InvalidateAuctionCacheOnAuctionEnded _handler;

    public InvalidateAuctionCacheOnAuctionEndedTests()
    {
        _auctionCache = TestHarness.CreateAuctionCache(_cache);
        _handler = new InvalidateAuctionCacheOnAuctionEnded(
            _auctionCache,
            NullLogger<InvalidateAuctionCacheOnAuctionEnded>.Instance);
    }

    [Fact]
    public async Task Drops_the_stale_detail_that_still_says_the_auction_is_open()
    {
        var key = AuctionCache.DetailKey(AuctionId);
        await _cache.SetAsync(key, StaleDetail(), TimeSpan.FromMinutes(5), CancellationToken.None);

        await _handler.Handle(Event(), CancellationToken.None);

        Assert.Null(await _cache.GetAsync<AuctionDetailResponse>(key, CancellationToken.None));
    }

    [Fact]
    public async Task Rotates_the_list_generation()
    {
        var before = await _auctionCache.GetListGenerationAsync(CancellationToken.None);

        await _handler.Handle(Event(), CancellationToken.None);

        Assert.NotEqual(before, await _auctionCache.GetListGenerationAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Leaves_other_auctions_cached()
    {
        var otherKey = AuctionCache.DetailKey(Guid.CreateVersion7());
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
        250m,
        5m,
        255m,
        3,
        "Active",
        TestHarness.Now.AddDays(-2),
        TestHarness.Now,
        TestHarness.Now.AddDays(-2),
        Guid.CreateVersion7(),
        "Demo Seller");

    private static AuctionEndedEvent Event() => new(
        AuctionId,
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        250m,
        4,
        TestHarness.Now,
        TestHarness.Now.AddSeconds(30));
}
