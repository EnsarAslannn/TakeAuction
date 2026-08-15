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
    public async Task Moves_the_cached_auction_detail_out_of_reach()
    {
        var stale = AuctionCache.DetailKey(
            AuctionId,
            await _auctionCache.GetDetailGenerationAsync(AuctionId, CancellationToken.None));

        await _cache.SetAsync(stale, StaleDetail(), TimeSpan.FromMinutes(5), CancellationToken.None);

        await _handler.Handle(Event(), CancellationToken.None);

        // The old entry is left to expire on its own. What matters is that the key readers
        // now compute is a different one, and it is empty.
        var current = AuctionCache.DetailKey(
            AuctionId,
            await _auctionCache.GetDetailGenerationAsync(AuctionId, CancellationToken.None));

        Assert.NotEqual(stale, current);
        Assert.Null(await _cache.GetAsync<AuctionDetailResponse>(current, CancellationToken.None));
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
        false,
        100m,
        null,
        TestHarness.Now.AddDays(2),
        false,
        TestHarness.Now);
}
