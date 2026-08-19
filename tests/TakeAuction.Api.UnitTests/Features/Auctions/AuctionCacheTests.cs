using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class AuctionCacheTests
{
    private readonly ICacheService _cache = TestHarness.CreateCacheService();
    private readonly AuctionCache _auctionCache;

    public AuctionCacheTests() => _auctionCache = TestHarness.CreateAuctionCache(_cache);

    [Fact]
    public async Task Generation_is_stable_until_it_is_rotated()
    {
        var first = await _auctionCache.GetListGenerationAsync(CancellationToken.None);
        var second = await _auctionCache.GetListGenerationAsync(CancellationToken.None);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Invalidation_rotates_the_generation()
    {
        var before = await _auctionCache.GetListGenerationAsync(CancellationToken.None);

        await _auctionCache.InvalidateListsAsync(CancellationToken.None);

        var after = await _auctionCache.GetListGenerationAsync(CancellationToken.None);
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task Rotating_the_generation_changes_every_list_key()
    {
        var before = await _auctionCache.GetListGenerationAsync(CancellationToken.None);
        await _auctionCache.InvalidateListsAsync(CancellationToken.None);
        var after = await _auctionCache.GetListGenerationAsync(CancellationToken.None);

        var keyBefore = AuctionCache.ListKey(before, 1, 20, null, null, null);
        var keyAfter = AuctionCache.ListKey(after, 1, 20, null, null, null);

        Assert.NotEqual(keyBefore, keyAfter);
    }

    [Fact]
    public void List_key_is_deterministic_for_identical_filters()
    {
        var sellerId = Guid.CreateVersion7();

        var first = AuctionCache.ListKey("gen", 2, 50, AuctionStatus.Active, sellerId, "Watch");
        var second = AuctionCache.ListKey("gen", 2, 50, AuctionStatus.Active, sellerId, "watch  ");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(2, 20, AuctionStatus.Active, "watch")]
    [InlineData(1, 50, AuctionStatus.Active, "watch")]
    [InlineData(1, 20, AuctionStatus.Ended, "watch")]
    [InlineData(1, 20, AuctionStatus.Active, "desk")]
    [InlineData(1, 20, null, "watch")]
    public void List_key_varies_with_every_filter(int page, int pageSize, AuctionStatus? status, string search)
    {
        var baseline = AuctionCache.ListKey("gen", 1, 20, AuctionStatus.Active, null, "watch");

        var candidate = AuctionCache.ListKey("gen", page, pageSize, status, null, search);

        Assert.NotEqual(baseline, candidate);
    }

    [Fact]
    public void List_key_varies_with_the_seller_filter()
    {
        var baseline = AuctionCache.ListKey("gen", 1, 20, null, null, null);

        var candidate = AuctionCache.ListKey("gen", 1, 20, null, Guid.CreateVersion7(), null);

        Assert.NotEqual(baseline, candidate);
    }

    [Fact]
    public void Detail_keys_are_scoped_to_the_auction_and_its_generation()
    {
        var auctionId = Guid.CreateVersion7();

        Assert.Equal($"auctions:detail:{auctionId}:g1", AuctionCache.DetailKey(auctionId, "g1"));
        Assert.NotEqual(AuctionCache.DetailKey(auctionId, "g1"), AuctionCache.DetailKey(Guid.CreateVersion7(), "g1"));
        Assert.NotEqual(AuctionCache.DetailKey(auctionId, "g1"), AuctionCache.DetailKey(auctionId, "g2"));
    }

    [Fact]
    public async Task Invalidating_the_detail_moves_readers_to_a_new_key()
    {
        var auctionId = Guid.CreateVersion7();

        var before = AuctionCache.DetailKey(
            auctionId,
            await _auctionCache.GetDetailGenerationAsync(auctionId, CancellationToken.None));

        await _auctionCache.InvalidateDetailAsync(auctionId, CancellationToken.None);

        var after = AuctionCache.DetailKey(
            auctionId,
            await _auctionCache.GetDetailGenerationAsync(auctionId, CancellationToken.None));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task A_detail_generation_is_stable_until_something_invalidates_it()
    {
        var auctionId = Guid.CreateVersion7();

        var first = await _auctionCache.GetDetailGenerationAsync(auctionId, CancellationToken.None);
        var second = await _auctionCache.GetDetailGenerationAsync(auctionId, CancellationToken.None);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Detail_generations_are_independent_between_auctions()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        var untouched = await _auctionCache.GetDetailGenerationAsync(second, CancellationToken.None);

        await _auctionCache.InvalidateDetailAsync(first, CancellationToken.None);

        Assert.Equal(untouched, await _auctionCache.GetDetailGenerationAsync(second, CancellationToken.None));
    }
}
