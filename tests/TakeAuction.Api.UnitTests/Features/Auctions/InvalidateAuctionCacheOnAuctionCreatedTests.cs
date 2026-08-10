using Microsoft.Extensions.Logging.Abstractions;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions;

public sealed class InvalidateAuctionCacheOnAuctionCreatedTests
{
    private readonly ICacheService _cache = TestHarness.CreateCacheService();
    private readonly AuctionCache _auctionCache;
    private readonly InvalidateAuctionCacheOnAuctionCreated _handler;

    public InvalidateAuctionCacheOnAuctionCreatedTests()
    {
        _auctionCache = TestHarness.CreateAuctionCache(_cache);
        _handler = new InvalidateAuctionCacheOnAuctionCreated(
            _auctionCache,
            NullLogger<InvalidateAuctionCacheOnAuctionCreated>.Instance);
    }

    [Fact]
    public async Task Rotates_the_list_generation()
    {
        var before = await _auctionCache.GetListGenerationAsync(CancellationToken.None);

        await _handler.Handle(Event(), CancellationToken.None);

        var after = await _auctionCache.GetListGenerationAsync(CancellationToken.None);
        Assert.NotEqual(before, after);
    }

    private static AuctionCreatedEvent Event() => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        100m,
        nameof(AuctionStatus.Active),
        TestHarness.Now.AddDays(1),
        TestHarness.Now);
}
