using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class AuctionCacheSeedingTests
{
    private const int Callers = 32;

    private readonly ICacheService _cache = TestHarness.CreateCacheService();
    private readonly AuctionCache _auctionCache;

    public AuctionCacheSeedingTests() => _auctionCache = TestHarness.CreateAuctionCache(_cache);

    [Fact]
    public async Task A_cold_list_generation_hands_every_caller_in_the_first_wave_the_same_token()
    {
        var tokens = await Task.WhenAll(
            Enumerable.Range(0, Callers).Select(_ =>
                Task.Run(() => _auctionCache.GetListGenerationAsync(CancellationToken.None))));

        Assert.Single(tokens.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_cold_detail_generation_hands_every_caller_in_the_first_wave_the_same_token()
    {
        var auctionId = Guid.CreateVersion7();

        var tokens = await Task.WhenAll(
            Enumerable.Range(0, Callers).Select(_ =>
                Task.Run(() => _auctionCache.GetDetailGenerationAsync(auctionId, CancellationToken.None))));

        Assert.Single(tokens.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_first_wave_settles_on_one_cache_key_so_the_page_is_only_built_once()
    {
        var keys = await Task.WhenAll(
            Enumerable.Range(0, Callers).Select(_ => Task.Run(async () =>
            {
                var generation = await _auctionCache.GetListGenerationAsync(CancellationToken.None);

                return AuctionCache.ListKey(generation, 1, 20, null, null, null);
            })));

        Assert.Single(keys.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Two_auctions_still_get_generations_of_their_own()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        var firstToken = await _auctionCache.GetDetailGenerationAsync(first, CancellationToken.None);
        var secondToken = await _auctionCache.GetDetailGenerationAsync(second, CancellationToken.None);

        Assert.NotEqual(firstToken, secondToken);
    }

    [Fact]
    public async Task Seeding_does_not_swallow_a_later_rotation()
    {
        var seeded = await _auctionCache.GetListGenerationAsync(CancellationToken.None);

        await _auctionCache.InvalidateListsAsync(CancellationToken.None);

        var rotated = await Task.WhenAll(
            Enumerable.Range(0, Callers).Select(_ =>
                Task.Run(() => _auctionCache.GetListGenerationAsync(CancellationToken.None))));

        Assert.Single(rotated.Distinct(StringComparer.Ordinal));
        Assert.NotEqual(seeded, rotated[0]);
    }
}
