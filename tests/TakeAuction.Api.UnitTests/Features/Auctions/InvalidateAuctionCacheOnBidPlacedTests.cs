using Microsoft.Extensions.Logging.Abstractions;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.Features.Auctions.PlaceBid;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class InvalidateAuctionCacheOnBidPlacedTests
{
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    private readonly ICacheService _cache = TestHarness.CreateCacheService();
    private readonly AuctionCache _auctionCache;
    private readonly InvalidateAuctionCacheOnBidPlaced _handler;

    public InvalidateAuctionCacheOnBidPlacedTests()
    {
        _auctionCache = TestHarness.CreateAuctionCache(_cache);
        _handler = new InvalidateAuctionCacheOnBidPlaced(
            _auctionCache,
            NullLogger<InvalidateAuctionCacheOnBidPlaced>.Instance);
    }

    [Fact]
    public async Task Drops_the_cached_auction_detail()
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

        var after = await _auctionCache.GetListGenerationAsync(CancellationToken.None);
        Assert.NotEqual(before, after);
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
        100m,
        100m,
        5m,
        100m,
        0,
        "Active",
        TestHarness.Now,
        TestHarness.Now.AddDays(2),
        TestHarness.Now,
        Guid.CreateVersion7(),
        "Demo Seller");

    private static BidPlacedEvent Event() => new(
        AuctionId,
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        150m,
        100m,
        null,
        TestHarness.Now.AddDays(2),
        TestHarness.Now);
}
